using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Testing.Drawables.Steps;
using osu.Game.Graphics.Containers;
using osu.Plugin.LegacyExperience.Seasonal;

namespace osu.Plugin.LegacyExperience.Tests.Seasonal;

public partial class SeasonalContainer : Container
{
    public Action<SeasonalContainer>? RecreateScene { get; init; }

    private OsuTextFlowContainer eventText = null!;

    private Container contentContainer = null!;

    protected override Container<Drawable> Content => IsLoaded ? contentContainer : this;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(eventText = new OsuTextFlowContainer
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            AutoSizeAxes = Axes.Both,
        });

        Add(contentContainer = new Container
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        SeasonalConfig.ActiveEvents.BindValueChanged(_ => updateEventText(), true);
        RecreateScene?.Invoke(this);
    }

    private void updateEventText()
    {
        eventText.Text = $"Active Seasonal Events: {SeasonalConfig.ActiveEvents.Value}";
    }

    [Cached(typeof(ISeasonalConfig))]
    public TestSeasonalConfig SeasonalConfig { get; } = new();

    public void TestSeasonal()
    {
        var scene = this.FindClosestParent<TestScene>();

        if (scene is null)
            throw new InvalidOperationException("SeasonalContainer must be added to a TestScene to use seasonal testing.");

        foreach (var @event in Enum.GetValues<SeasonalEvents>())
        {
            scene.AddStep(new ToggleStepButton
            {
                Text = $"Toggle {@event} event",
                IsSetupStep = false,
                Action = b =>
                {
                    if (b)
                        SeasonalConfig.ActiveEvents.Value |= @event;
                    else
                        SeasonalConfig.ActiveEvents.Value &= ~@event;

                    RecreateScene?.Invoke(this);
                },
            });
        }
    }
}
