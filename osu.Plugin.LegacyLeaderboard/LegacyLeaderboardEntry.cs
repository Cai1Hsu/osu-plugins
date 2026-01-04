using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardEntry : CompositeDrawable
{
    internal const int WIDTH = 120;
    internal const int HEIGHT = 36;

    private bool passing = true;
    public bool Passing
    {
        get => passing;
        set
        {
            passing = value;
            if (comboSprite != null)
                playerNameSprite.FadeColour(passing ? Color4.White : new Color4(236, 39, 81, 255), 255);
        }
    }

    private string playerName = string.Empty;
    public string PlayerName
    {
        get => playerName;
        set
        {
            playerName = value;
            if (comboSprite != null)
                playerNameSprite.Text = playerName;
        }
    }

    private int score;
    public int Score
    {
        get => score;
        set
        {
            score = value;
            if (comboSprite != null)
                scoreSprite.Text = $"{score:N0}";
        }
    }

    private int combo;
    public int Combo
    {
        get => combo;
        set
        {
            combo = value;
            if (comboSprite != null)
                comboSprite.Text = $"{combo:N0}x";
        }
    }

    private int rank;
    public int Rank
    {
        get => rank;
        set
        {
            rank = value;
            if (comboSprite != null)
            {
                rankSprite.Text = rank.ToString();
            }

        }
    }

    private Sprite backgroundSprite = null!;

    private OsuSpriteText playerNameSprite = null!;
    private OsuSpriteText scoreSprite = null!;
    private OsuSpriteText comboSprite = null!;
    private OsuSpriteText rankSprite = null!;

    private const float stable_ratio = 1.6f;

    private static readonly Vector2 entry_size = new Vector2(120, 48);
    private static readonly Vector2 info_area_size = new Vector2(120, 36);

    [BackgroundDependencyLoader]
    private void load(ISkinSource skinSource)
    {
        Size = entry_size;

        var font = OsuFont.GetFont(weight: FontWeight.SemiBold);
        var rankFont = OsuFont.GetFont(weight: FontWeight.SemiBold, fixedWidth: true);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Size = entry_size,
                FillMode = FillMode.Fill,
                Masking = true,
                Child = backgroundSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Scale = new Vector2(0.62f),
                    Alpha = 150f / 255f,
                    // FIXME: this will not respect skin changes
                    Texture = skinSource.GetTexture("menu-button-background"),
                }
            },
            new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(0, 10),
                AutoSizeAxes = Axes.Both,
                RelativeChildSize = info_area_size,
                Children = new Drawable[]
                {
                    // size maintaining
                    Empty().With(e => e.Size = info_area_size),
                    new Container
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Position = new Vector2(2, 0) * stable_ratio,
                        Child = playerNameSprite = new ScoreInfoText
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Colour = new Colour4(255, 255, 255, 255),
                            Scale = new Vector2(1.3f),
                            Font = font,
                        }
                    },
                    new Container
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Position = new Vector2(2, 18) * stable_ratio,
                        Child = scoreSprite = new ScoreInfoText
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            Colour = new Colour4(255, 255, 255, 255),
                            Scale = new Vector2(0.9f),
                            Font = font,
                        }
                    },
                    comboSprite = new ScoreInfoText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Colour = new Colour4(153, 251, 255, 255),
                        Position = new Vector2(-2f, 18f) * stable_ratio,
                        Scale = new Vector2(0.9f),
                        Font = font,
                    },
                    rankSprite =  new ScoreInfoText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Colour = new Colour4(255, 255, 255, 80),
                        Position = new Vector2(-4f, 4f) * stable_ratio,
                        Font = rankFont,
                        Spacing = new Vector2(-1f, 0),
                        Scale = new Vector2(2.2f),
                    },
                }
            }
        };

        // Ensure initial values are applied.
        PlayerName = playerName;
        Score = score;
        Combo = combo;
        Rank = rank;
        Passing = passing;
    }

    private partial class ScoreInfoText : OsuSpriteText
    {
        public ScoreInfoText()
        {
            UseFullGlyphHeight = false;
            AllowMultiline = false;
        }
    }
}
