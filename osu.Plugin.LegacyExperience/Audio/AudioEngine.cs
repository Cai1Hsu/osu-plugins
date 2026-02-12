using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Game;
using osu.Game.Audio;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyExperience.Audio;

/// <summary>
/// An audio manager that provides osu!stable AudioEngine style API.
/// Provides as a helper for transplanting osu!stable code.
/// </summary>
public partial class AudioEngine : Component
{
    [Resolved]
    private AudioManager frameworkAudio { get; set; } = null!;

    [Resolved]
    private ISkinSource? skin { get; set; }

    private InputManager inputManager = null!;

    private FrozenDictionary<LegacySample, ISample?> samples = FrozenDictionary<LegacySample, ISample?>.Empty;

    [BackgroundDependencyLoader]
    private void load()
    {
        skin?.SourceChanged += updateSamples;
        updateSamples();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        inputManager = GetContainingInputManager();
    }

    private static readonly LegacySample[] all_usages = Enum.GetValues<LegacySample>();
    private static readonly SampleInfo[] all_sample_infos = Array.ConvertAll(all_usages, static usage => new SampleInfo(usage.GetDescription()));

    private void updateSamples()
    {
        var samples = new Dictionary<LegacySample, ISample?>();

        foreach (var usage in all_usages)
        {
            var sample = resolveSample(usage);

            sample?.PlaybackConcurrency.Value = OsuGameBase.SAMPLE_CONCURRENCY;

            samples[usage] = sample;
        }

        this.samples = samples.ToFrozenDictionary();
    }

    // to avoid conflict with osu!lazer's own samples
    private const string sample_namespace = nameof(LegacyExperience);

    private ISample? resolveSample(LegacySample usage)
    {
        var sampleInfo = all_sample_infos[(int)usage];

        return skin?.GetSample(sampleInfo)
            ?? sampleInfo.LookupNames
                .Select(n => frameworkAudio.Samples.Get($"{sample_namespace}/{n}"))
                .FirstOrDefault(static s => s is not null);
    }

    public SampleChannel? this[LegacySample usage] => samples[usage]?.GetChannel();

    private double clickSoundTime = double.MinValue;

    /// <summary>
    /// Plays a click sound.
    /// </summary>
    /// <param name="volume">The volume of the click sound, from 0 to 100.</param>
    /// <param name="sample">The sample to play.</param>
    /// <param name="speed">The speed (frequency) to play the sample at. Values above 1 speed up playback.</param>
    /// <param name="force">Whether to force play the click sound, ignoring cooldown.</param>
    public void Click(int volume = 100, LegacySample sample = LegacySample.menuclick, float speed = 1, bool force = false)
    {
        const double click_cooldown = 50; // match stable

        // TODO: stable also checks GameBase.Instance.IsActive, do we need to?

        // stable doesn't guard cases where sample is missing, which means cooldown is consumed even if no sound is played.
        // this isn't a big deal, just mentioning it here for completeness.
        if (Clock.CurrentTime - clickSoundTime <= click_cooldown && !force)
            return;

        clickSoundTime = Clock.CurrentTime;

        PlaySamplePositional(sample, c =>
        {
            c.Volume.Value = volume / 100.0;
            c.Frequency.Value = speed;
        });
    }

    /// <summary>
    /// Plays a sample with positional audio based on the current mouse position.
    /// The sample's balance will be set to match the horizontal position of the mouse, with the center of the screen being balanced, left being negative balance, and right being positive balance.
    /// </summary>
    /// <param name="sample">The sample to play.</param>
    /// <param name="configure"> An optional action to configure the sample channel before playing. Can be used to set volume, speed, etc.</param>
    public void PlaySamplePositional(LegacySample sample, Action<SampleChannel>? configure)
    {
        if (this[sample] is SampleChannel channel)
        {
            configure?.Invoke(channel);

            channel.Balance.Value = currentPositionalBalance() * 0.4; // match stable scaling
            channel.Play();
        }
    }

    /// <summary>
    /// Returns the current positional balance based on the mouse position. Ranges from -0.5 (left) to 0.5 (right).
    /// The full balance range is -1 to 1, but stable scales it down to 0.5.
    /// </summary>
    /// <returns>The current positional balance.</returns>
    private float currentPositionalBalance()
    {
        var screenSpaceMouse = inputManager.CurrentState.Mouse.Position;

        var localPosition = inputManager.ToLocalSpace(screenSpaceMouse);
        var normalized = new Vector2(
            localPosition.X / inputManager.DrawWidth,
            localPosition.Y / inputManager.DrawHeight
        ) - Vector2.One * 0.5f;

        return Math.Clamp(normalized.X, -0.5f, 0.5f);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateSamples;
    }
}
