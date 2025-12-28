using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Plugins;

namespace osu.Plugin.Template;

public class TemplatePlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        Thread.Sleep(10 * 1000); // simulate long loading task
        Logger.Log("TemplatePlugin loaded!", LoggingTarget.Runtime, LogLevel.Important);
    }
}
