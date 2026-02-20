using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Plugin.LegacyExperience.Screens.Menu;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneMenuVisualisation : TestSceneWithBeatmap
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
}
