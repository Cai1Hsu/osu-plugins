namespace osu.Game.Rulesets.PluginsLoader.Tests;

public partial class TestOsuGame : OsuGame
{
    public override bool UseDevelopmentServer => true;

    public override Version AssemblyVersion => new Version(0, 0, 0, 0);
}
