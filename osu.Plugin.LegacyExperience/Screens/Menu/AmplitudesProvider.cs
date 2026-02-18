using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Utils;
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

        // FFT512 -> FFT2048 expansion, osu!framework hardcoded 256 samples for FFT512
        const int expand_factor = SampleSize / ChannelAmplitudes.AMPLITUDES_SIZE;

        // TODO: may use better interpolation method if needed, but looks good to me for now. 
        for (int i = 0; i < ChannelAmplitudes.AMPLITUDES_SIZE; i++)
        {
            float prev = source[(i - 1 + ChannelAmplitudes.AMPLITUDES_SIZE) % ChannelAmplitudes.AMPLITUDES_SIZE];
            float v = source[i];
            float next = source[(i + 1) % ChannelAmplitudes.AMPLITUDES_SIZE];

            int baseIndex = i * expand_factor;

            // peak boosting, may look better when the source is too flat
            data[baseIndex] = (float)Interpolation.Lerp(prev, v, 0.5f) * 0.2f;
            data[baseIndex + 1] = (float)Interpolation.Lerp(v, next, 0.75f) * 0.6f;
            data[baseIndex + 2] = v;
            data[baseIndex + 3] = (float)Interpolation.Lerp(v, next, 0.25f) * 0.6f;
        }

        if (Epicness != 1)
        {
            for (int i = 0; i < SampleSize; i++)
                data[i] *= Epicness;
        }
    }
}
