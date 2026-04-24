using osu.Framework.Bindables;
using osu.Framework.Threading;

namespace osu.Game.Plugins;

public abstract class OsuPlugin
{
    public OsuPlugin()
    {
    }

    /// <summary>
    /// The display name of the plugin.
    /// </summary>
    public virtual string? DisplayName => null;

    /// <summary>
    /// A short description of the plugin.
    /// </summary>
    public virtual string? Description => null;

    internal readonly BindableBool enabled = new BindableBool(true);

    /// <summary>
    /// Whether the plugin is enabled. This is set to <see langword="true"/> after the plugin is activated.
    /// </summary>
    public Bindable<bool> Enabled => enabled;

    /// <summary>
    /// Interrupts the activation of this plugin, disabling it in the process.
    /// 
    /// For example, if a plugin is Windows-only and detects a non-Windows platform, 
    /// it can call this method during activation to prevent itself from being enabled.
    /// </summary>
    /// <param name="reason">The reason for the interruption.</param>
    /// <exception cref="PluginActivationInterruptedException">Thrown when the activation is interrupted.</exception>
    protected void CancelActivation(string? reason)
    {
        bool disabled = enabled.Disabled;
        enabled.Disabled = false;
        enabled.Value = false;
        enabled.Disabled = disabled;

        throw new PluginActivationInterruptedException(reason);
    }

    /// <summary>
    /// Invoked when the plugin is loaded. To execute code on the update thread, use the provided <see cref="Scheduler"/>.
    /// </summary>
    /// <param name="gameBase">The game instance.</param>
    /// <param name="scheduler">The scheduler for executing code on the update thread.</param>
    public virtual void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
    }


    [Serializable]
    public class PluginActivationInterruptedException : Exception
    {
        public string? Reason => Message;

        public PluginActivationInterruptedException(string? reason = null) : base(reason)
        {
        }
    }
}
