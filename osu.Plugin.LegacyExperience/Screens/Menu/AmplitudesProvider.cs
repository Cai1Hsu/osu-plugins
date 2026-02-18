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

    /// <summary>
    /// Updates the provider's amplitude buffer for the current frame.
    /// </summary>
    /// <remarks>
    /// Chooses between applying track-derived amplitudes and applying temporal shrinkage:
    /// uses track amplitudes when <see cref="UseTrackAmplitudes"/> is true or when the current track is running and its playback time is within the track bounds; otherwise applies shrinkage to decay existing amplitudes.
    /// </remarks>
    protected override void Update()
    {
        base.Update();

        var currentTrack = musicController.CurrentTrack;
        var extendedTime = currentTrack.HasCompleted ||
            currentTrack.CurrentTime <= 0 ||
            currentTrack.CurrentTime >= currentTrack.Length;

        if (UseTrackAmplitudes || (currentTrack.IsRunning && !extendedTime))
            applyTrackAmplitudes();
        else
            applyShrinkage();
    }

    /// <summary>
    /// Gradually reduces stored amplitude samples toward zero based on elapsed frame time.
    /// </summary>
    /// <remarks>
    /// Applies an exponential decay normalized to a 60 FPS baseline and sets values below 0.01 to zero.
    /// </remarks>
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

    /// <summary>
    /// Populates the internal amplitude buffer by expanding the beat-sync frequency amplitudes into the provider's sample resolution and applying per-sample boost weighting.
    /// </summary>
    /// <remarks>
    /// Each source amplitude is linearly interpolated across an expansion window (wrapping the last sample to the first) and multiplied by a precomputed boost factor for that position. After expansion, the entire buffer is multiplied by <see cref="Epicness"/> when its value is not 1.
    /// </remarks>
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

    /// <summary>
/// Linearly interpolates between two float values.
/// </summary>
/// <param name="start">The value at interpolation factor 0.</param>
/// <param name="final">The value at interpolation factor 1.</param>
/// <param name="amount">Interpolation factor; 0 returns <paramref name="start"/>, 1 returns <paramref name="final"/>.</param>
/// <returns>The interpolated value between <paramref name="start"/> and <paramref name="final"/> for the given <paramref name="amount"/>.</returns>
private static float LerpF(float start, float final, float amount) => start + (final - start) * amount;

    // FFT512 -> FFT2048 expansion, osu!framework hardcoded 256 samples for FFT512
    private const int expand_factor = SampleSize / ChannelAmplitudes.AMPLITUDES_SIZE;
    private static readonly float[] boost_factors = Enumerable.Range(0, expand_factor)
                                                              .Select(static i => boost_factor((float)i / expand_factor))
                                                              .ToArray();

    /// <summary>
    /// Computes a normalized exponential boost multiplier for a position in the range [0, 1].
    /// </summary>
    /// <param name="t">Normalized position in the range [0, 1], where 0 yields no boost and 1 yields full boost.</param>
    /// <returns>A boost factor between 0 and 1 corresponding to <paramref name="t"/>.</returns>
    private static float boost_factor(float t)
    {
        const float alpha = 1.5f;

        float exp_alpha = MathF.Exp(-alpha);
        return (MathF.Exp(-alpha * t) - exp_alpha) / (1 - exp_alpha);
    }
}