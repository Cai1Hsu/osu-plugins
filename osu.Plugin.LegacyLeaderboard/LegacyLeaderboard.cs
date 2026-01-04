using osu.Framework.Allocation;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;


namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboard : LegacyLeaderboardBase, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; }

    [Resolved]
    private Player? player { get; set; }

    [Resolved]
    private IGameplayLeaderboardProvider leaderboardProvider { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        // TODO
    }
}