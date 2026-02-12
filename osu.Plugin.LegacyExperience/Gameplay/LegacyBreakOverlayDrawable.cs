using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Threading;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Audio;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

/// <summary>
/// The base skin component of legacy break overlay, usually used for testing purposes.
/// </summary>
public partial class LegacyBreakOverlayDrawable : CompositeDrawable
{
    [Resolved]
    private ISkinSource? skin { get; set; } = null;

    protected readonly Container WarningArrowContainer;
    protected readonly Sprite SectionRankingSprite;

    // skin conponents size are correctly scaled, but here we also scale positions to match stable's coordinates
    // see https://github.com/ppy/osu/blob/b6dc64668e9a7b9468fc1d54002a0b9a57a0c56a/osu.Game.Rulesets.Osu/UI/OsuPlayfieldAdjustmentContainer.cs#L57
    private const float stable_magic_ratio = 1.6f;
    private static readonly Vector2 warning_arrow_position = new Vector2(80, 100) * stable_magic_ratio;
    private const float warning_arrow_duration = 100;

    public LegacyBreakOverlayDrawable()
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Position = Vector2.Zero;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            SectionRankingSprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            },
            WarningArrowContainer = new Container()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new WarningArrow
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.Centre,
                        Position = warning_arrow_position,
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.Centre,
                        Position = new Vector2(warning_arrow_position.X, -warning_arrow_position.Y),
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-warning_arrow_position.X, warning_arrow_position.Y),
                        Scale = new Vector2(-1, 1),
                    },
                    new WarningArrow
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.Centre,
                        Position = new Vector2(-warning_arrow_position.X, -warning_arrow_position.Y),
                        Scale = new Vector2(-1, 1),
                    }
                },
            }
        };
    }

    [Resolved]
    private AudioEngine audioEngine { get; set; } = null!;

    public void PlayWarningAnimation(int loopCount)
    {
        if (loopCount <= 0)
            return;

        var transform = WarningArrowContainer.FadeIn()
            .Delay(warning_arrow_duration)
            .FadeOut();

        if (loopCount > 1)
            transform.Loop(warning_arrow_duration, loopCount);
    }

    private ScheduledDelegate? scheduledSamplePlay;

    public void PlayBreakRankingAnimation(bool passing)
    {
        ClearBreakRankingAnimation();

        scheduledSamplePlay?.Cancel();
        scheduledSamplePlay = Schedule(() =>
        {
            playSample();
            scheduledSamplePlay = null;
        });

        updateSectionRankingTexture(passing ? skin?.GetTexture("section-pass") : skin?.GetTexture("section-fail"));

        playAnimation();

        void playSample()
        {
            audioEngine?.PlaySample(passing ? LegacySample.sectionpass : LegacySample.sectionfail);
        }

        void playAnimation()
        {
            if (passing)
                PlayPassingAnimation();
            else
                PlayFailingAnimation();
        }
    }

    public void ClearAnimations()
    {
        ClearBreakRankingAnimation();
        ClearWarningArrowsAnimation();
    }

    public void ClearWarningArrowsAnimation()
    {
        WarningArrowContainer.ClearTransforms();
        WarningArrowContainer.FadeOut();
    }

    public void ClearBreakRankingAnimation()
    {
        SectionRankingSprite.ClearTransforms();
        SectionRankingSprite.FadeOut();

        scheduledSamplePlay?.Cancel();
        scheduledSamplePlay = null;
    }

    private void updateSectionRankingTexture(Texture? texture)
    {
        if (texture is not null)
        {
            SectionRankingSprite.Texture = texture;

            // size won't auto-update
            SectionRankingSprite.Size = texture.DisplaySize;
        }
        else
            // Hide to avoid showing old texture
            SectionRankingSprite.Alpha = 0;
    }

    protected void PlayPassingAnimation()
    {
        SectionRankingSprite
            .Delay(20)
            .FadeInFromZero()
            .Delay(80)
            .FadeOutFromOne()
            .Delay(60)
            .FadeInFromZero()
            .Delay(70)
            .FadeOutFromOne()
            .Delay(50)
            .FadeInFromZero()
            .Delay(1000)
            .FadeOutFromOne(200);
    }

    protected void PlayFailingAnimation()
    {
        SectionRankingSprite
            .Delay(130)
            .FadeInFromZero()
            .Delay(100)
            .FadeOutFromOne()
            .Delay(50)
            .FadeInFromZero()
            .Delay(1000)
            .FadeOutFromOne(200);
    }

    private partial class WarningArrow : Sprite
    {
        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            Texture? texture = skin.GetTexture("arrow-warning")
                ?? skin.GetTexture("arrow-pause");

            if (texture is not null)
            {
                Texture = texture;
            }
            else if (skin.GetTexture("play-warningarrow") is Texture legacyTexture)
            {
                Texture = legacyTexture;
                Colour = Colour4.Red;
            }
        }
    }
}
