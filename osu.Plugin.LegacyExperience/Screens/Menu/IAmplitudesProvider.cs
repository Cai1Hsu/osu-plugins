namespace osu.Plugin.LegacyExperience.Screens.Menu;

public interface IAmplitudesProvider
{
    public const int SampleSize = 1024;

    ReadOnlySpan<float> Data { get; }

    float Epicness { get; set; }

    bool UseTrackAmplitudes { get; set; }
}
