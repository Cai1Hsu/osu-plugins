using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyBreakOverlay;

/// <summary>
/// The base skin component of legacy break overlay, ususally used for testing purposes.
/// </summary>
public partial class LegacyBreakOverlayBase : CompositeDrawable
{
    [Resolved]
    private ISkinSource? skin { get; set; } = null;

    protected readonly Container WarningArrowContainer;
    protected readonly Sprite SectionRankingSprite;

    private static readonly Vector2 warning_arrow_position = new Vector2(80, 100);
    private const float warning_arrow_duration = 100;

    public LegacyBreakOverlayBase()
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
            },
            WarningArrowContainer = new Container()
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
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

    private PoolableSkinnableSample? sectionPassSample = null;
    private PoolableSkinnableSample? sectionFailSample = null;

    [BackgroundDependencyLoader]
    private void load()
    {
        // FIXME: in tests, samples from skin were not used. but in normal play, they were used.
        AddInternal(sectionPassSample = new PoolableSkinnableSample(new SampleInfo("Gameplay/sectionpass")));
        AddInternal(sectionFailSample = new PoolableSkinnableSample(new SampleInfo("Gameplay/sectionfail")));
    }

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

        scheduledSamplePlay = Schedule(() =>
        {
            playSample();
            scheduledSamplePlay = null;
        });

        Texture? texture = skin?.GetTexture(passing ? "section-pass" : "section-fail");

        if (texture is null)
            return;

        SectionRankingSprite.Texture = texture;

        playAnimation();

        void playSample()
        {
            if (passing)
                sectionPassSample?.Play();
            else
                sectionFailSample?.Play();
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
                Texture = texture;
        }
    }
}
