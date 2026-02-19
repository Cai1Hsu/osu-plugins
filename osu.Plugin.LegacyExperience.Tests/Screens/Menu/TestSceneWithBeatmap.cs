using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Tests.Visual;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public abstract partial class TestSceneWithBeatmap : OsuTestScene
{
    [Test]
    public void TestMusic()
    {
        AddStep("stop", () =>
        {
            musicController.Stop();
        });

        AddStep("restart play", () => musicController.Play(restart: true));
        AddStep("toggle pause", () => musicController.TogglePause());

        AddStep("play circles!", () => playBeatmap("circles.osz", "3c8b1fcc9434dbb29e2fb613d3b9eada9d7bb6c125ceb32396c3b53437280c83"));
        AddStep("play triangles", () => playBeatmap("triangles.osz", "a1556d0801b3a6b175dda32ef546f0ec812b400499f575c44fccbe9c67f9b1e5"));
        AddStep("play welcome", () => playBeatmap("welcome.osz", "64e00d7022195959bfa3109d09c2e2276c8f12f486b91fcf6175583e973b48f2"));

        AddSliderStep("seek", 0f, 1f, 0f, value =>
        {
            var target = musicController.CurrentTrack.Length * value;
            musicController.SeekTo(target);
        });
    }

    [Resolved]
    private MusicController musicController { get; set; } = null!;

    [Resolved]
    private Bindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

    [Resolved]
    private BeatmapManager beatmapManager { get; set; } = null!;

    [Resolved]
    private OsuGameBase game { get; set; } = null!;

    private void playBeatmap(string file, string hash)
    {
        var import = beatmapManager.Import(
            new ImportTask(game.Resources.GetStream($"Tracks/{file}"), file)).GetResultSafely();

        import?.PerformWrite(b => b.Protected = true);

        var setInfo = beatmapManager.QueryBeatmapSet(b => b.Protected && b.Hash == hash);

        ArgumentNullException.ThrowIfNull(setInfo);

        var beatmap = setInfo.PerformRead(s => s.Beatmaps.First());
        var working = beatmapManager.GetWorkingBeatmap(beatmap);

        // trigger beatmap loading
        _ = working.Beatmap;

        workingBeatmap.Value = working;
        musicController.Play();
    }
}
