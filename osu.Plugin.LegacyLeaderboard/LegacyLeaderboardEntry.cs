using System.Diagnostics.CodeAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;
using LegacySpriteText = osu.Plugin.Legacy.LegacySpriteText;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardEntry : CompositeDrawable
{
    public const float HEIGHT = 103 * background_scale; // default sprite's height is 103
    public const float WIDTH = 82 * stable_ratio;

    private const float stable_ratio = 1.6f;

    // stable uses 0.62 but McOsu and Wieku/danser-go both use 0.625 for some reason.
    // Let's align with stable for now.
    private const float background_scale = 0.62f;
    private static readonly Vector2 background_offset = new Vector2(0, 20 * background_scale);

    private OsuSpriteText nameSprite = null!;
    private LegacySpriteText scoreSprite = null!;
    private LegacySpriteText comboSprite = null!;
    private LegacySpriteText rankSprite = null!;
    private Sprite backgroundSprite = null!;

    public LegacyLeaderboardEntry()
    {
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        Size = new Vector2(WIDTH, HEIGHT);
    }

    [BackgroundDependencyLoader]
    private void load(ISkinSource skin, OsuConfigManager config)
    {
        InternalChildren = new Drawable[]
        {
            backgroundSprite = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Scale = new Vector2(background_scale),
            },
            // TODO: use stable's font
            nameSprite = new TruncatingSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Font = new FontUsage(size: 14f, fixedWidth: false),
                Scale = new Vector2(stable_ratio),
                RelativeSizeAxes = Axes.X,
                Width = 1 / stable_ratio, // we scaled up, so we need to scale down the width
                Position = background_offset + new Vector2(2.5f, -2f) * stable_ratio,
                AllowMultiline = false,
            },
            scoreSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                FixedWidth = true,
                Position = background_offset + new Vector2(2f, 18f) * stable_ratio,
                Colour = Color4.White,
                FontOverlap = 2.5f * stable_ratio,
                TextureLookup = skin.GetTexture,
            },
            comboSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                FixedWidth = true,
                Colour = new Color4(153, 251, 255, 255),
                Position = background_offset + new Vector2(0, 18f) * stable_ratio,
                FontOverlap = 2.5f * stable_ratio,
                TextureLookup = skin.GetTexture,
            },
            rankSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = background_offset + new Vector2(0, -2f) * stable_ratio,
                Colour = new Color4(255, 255, 255, 80),
                Scale = new Vector2(2.2f),
                Alpha = 0,
                FontOverlap = 3f,
                TextureLookup = skin.GetTexture,
            }
        };

        Texture? getCroppedBackground()
        {
            Texture? texture = skin.GetTexture("menu-button-background");

            if (texture is null)
                return null;

            Vector2 cropAt = new Vector2(470, 0) * texture.ScaleAdjust;
            Vector2 textureSize = texture.Size;

            if (cropAt.X >= textureSize.X)
                return texture;

            var cropped = texture.Crop(new RectangleF(cropAt, textureSize - cropAt));
            cropped.ScaleAdjust = texture.ScaleAdjust;

            return cropped;
        }

        backgroundSprite.Texture = getCroppedBackground();

        scoreDisplayMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);

        scoreDisplayMode.BindValueChanged(v => updateScore());
        TotalScore.BindValueChanged(_ => updateScore());

        HasQuit.BindValueChanged(_ => updatePanelState());
        ScorePosition.BindValueChanged(_ => updatePanelState());

        Combo.BindValueChanged(v => comboSprite.Text = $@"{v.NewValue:N0}x");
    }

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    public BindableLong TotalScore { get; } = new BindableLong();
    public BindableDouble Accuracy { get; } = new BindableDouble(1); // accuracy is not displayed in legacy leaderboard
    public BindableInt Combo { get; } = new BindableInt();
    public BindableBool HasQuit { get; } = new BindableBool();
    public Bindable<int?> ScorePosition { get; } = new Bindable<int?>();
    public Bindable<long> ProviderDisplayOrder { get; } = new Bindable<long>();

    private Func<ScoringMode, long>? getDisplayScoreFunction;

    public Func<ScoringMode, long> GetDisplayScore
    {
        set => getDisplayScoreFunction = value;
    }

    public IUser? User { get; set; }

    private IBindable<ScoringMode> scoreDisplayMode = null!;

    private bool isFriend;

    public bool IsTracking { get; set; }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        FinishTransforms(true);
    }

    #region background colors

    private static readonly Color4 quit_color = new Color4(80, 80, 80, 150);
    private static readonly Color4 first_place_color = new Color4(97, 190, 255, 150);
    private static readonly Color4 normal_color = new Color4(31, 115, 153, 150);
    private static readonly Color4 friend_color = new Color4(255, 97, 175, 180);
    private static readonly Color4 tracked_color = new Color4(250, 250, 250, 100);

    // this color exists in stable but the purpose is undetermined since the code is heavily obfuscated.
    [SuppressMessage("Style", "IDE0052", Justification = "Mimicking stable behaviour")]
    private static readonly Color4 unknown_color = new Color4(255, 69, 0, 150);

    #endregion

    private static readonly Color4 quit_name_color = new Color4(236, 39, 81, 150);

    private const float rank_fade_duration = 200;

    private void updatePanelState()
    {
        rankSprite.Text = ScorePosition.Value.HasValue ? $"{ScorePosition.Value.Value}" : string.Empty;

        if (ScorePosition.Value.HasValue)
            rankSprite.FadeIn(rank_fade_duration);
        else
            rankSprite.FadeOut(rank_fade_duration);

        Color4 nameColour = Color4.White;
        Color4 backgroundColour = normal_color;

        if (HasQuit.Value)
        {
            nameColour = quit_name_color;
            backgroundColour = quit_color;
        }
        if (IsTracking)
        {
            backgroundColour = tracked_color;
        }
        else if (ScorePosition.Value == 1)
        {
            backgroundColour = first_place_color;
        }
        else if (isFriend)
        {
            backgroundColour = friend_color;
        }

        nameSprite.FadeColour(nameColour, 150);
        backgroundSprite.FadeColour(backgroundColour, 50);
    }

    public void UpdatePanelState()
    {
        isFriend = User != null && api.LocalUserState.Friends.Any(u => User.OnlineID == u.TargetID);

        updateScore();
        Combo.TriggerChange();
        updatePanelState();

        nameSprite.Text = User?.Username ?? string.Empty;
    }

    public void FlashBackground()
    {
        backgroundSprite.FlashColour(Color4.White, 200);
    }

    private void updateScore()
        => scoreSprite.Text = $"{getDisplayScoreFunction?.Invoke(scoreDisplayMode.Value) ?? TotalScore.Value:N0}";
}
