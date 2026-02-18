using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using static osu.Plugin.LegacyExperience.Screens.Menu.IAmplitudesProvider;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class AmplitudesProvider : Component, IAmplitudesProvider
{
    public ReadOnlySpan<float> Data => data;

    private readonly float[] data = new float[SampleSize];

    [Resolved]
    private IBeatSyncProvider beatSyncProvider { get; set; } = null!;

    public float Epicness { get; set; } = 1;

    [Resolved]
    private MusicController musicController { get; set; } = null!;

    public bool UseTrackAmplitudes { get; set; } = true;

    protected override void Update()
    {
        base.Update();

        var currentTrack = musicController.CurrentTrack;
        var extendedTime = currentTrack.HasCompleted ||
            currentTrack.CurrentTime <= 0 ||
            currentTrack.CurrentTime >= currentTrack.Length;

        if (!UseTrackAmplitudes && (!currentTrack.IsRunning || extendedTime))
        {
            applyShrinkage();
        }
        else
        {
            applyTrackAmplitudes();
        }
    }

    private void applyShrinkage()
    {
        const double sixty_fps = 1000.0 / 60;

        double frameRatio = Clock.ElapsedFrameTime / sixty_fps;
        double factor = Math.Pow(0.95, frameRatio);

        for (int i = 0; i < SampleSize; i++)
        {
            double v = data[i] * factor;

            if (v < 0.01f)
                v = 0;

            data[i] = (float)v;
        }
    }

    private void applyTrackAmplitudes()
    {
        var source = beatSyncProvider.CurrentAmplitudes.FrequencyAmplitudes.Span;

        // TODO: may use better interpolation method if needed, but looks good to me for now. 
        for (int i = 0; i < ChannelAmplitudes.AMPLITUDES_SIZE; i++)
        {
            float v = source[i];
            float next = source[(i + 1) % ChannelAmplitudes.AMPLITUDES_SIZE];

            int baseIndex = i * expand_factor;

            for (int j = 0; j < expand_factor; j++)
            {
                float t = (float)j / expand_factor;
                float boost = boost_factors[j];

                data[baseIndex + j] = LerpF(v, next, t) * boost;
            }
        }

        if (Epicness != 1)
        {
            for (int i = 0; i < SampleSize; i++)
                data[i] *= Epicness;
        }
    }

    private static float LerpF(float start, float final, float amount) => start + (final - start) * amount;

    // FFT512 -> FFT2048 expansion, osu!framework hardcoded 256 samples for FFT512
    private const int expand_factor = SampleSize / ChannelAmplitudes.AMPLITUDES_SIZE;
    private static readonly float[] boost_factors = Enumerable.Range(0, expand_factor)
                                                              .Select(static i => boost_factor((float)i / expand_factor))
                                                              .ToArray();

    // peak boosting, may look better when the source is too flat
    private static float boost_factor(float t)
    {
        const float alpha = 1.5f;

        float exp_alpha = MathF.Exp(-alpha);
        return (MathF.Exp(-alpha * t) - exp_alpha) / (1 - exp_alpha);
    }
}
