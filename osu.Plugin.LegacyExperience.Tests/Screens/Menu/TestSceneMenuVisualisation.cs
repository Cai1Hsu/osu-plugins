using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Tests.Visual;
using osu.Game.Overlays;
using osu.Plugin.LegacyExperience.Screens.Menu;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneMenuVisualisation : OsuTestScene
{
    private partial class TestAmplitudesProvider(AmplitudesProvider amplitudes) : IAmplitudesProvider
    {
        ReadOnlySpan<float> IAmplitudesProvider.Data => UseTrackAmplitudes ? amplitudes.Data : data;

        public Span<float> Data => data;

        private readonly float[] data = new float[IAmplitudesProvider.SampleSize];

        public float Epicness { get; set; } = 1;
        public bool UseTrackAmplitudes { get; set; } = false;

        private readonly AmplitudesProvider amplitudes = amplitudes;
    }

    [Cached]
    private readonly AmplitudesProvider amplitudes = new AmplitudesProvider();

    [Cached(typeof(IAmplitudesProvider))]
    private readonly TestAmplitudesProvider provider;

    public TestSceneMenuVisualisation()
    {
        provider = new TestAmplitudesProvider(amplitudes);
    }

    private MenuVisualisation visualisation = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            amplitudes,
            visualisation = new MenuVisualisation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            }
        });
    }

    [Test]
    public void TestAmplitudesSource()
    {
        AddStep("use track amplitudes", () =>
        {
            provider.UseTrackAmplitudes = true;
        });

        AddStep("use custom amplitudes", () =>
        {
            provider.UseTrackAmplitudes = false;
        });
    }

    [Test]
    public void TestData()
    {
        AddSliderStep("fill", 0f, 1f, 0f, value =>
        {
            provider.Data.Fill(value);
        });
    }

    [Test]
    public void TestRadius()
    {
        // roughly stable default value(note that it's a dynamic value)
        AddSliderStep("radius", 0f, 200f, 160f, value =>
        {
            visualisation.Radius = value * LegacyExperiencePlugin.StableRatio;
        });
    }

    [Test]
    public void TestAlpha()
    {
        AddSliderStep("alpha", 0f, 1f, 0.8f, value =>
        {
            visualisation.Alpha = value;
        });
    }

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

        workingBeatmap.Value = working;
        musicController.Play();
    }
}
