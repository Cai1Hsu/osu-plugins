using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardEntry : CompositeDrawable
{
    private GameplayLeaderboardScore? score;

    private const float stable_ratio = 1.6f;

    public LegacyLeaderboardEntry(GameplayLeaderboardScore? score = null)
    {
        this.score = score;
    }

    public LegacyLeaderboardEntry()
    {
    }

    private Container textureClipContainer = null!;

    [BackgroundDependencyLoader]
    private void load(ISkinSource? skin)
    {
        Texture? backgroundTexture = skin?.GetTexture("menu-button-background");

        AutoSizeAxes = Axes.Y;

        // FontStore
        // new RawCachingGlyphStore()

        // new SpriteText()
        // {
        //     Font = new FontUsage()
        // }

        InternalChildren = new Drawable[]
        {
            new Sprite()
            {
                Texture = backgroundTexture,
                Anchor = Anchor.Custom,
                Origin = Anchor.Custom,
                Scale = new Vector2(0.62f),
            }
            // new ScoreEntrySprite()
            // {
            //     Size = 
            //     Anchor = Anchor.CentreRight,
            //     Origin = Anchor.CentreRight,
            // },
        };

        if (score is not null)
            BindScoreEvents(score);
    }

    public void BindScore(GameplayLeaderboardScore? score)
    {
        if (this.score != null)
            UnbindScoreEvents(this.score);

        this.score = score;
    }

    private void BindScoreEvents(GameplayLeaderboardScore score)
    {
    }

    private void UnbindScoreEvents(GameplayLeaderboardScore score)
    {
        // TODO: unbind events
    }

    private partial class ScoreEntrySpriteText : SkinnableSpriteText
    {
        public ScoreEntrySpriteText(ISkinComponentLookup lookup, Func<ISkinComponentLookup, SpriteText> defaultImplementation, ConfineMode confineMode = ConfineMode.NoScaling)
            : base(lookup, defaultImplementation, confineMode)
        {
        }
    }

    private partial class ScoreEntrySprite : Container
    {
        private const float sprite_scale = 0.625f;

        [BackgroundDependencyLoader]
        private void load(ISkinSource? skin)
        {
            Masking = true;
            RelativeSizeAxes = Axes.Y;

            var texture = skin?.GetTexture("menu-button-background");

            // if (texture is not null)
            // {
            //     Child = new Sprite
            //     {
            //         Anchor = Anchor.CentreRight,
            //         Origin = Anchor.CentreRight,
            //         Texture = skin?.GetTexture("menu-button-background"),
            //         Scale = new Vector2(sprite_scale),
            //     };
            // }

            // if (texture is not null)
            // {
            //     float scale = sprite_scale / texture.ScaleAdjust;

            //     // clip to keep right 470 pixels
            //     // reference: https://github.com/Wieku/danser-go/blob/8331b0ffb841cc9e0f5e6b756bcf2bba2a9465c0/app/states/components/overlays/play/scoreboardentry.go#L126
            //     float clipWidth = 470 * scale;
            //     float clipHeight = texture.Height * scale;

            //     Size = new Vector2(clipWidth, clipHeight);

            //     Child = new Sprite
            //     {
            //         Anchor = Anchor.CentreRight,
            //         Origin = Anchor.CentreRight,
            //         Texture = skin?.GetTexture("menu-button-background"),
            //         Scale = new Vector2(sprite_scale),
            //     };
            // }
            // else
            // {
            //     Size = entry_size;
            // }
        }
    }
}
