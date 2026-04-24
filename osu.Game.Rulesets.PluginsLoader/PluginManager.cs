using System.Diagnostics;
using System.Reflection;
using Humanizer;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Menu;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginManager : Drawable
{
    [Resolved]
    private OsuGame game { get; set; } = null!;

    private const string plugin_library_prefix = "osu.Plugin.";

    private readonly HashSet<Assembly> loadedAssemblies = new();
    private readonly List<OsuPlugin> loadedPlugins = new();

    public IReadOnlyList<OsuPlugin> LoadedPlugins => loadedPlugins;

    private List<Task> loadingTasks = new();
    private List<Action> earlyLoadActions = new();

    private Stopwatch loadStopwatch = new Stopwatch();

    private bool hasPluginsFromStartupDirectory = false;

    public void LoadEarlyAssemblies(Storage? gameStorage)
    {
        Debug.Assert(!loadStopwatch.IsRunning);
        loadStopwatch.Start();

        try
        {
            loadPluginsFromAppDomain();

            if (gameStorage is not null)
                loadPluginsFromStorage(gameStorage, "plugins");
            else
                // This serves as a fallback to load plugins from storage
                tryLoadLocalEarlyAssemblies();

            // Place your plugins in the startup directory is a bad idea, they will be removed when the game updates.
            // This is generally for development purposes only.
            hasPluginsFromStartupDirectory |= loadPluginsFromDirectory(RuntimeInfo.StartupDirectory);
            hasPluginsFromStartupDirectory |= loadPluginsFromDirectory(AppContext.BaseDirectory);
        }
        finally
        {
            // do we have to stop here?
            loadStopwatch.Stop();
        }
    }

    private void tryLoadLocalEarlyAssemblies()
    {
        var ourLocation = typeof(PluginManager).Assembly.Location;
        var ourDirectory = Path.GetDirectoryName(ourLocation);

        if (string.IsNullOrEmpty(ourDirectory))
            return;

        loadPluginsFromDirectory(ourDirectory, "plugins");

        var parentDirectory = Path.GetDirectoryName(ourDirectory);

        if (string.IsNullOrEmpty(parentDirectory))
            return;

        loadPluginsFromDirectory(parentDirectory, "plugins");
    }

    [BackgroundDependencyLoader]
    private void load(OsuGame? game, INotificationOverlay? notification, Storage storage)
    {
        Debug.Assert(!loadStopwatch.IsRunning);

        loadStopwatch.Start();

        startEarlyLoadActionProcessing();

        loadPluginsFromStorage(storage, "plugins");

        performWhenMainMenuReady(game, notification, hasPluginsFromStartupDirectory);

        // Sometimes the load action still blocks update thread,
        // so we explicitly offload to thread pool here.
        Task.Factory.StartNew(() =>
        {
            try
            {
                Task.WhenAll(loadingTasks).Wait();

                loadStopwatch.Stop();

                lock (loadedPlugins)
                {
                    string loadedMessage = loadedPlugins.Count > 0
                        ? $"Successfully loaded {loadedPlugins.Count} plugins in {loadStopwatch.Elapsed.Humanize()}."
                        : "No plugins were loaded.";

                    notification?.Post(new PluginNotification
                    {
                        Text = loadedMessage,
                        Transient = true
                    });
                }
            }
            finally
            {
                lock (loadingTasks)
                {
                    loadingTasks.Clear();
                    // we have to keep the list reference around as some tasks may still be observing it.
                }
            }
        }, TaskCreationOptions.LongRunning);
    }

    void performWhenMainMenuReady(OsuGame? game, INotificationOverlay? notification, bool hasPluginsFromStartupDirectory)
    {
        if (game is null)
            return;

        void postNotifications(Drawable drawable)
        {
            var screen = (IScreen)drawable;

            if (!screen.IsCurrentScreen())
                return;

            // Bypass for debug build since debug build may use mock storage and we rely on startup directory for plugin loading during development.
            if (hasPluginsFromStartupDirectory && !DebugUtils.IsDebugBuild)
            {
                notification?.Post(new PluginNotification()
                {
                    Text = "Plugins loaded from the startup directory are discouraged because they are removed on game update. "
                        + "Please move your plugins to the 'plugins' folder inside the osu! data directory.",
                    IconColour = Colour4.Orange,
                });
            }

            bool hasPluginStillLoading = false;

            lock (loadingTasks)
            {
                hasPluginStillLoading = loadingTasks.Any(t => !t.IsCompleted);
            }

            if (hasPluginStillLoading)
            {
                notification?.Post(new PluginNotification()
                {
                    Text = $"Plugin loading is taking longer than expected. "
                        + "Some plugins may still be loading in the background. "
                        + $"{loadedPlugins.Count} plugins have been loaded so far.",
                    Transient = true,
                });
            }
        }

        game.PerformOnceExcludeScreen((_, newScreen) =>
        {
            if (newScreen is not Drawable drawable)
                return;

            drawable.InvokeWhenReady(postNotifications);
        }, new[] { typeof(Loader), typeof(IntroScreen) });
    }

    private void startEarlyLoadActionProcessing()
    {
        Debug.Assert(LoadState is LoadState.Loading);

        foreach (var action in earlyLoadActions)
            scheduleBackground(action);

        earlyLoadActions.Clear();
    }

    private void scheduleBackground(Action action)
    {
        if (LoadState < LoadState.Loading)
            earlyLoadActions.Add(action);
        else
        {
            Debug.Assert(LoadState is LoadState.Loading or LoadState.Ready);

            lock (loadingTasks)
            {
                loadingTasks.Add(Task.Run(action));
            }
        }
    }

    private bool loadPluginsFromAppDomain()
    {
        bool loadedAny = false;

        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                string? rulesetName = assembly.GetName().Name;

                if (rulesetName == null)
                    continue;

                if (!rulesetName.StartsWith(plugin_library_prefix, StringComparison.InvariantCultureIgnoreCase)
                    || rulesetName.Contains(@"Tests"))
                    continue;

                loadedAny |= loadPluginAssembly(assembly);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies from AppDomain");
        }

        return loadedAny;
    }

    private bool loadPluginsFromDirectory(string directory, string subPath = ".")
    {
        try
        {
            if (!string.IsNullOrEmpty(directory) &&
                Directory.Exists(directory))
            {
                return loadPluginsFromStorage(new NativeStorage(directory), subPath);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies from startup path");
        }

        return false;
    }

    private bool loadPluginsFromStorage(Storage storage, string subPath)
    {
        bool loadedAny = false;

        try
        {
            if (!storage.ExistsDirectory(subPath))
                return false;

            var plugins = storage.GetFiles(subPath, "osu.Plugin.*.dll");

            foreach (var plugin in plugins)
            {
                var assembly = loadAssemblyFromStorage(storage.GetFullPath(plugin));

                if (assembly != null)
                {
                    loadedAny |= loadPluginAssembly(assembly);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies.");
        }

        return loadedAny;
    }

    private Assembly? loadAssemblyFromStorage(string path)
    {
        try
        {
            var assembly = Assembly.LoadFrom(path);

            return assembly;
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load plugin from path: {Path.GetFileName(path)}");
        }

        return null;
    }

    private bool loadPluginAssembly(Assembly assembly)
    {
        bool loadedAny = false;

        try
        {
            lock (loadedAssemblies)
            {
                if (loadedAssemblies.Contains(assembly))
                    return false;

                if (loadedAssemblies.Any(a => a.FullName == assembly.FullName))
                    return false;

                loadedAssemblies.Add(assembly);
            }

            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(OsuPlugin).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t.IsPublic);

            foreach (var type in pluginTypes)
            {
                scheduleBackground(() => instantiatePluginAndPerformLoad(type));
                loadedAny = true;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load plugin from assembly: {assembly.FullName}");
        }

        return loadedAny;
    }

    private void instantiatePluginAndPerformLoad(Type pluginType)
    {
        try
        {
            var pluginInstance = Activator.CreateInstance(pluginType) as OsuPlugin
                ?? throw new InvalidOperationException($"Failed to create instance of plugin type: {pluginType.FullName}");

            pluginInstance.OnLoad(game, Scheduler);

            lock (loadedPlugins)
            {
                loadedPlugins.Add(pluginInstance);
            }

            var enabled = plugin_enabled_field.GetValue(pluginInstance) as Bindable<bool>;

            if (enabled is not null)
                Scheduler.Add(() => enabled.Value = true);

            Logger.Log($"Successfully loaded plugin: {pluginType.FullName} from {pluginType.Assembly.Location}", LoggingTarget.Runtime, LogLevel.Verbose);
        }
        catch (OsuPlugin.PluginActivationInterruptedException pae)
        {
            Logger.Log($"{pluginType.FullName} cancelled load for {pae.Reason}", LoggingTarget.Runtime, LogLevel.Important);
        }
        catch (LoadException le)
        {
            Logger.Error(le, $"Failed to load plugin of type: {pluginType.FullName}");
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to instantiate plugin of type: {pluginType.FullName}, {e.Message}");
        }
    }

    // intentially not use InternalVisibleTo so that loader can be decoupled from the plugin library.
    private static readonly FieldInfo plugin_enabled_field = typeof(OsuPlugin)
        .GetField("enabled", BindingFlags.NonPublic | BindingFlags.Instance)!;
}
