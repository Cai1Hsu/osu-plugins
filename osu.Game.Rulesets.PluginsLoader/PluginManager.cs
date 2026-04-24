using System.Diagnostics;
using System.Reflection;
using AccessItEasy;
using Humanizer;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
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
    private readonly HashSet<string> scheduledPluginTypes = new(StringComparer.Ordinal);

    private readonly List<Task<OsuPlugin?>> pluginInstantiationTasks = new();

    private PluginConfigManager pluginConfigManager = null!;

    public IReadOnlyList<OsuPlugin> LoadedPlugins => loadedPlugins;

    private List<Task> loadingTasks = new();

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
    private void load(INotificationOverlay? notification, Storage storage)
    {
        Debug.Assert(!loadStopwatch.IsRunning);

        loadStopwatch.Start();

        loadPluginsFromStorage(storage, "plugins");

        createSettingsSection();
        performWhenMainMenuReady(game, notification, hasPluginsFromStartupDirectory);

        // Finish load pipeline on worker thread to avoid blocking the update thread.
        var pipelineTask = Task.Factory.StartNew(() =>
        {
            try
            {
                var instantiatedPlugins = awaitAllInstantiationTasks();

                loadPluginConfiguration(storage, instantiatedPlugins);

                var loadTasks = instantiatedPlugins
                    .Select(plugin => Task.Run(() => performPluginLoad(plugin)))
                    .ToArray();

                Task.WhenAll(loadTasks).Wait();

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
                    loadingTasks.RemoveAll(t => t.IsCompleted);
                }
            }
        }, TaskCreationOptions.LongRunning);

        lock (loadingTasks)
            loadingTasks.Add(pipelineTask);
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
                schedulePluginInstantiation(type);
                loadedAny = true;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to load plugin from assembly: {assembly.FullName}");
        }

        return loadedAny;
    }

    private void schedulePluginInstantiation(Type pluginType)
    {
        var pluginTypeId = pluginType.AssemblyQualifiedName ?? pluginType.FullName;

        if (string.IsNullOrEmpty(pluginTypeId))
            return;

        lock (pluginInstantiationTasks)
        {
            if (!scheduledPluginTypes.Add(pluginTypeId))
                return;

            pluginInstantiationTasks.Add(Task.Run(() => instantiatePlugin(pluginType)));
        }
    }

    private OsuPlugin? instantiatePlugin(Type pluginType)
    {
        try
        {
            var pluginInstance = Activator.CreateInstance(pluginType) as OsuPlugin
                ?? throw new InvalidOperationException($"Failed to create instance of plugin type: {pluginType.FullName}");

            Logger.Log($"Instantiated plugin: {pluginType.FullName} from {pluginType.Assembly.Location}", LoggingTarget.Runtime, LogLevel.Verbose);
            return pluginInstance;
        }
        catch (Exception e)
        {
            Logger.Error(e, $"Failed to instantiate plugin of type: {pluginType.FullName}, {e.Message}");
        }

        return null;
    }

    private OsuPlugin[] awaitAllInstantiationTasks()
    {
        Task<OsuPlugin?>[] tasks;

        lock (pluginInstantiationTasks)
            tasks = pluginInstantiationTasks.ToArray();

        Task.WhenAll(tasks).Wait();

        return tasks
            .Where(t => t.Status == TaskStatus.RanToCompletion && t.Result is not null)
            .Select(t => t.Result!)
            .ToArray();
    }

    private void loadPluginConfiguration(Storage storage, OsuPlugin[] plugins)
    {
        if (plugins.Length == 0)
            return;

        try
        {
            pluginConfigManager = new PluginConfigManager(storage, plugins);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to initialize plugin configuration manager.");
        }
    }

    private void performPluginLoad(OsuPlugin pluginInstance)
    {
        var pluginType = pluginInstance.GetType();

        try
        {
            pluginInstance.OnLoad(game, Scheduler);

            lock (loadedPlugins)
                loadedPlugins.Add(pluginInstance);

            Logger.Log($"Successfully loaded plugin: {pluginType.FullName} from {pluginType.Assembly.Location}", LoggingTarget.Runtime, LogLevel.Verbose);

            // TODO: we may want better ordering
            // TODO: PluginSubsection creation invoves reflection, consider asynchronously loading
            Scheduler.Add(settingsSection.Add, new PluginSubsection(pluginInstance));
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
            Logger.Error(e, $"Failed to load plugin of type: {pluginType.FullName}, {e.Message}");
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        pluginConfigManager.Dispose();
    }

    #region Settings integration

    private PluginsSection settingsSection = null!;

    private void createSettingsSection()
    {
        settingsSection = new PluginsSection();

        // note that SettingsOverlay is cached after our load call, so DI can't help us here,
        game.InvokeWhenReady(d =>
        {
            var settingsOverlay = ((OsuGameBase)d).Dependencies.Get<SettingsOverlay>();

            settingsOverlay.InvokeWhenReady(d =>
            {
                settingsOverlay.Add(new SettingsOverlayObserver
                {
                    Predicate = () => settingsOverlay.SectionsContainer.Count > 0,
                    Action = () =>
                    {
                        var section = settingsSection;

                        settingsOverlay.SectionsContainer.Add(section);
                        var sideBar = SettingsPanelAccessor.GetSidebar(settingsOverlay);

                        sideBar.Add(new SettingsOverlayObserver
                        {
                            Predicate = () => sideBar.Children.Any(c => c is SidebarIconButton),
                            Action = () => sideBar.Add(new SidebarIconButton()
                            {
                                Section = section,
                                Action = () =>
                                {
                                    if (!settingsOverlay.SectionsContainer.IsLoaded)
                                        return;

                                    settingsOverlay.SectionsContainer.ScrollTo(section);
                                },
                            })
                        });
                    }
                });
            });
        });
    }

    private abstract partial class SettingsPanelAccessor : SettingsPanel
    {
        protected SettingsPanelAccessor(bool showBackButton) : base(showBackButton) { }

        [PrivateAccessor(PrivateAccessorKind.Field, Name = nameof(Sidebar))]
        public static extern ref SettingsSidebar GetSidebar(SettingsPanel panel);
    }

    private partial class SettingsOverlayObserver : Drawable
    {
        public required Func<bool> Predicate { get; init; }
        public required Action Action { get; init; }

        protected override void Update()
        {
            base.Update();

            if (!Predicate())
                return;

            try
            {
                Action();
            }
            finally
            {
                Expire();
            }
        }
    }

    #endregion
}
