using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Plugin.StableIntegrationPlugin;

public partial class PlayInStableButton : ScreenFooterButton
{
    [Resolved]
    private StableIntegrationManager stableIntegrationManager { get; set; } = null!;

    [Resolved]
    private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(OsuColour colours)
    {
        Icon = FontAwesome.Solid.PlayCircle;
        Text = "Play in Stable";
        AccentColour = colours.Pink;

        Action = () =>
        {
            var stableController = stableIntegrationManager.StableController;
            var beatmap = currentBeatmap.Value;

            if (stableController?.IsAvailable is not true)
                return;

            stableController.OpenInStable(beatmap.BeatmapInfo);
        };

        currentBeatmap.BindValueChanged(_ => updateEnabledState(), true);
    }

    private void updateEnabledState()
    {
        bool enabled = true;

        enabled &= stableIntegrationManager.StableController?.IsAvailable ?? false;
        enabled &= currentBeatmap.Value.BeatmapInfo.OnlineID > 0;

        bool disabled = Enabled.Disabled;
        Enabled.Disabled = false;
        Enabled.Value = enabled;
        Enabled.Disabled = disabled;

        TooltipText = enabled
            ? "Play the current beatmap in stable via osu!direct."
            : "Cannot play in osu! Stable (beatmap may not be uploaded online or osu! Stable is not available).";
    }
}
