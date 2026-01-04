using osu.Framework.Platform;

namespace osu.Game.Rulesets.PluginsLoader.Tests;

public partial class TestOsuGame : OsuGame
{
    public const string GameName = @"osu-development";

    public override bool UseDevelopmentServer => true;

    public override Version AssemblyVersion => new Version(0, 0, 0, 0);
}
