using System.Reflection;
using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Plugin.Legacy;

public partial class LegacyEndReplayButtons : LegacySpriteButton, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public LegacyEndReplayButtons()
    {
        Texture = "UI/overlay-endreplay";

        Anchor = Anchor.CentreRight;
        Origin = Anchor.CentreRight;
    }

    [BackgroundDependencyLoader]
    private void load(Player? player)
    {
        if (player is ReplayPlayer replayPlayer)
        {
            Action = () => AttemptPerformExit(replayPlayer);
        }
        else
        {
            Sprite.Colour = Colour4.DarkGray;
        }
    }

    private static void AttemptPerformExit(ReplayPlayer replayPlayer)
    {
        if (performExitMethod is null)
            return;

        performExitMethod.Invoke(replayPlayer, new object[] { true });
    }

    static readonly MethodInfo? performExitMethod = typeof(Player)
        .GetMethod("PerformExit", BindingFlags.Instance | BindingFlags.NonPublic, new Type[] { typeof(bool) });
}