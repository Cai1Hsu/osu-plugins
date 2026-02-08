using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyExperience.Tests;

public partial class TestSceneLegacyButton : LocalSkinTestScene
{
    private SkinProvidingContainer content = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(content = new SkinProvidingContainer(new DefaultLegacySkin(this))
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    private LegacyButton button = null!;

    private void clickFeedback()
    {
        var text = new SpriteText
        {
            Text = "Clicked!",
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = Colour4.Yellow,
        };

        Add(text);

        text.MoveToOffset(new Vector2(0, -50), 500, Easing.OutQuint)
            .FadeOut(500, Easing.OutQuint)
            .Expire();
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("clear content", () => content.Clear());
        AddStep("add button", () =>
        {
            content.Add(button = new LegacyButton("Test Button", new Vector2(460, 40))
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                BackgroundColour = Colour4.OrangeRed,
                Action = clickFeedback,
            });
        });
        AddStep("hide button", () => button.Hide());
        AddStep("show button", () => button.Show());
        AddToggleStep("toggle enabled", b => button.Enabled.Value = b);
    }
}
