using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Gameplay;
using osuTK;

namespace osu.Plugin.LegacyExperience.Tests.Gameplay;

public partial class TestSceneLegacyJudgements : OsuTestScene
{
    private LegacyJudgements legacyJudgements = null!;

    private InputManager inputManager = null!;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        inputManager = GetContainingInputManager();
    }

    private Colour4 colour = Colour4.Red;

    [SetUpSteps]
    public void SetupSteps()
    {
        AddStep("add legacy judgements", () =>
        {
            Clear();

            Add(new ClickableContainer()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(100, 10),
                Scale = new Vector2(5),
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.DarkGray.Opacity(0.5f),
                    },
                    legacyJudgements = new LegacyJudgements
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                    }
                },
                Action = () =>
                {
                    if (legacyJudgements is null)
                        return;

                    var spawnPosition = legacyJudgements.ToLocalSpace(inputManager.CurrentState.Mouse.Position);
                    legacyJudgements.SpawnSpark(spawnPosition, colour);
                }
            });
        });

        AddStep("clear sparks", () =>
        {
            if (legacyJudgements is null)
                return;

            legacyJudgements.Clear();
        });

        AddStep("random colour", () =>
        {
            colour = new Colour4(
                RNG.NextSingle(),
                RNG.NextSingle(),
                RNG.NextSingle(),
                1f);
        });
    }
}
