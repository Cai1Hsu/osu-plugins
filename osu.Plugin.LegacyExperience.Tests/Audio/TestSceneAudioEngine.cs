using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Tests.Seasonal;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.Tests.Audio;

public partial class TestSceneAudioEngine : LocalSkinTestScene
{
    private OsuTextFlowContainer infoText = null!;
    private AudioEngine audioEngine = null!;

    private readonly Bindable<LegacySample> clickSample = new Bindable<LegacySample>(LegacySample.menuhit);

    private SeasonalContainer seasonalContainer = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Child = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                infoText = new OsuTextFlowContainer(s => s.Font = s.Font.With(size: 16))
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    // ugly way to prevent overlapping with SeasonalContainer's event text, but it works for now
                    Margin = new MarginPadding { Top = 14 },
                },
                seasonalContainer = new SeasonalContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RecreateScene = c => c.Child = audioEngine = new AudioEngine(),
                }
            }
        };

        clickSample.BindValueChanged(_ => updateInfoText(), true);
    }

    private void updateInfoText()
    {
        infoText.Text = $"Current Click Sample: {clickSample.Value.GetDescription()} (Press 1 to play)";
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!e.Repeat)
        {
            switch (e.Key)
            {
                case Key.Number1:
                    audioEngine.Click(sample: clickSample.Value);
                    return true;
            }
        }
        return base.OnKeyDown(e);
    }

    [Test]
    public void TestClickEffect()
    {
        foreach (var sample in Enum.GetValues<LegacySample>())
        {
            AddStep($"Set {sample.GetDescription()}", () => clickSample.Value = sample);
        }

        AddStep("Click", () => audioEngine.Click(sample: clickSample.Value));
    }

    [Test]
    public void TestSeasonal() => seasonalContainer.TestSeasonal();
}
