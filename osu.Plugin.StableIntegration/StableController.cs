using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Overlays;

namespace osu.Plugin.StableIntegrationPlugin;

public abstract partial class StableController : Drawable
{
    public abstract bool IsAvailable { get; }

    public abstract Task MuteStable();

    public abstract Task OpenInStable(BeatmapInfo beatmap);

    [Resolved]
    private MusicController musicController { get; set; } = null!;

    // required to allow dependency to be injected
    [BackgroundDependencyLoader]
    private void load()
    {
    }

    protected void PreSwitchToStable()
    {
        if (musicController.AllowTrackControl.Value)
            musicController.Stop();
    }

    protected static string? GetStableProtocolUrl(BeatmapInfo beatmapInfo) => beatmapInfo.OnlineID switch
    {
        > 0 => $"osu://b/{beatmapInfo.OnlineID}",
        _ => null
    };
}
