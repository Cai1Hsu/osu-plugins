using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Gameplay;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.Tests.Gameplay;

public partial class TestSceneLegacySkipDrawable : LocalSkinTestScene
{
    private LegacySkipDrawable skipDrawable = null!;

    private SkinProvidingContainer skinContainer = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create skin", () =>
        {
            var skin = new DefaultLegacySkin(this);
            Child = skinContainer = new SkinProvidingContainer(skin);
        });

        AddStep("create skip drawable", () =>
        {
            skinContainer.Clear();

            skinContainer.Add(skipDrawable = new LegacySkipDrawable()
            {
                SkipRequested = () =>
                {
                    var text = new OsuSpriteText
                    {
                        Text = "Skip requested!",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    };

                    Add(text);

                    text.FlashColour(Color4.Yellow, 500, Easing.OutQuint)
                        .FadeOut(500, Easing.OutQuint)
                        .MoveToOffset(new osuTK.Vector2(0, -50), 500, Easing.OutQuint)
                        .Expire();
                }
            });
        });
    }
}
