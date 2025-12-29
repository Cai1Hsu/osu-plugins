using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Humanizer;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Plugins;
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

    [BackgroundDependencyLoader]
    private void load(OsuGame? game, INotificationOverlay? notification, Storage storage)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        loadPluginsFromAppDomain();
        loadPluginsFromStorage(storage, "plugins");

        // Place your plugins in the startup directory is a bad idea, they will be removed when the game updates.
        // This is generally for development purposes only.
        bool hasPluginsFromStartupDirectory = false;
        hasPluginsFromStartupDirectory |= loadPluginsFromDirectory(RuntimeInfo.StartupDirectory);
        hasPluginsFromStartupDirectory |= loadPluginsFromDirectory(AppContext.BaseDirectory);

        performWhenMainMenuReady(game, notification, hasPluginsFromStartupDirectory);

        // Sometimes the load action still blocks update thread,
        // so we explicitly offload to thread pool here.
        Task.Run(() =>
        {
            try
            {
                Task.WhenAll(loadingTasks).Wait();

                stopwatch.Stop();

                lock (loadedPlugins)
                {
                    string loadedMessage = loadedPlugins.Count > 0
                        ? $"Successfully loaded {loadedPlugins.Count} plugins in {stopwatch.Elapsed.Humanize()}."
                        : "No plugins were loaded.";

                    notification?.Post(new PluginNotification
                    {
                        Text = loadedMessage,
                    });
                }
            }
            finally
            {
                lock (loadingTasks)
                {
                    loadingTasks.Clear();
                    loadingTasks = null!;
                }
            }
        });
    }

    void performWhenMainMenuReady(OsuGame? game, INotificationOverlay? notification, bool hasPluginsFromStartupDirectory)
    {
        if (game is null)
            return;

        void postNotifications(Drawable screen)
        {
            var mainMenu = (MainMenu)screen;

            if (!mainMenu.IsCurrentScreen())
                return;

            // Bypass for debug build since debug build may use mock storage and we rely on startup directory for plugin loading during development.
            if (hasPluginsFromStartupDirectory && !DebugUtils.IsDebugBuild)
            {
                notification?.Post(new PluginNotification()
                {
                    Text = "Plugins loaded from the startup directory are discouraged because they are removed on game update. "
                        + "Please move your plugins to the 'plugins' folder inside the osu! data directory."
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
                });
            }
        }

        game.PerformOnceFromScreen((_, newScreen) =>
        {
            if (newScreen is not MainMenu mainMenu)
                return;

            if (mainMenu.IsLoaded)
                postNotifications(mainMenu);
            else
                mainMenu.OnLoadComplete += postNotifications;
        }, new[] { typeof(MainMenu) });
    }

    private void scheduleBackground(Action action)
    {
        lock (loadingTasks)
        {
            loadingTasks.Add(Task.Run(action));
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

    private bool loadPluginsFromStorage(Storage storage, string subPath = ".")
    {
        bool loadedAny = false;

        try
        {
            if (subPath != "." && !storage.Exists(subPath))
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

            Scheduler.Add(() => GetPluginEnabled(pluginInstance).Value = true);

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
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "enabled")]
    private static extern ref BindableBool GetPluginEnabled(OsuPlugin plugin);

    private partial class PluginNotification : SimpleNotification
    {
        public PluginNotification()
        {
            Icon = FontAwesome.Solid.PuzzlePiece;
        }
    }
}
