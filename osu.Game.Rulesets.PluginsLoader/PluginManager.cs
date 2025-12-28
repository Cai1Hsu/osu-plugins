using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Plugins;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginManager : Drawable
{
    [Resolved]
    private OsuGame game { get; set; } = null!;

    private bool hasPluginsFromStartupDirectory = false;

    private const string plugin_library_prefix = "osu.Plugin.";

    private readonly HashSet<Assembly> loadedAssemblies = new();
    private readonly List<OsuPlugin> loadedPlugins = new();

    public IReadOnlyList<OsuPlugin> LoadedPlugins => loadedPlugins;

    private List<Task> loadingTasks = new();

    private const double plugin_long_load_threshold = 100;

    // TODO: hard coded for now, consider making configurable if needed.
    private const double async_load_threshold = 3000;

    [BackgroundDependencyLoader]
    private void load(Storage storage)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        loadPluginsFromAppDomain();
        loadPluginsFromStorage(storage, false, "plugins");

        // Place your plugins in the startup directory is a bad idea, they will be removed when the game updates.
        // This is generally for development purposes only.
        loadPluginsFromDirectory(RuntimeInfo.StartupDirectory, true);
        loadPluginsFromDirectory(AppContext.BaseDirectory, true);

        // Sometimes the load action still blocks update thread,
        // so we explicitly offload to thread pool here.
        Task.Run(() =>
        {
            try
            {
                var loadTask = Task.WhenAll(loadingTasks);
                var timeoutTask = Task.Delay((int)async_load_threshold);

                // we don't want to block the game to wait for plugins to load, so we only wait a short amount of time here.
                var completed = Task.WhenAny(loadTask, timeoutTask).GetAwaiter().GetResult();

                void loadCompleted()
                {
                    // ensure any exceptions are observed.
                    loadTask.Wait();
                    stopwatch.Stop();

                    lock (loadedPlugins)
                    {
                        string loadedMessage = loadedPlugins.Count > 0
                            ? $"Successfully loaded {loadedPlugins.Count} plugins in {stopwatch.ElapsedMilliseconds:F2}ms."
                            : "No plugins were loaded.";

                        var logLevel = stopwatch.ElapsedMilliseconds > plugin_long_load_threshold
                            ? LogLevel.Important : LogLevel.Verbose;

                        Logger.Log(loadedMessage, LoggingTarget.Runtime, logLevel);
                    }
                }

                if (completed == loadTask)
                {
                    loadCompleted();
                }
                else
                {
                    lock (loadedPlugins)
                    {
                        Logger.Log($"Plugin loading is taking longer than expected (> {async_load_threshold}ms). "
                            + "Some plugins may still be loading in the background. "
                            + $"{loadedPlugins.Count} plugins have been loaded so far.", LoggingTarget.Runtime, LogLevel.Important);
                    }

                    loadTask.ContinueWith(_ => loadCompleted());
                }

                // Bypass for debug build since debug build may use mock storage and we rely on startup directory for plugin loading during development.
                if (hasPluginsFromStartupDirectory && !DebugUtils.IsDebugBuild)
                {
                    Logger.Log("Plugins loaded from the startup directory are not supported and may be removed on game update. "
                        + "Please move your plugins to the 'plugins' folder inside the osu! installation directory.", LoggingTarget.Runtime, LogLevel.Important);
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

    private void scheduleBackground(Action action)
    {
        lock (loadingTasks)
        {
            loadingTasks.Add(Task.Run(action));
        }
    }

    private void loadPluginsFromAppDomain()
    {
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

                loadPluginAssembly(assembly, false);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies from AppDomain");
        }
    }

    private void loadPluginsFromDirectory(string directory, bool isFromStartupDirectory, string subPath = ".")
    {
        try
        {
            if (!string.IsNullOrEmpty(directory) &&
                Directory.Exists(directory))
            {
                loadPluginsFromStorage(new NativeStorage(directory), isFromStartupDirectory, subPath);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies from startup path");
        }
    }

    private void loadPluginsFromStorage(Storage storage, bool isFromStartupDirectory, string subPath = ".")
    {
        try
        {
            if (subPath != "." && !storage.Exists(subPath))
                return;

            var plugins = storage.GetFiles(subPath, "osu.Plugin.*.dll");

            foreach (var plugin in plugins)
            {
                var assembly = loadAssemblyFromStorage(storage.GetFullPath(plugin));

                if (assembly != null)
                {
                    loadPluginAssembly(assembly, isFromStartupDirectory);
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Unable to enumerate plugin assemblies.");
        }
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

    private void loadPluginAssembly(Assembly assembly, bool isFromStartupDirectory)
    {
        try
        {
            lock (loadedAssemblies)
            {
                if (loadedAssemblies.Contains(assembly))
                    return;

                if (loadedAssemblies.Any(a => a.FullName == assembly.FullName))
                    return;

                loadedAssemblies.Add(assembly);
            }

            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(OsuPlugin).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t.IsPublic);

            foreach (var type in pluginTypes)
            {
                scheduleBackground(() => instantiatePluginAndPerformLoad(type));
                isFromStartupDirectory = true;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load plugin from assembly: {assembly.FullName}");
        }
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
}
