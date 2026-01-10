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
using osu.Game.Plugins.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardEntry : CompositeDrawable
{
    private const float stable_ratio = 1.6f;

    // stable uses 0.62 but McOsu and Wieku/danser-go both use 0.625 for some reason.
    // Let's align with stable for now.
    private const float background_scale = 0.62f;
    private static readonly Vector2 background_offset = new Vector2(0, 20 * background_scale);

    private OsuSpriteText nameSprite = null!;
    private LegacySpriteTextContainer scoreSprite = null!;
    private LegacySpriteTextContainer comboSprite = null!;
    private LegacySpriteTextContainer rankSprite = null!;
    private Sprite backgroundSprite = null!;

    public LegacyLeaderboardEntry(GameplayLeaderboardScore score)
    {
        User = score.User;
        IsTracking = score.Tracked;
        TotalScore.BindTo(score.TotalScore);
        Accuracy.BindTo(score.Accuracy);
        Combo.BindTo(score.Combo);
        HasQuit.BindTo(score.HasQuit);
        ScorePosition.BindTo(score.Position);
        ProviderDisplayOrder.BindTo(score.DisplayOrder);
        GetDisplayScore = score.GetDisplayScore;

        InternalChildren = new Drawable[]
        {
            backgroundSprite= new Sprite
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
                SpriteText =
                {
                    FontOverlap = 2.5f * stable_ratio,
                }
            },
            comboSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                FixedWidth = true,
                Colour = new Color4(153, 251, 255, 255),
                Position = background_offset + new Vector2(0, 18f) * stable_ratio,
                SpriteText =
                {
                    FontOverlap = 2.5f * stable_ratio,
                }
            },
            rankSprite = new ScoreEntrySpriteText()
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = background_offset + new Vector2(0, -2f) * stable_ratio,
                Colour = new Color4(255, 255, 255, 80),
                Scale = new Vector2(2.2f),
                Alpha = 0,
                SpriteText =
                {
                    FontOverlap = 3f,
                }
            }
        };
    }

    [BackgroundDependencyLoader]
    private void load(ISkinSource skin)
    {
        // background vertical offset + 18 for score/combos Y + 14 for score/combos height
        float height = 103 * background_scale; // default sprite's height is 103
        float width = 82 * stable_ratio;

        Size = new Vector2(width, height);

        Texture? getCroppedBackground()
        {
            Texture? texture = skin.GetTexture("menu-button-background");

            if (texture is null)
                return null;

            Vector2 cropAt = new Vector2(470 * texture.ScaleAdjust, 0);
            Vector2 textureSize = texture.Size;

            var cropped = texture.Crop(new RectangleF(cropAt, textureSize - cropAt));
            cropped.ScaleAdjust = texture.ScaleAdjust;

            return cropped;
        }

        backgroundSprite.Texture = getCroppedBackground();

        if (User is not null)
            nameSprite.Text = User.Username;
    }

    [Resolved]
    private OsuConfigManager config { get; set; } = null!;

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    public BindableLong TotalScore { get; } = new BindableLong();
    public BindableDouble Accuracy { get; } = new BindableDouble(1); // accuracy is not displayed in legacy leaderboard
    public BindableInt Combo { get; } = new BindableInt();
    public BindableBool HasQuit { get; } = new BindableBool();
    public Bindable<int?> ScorePosition { get; } = new Bindable<int?>();
    public Bindable<long> ProviderDisplayOrder { get; } = new Bindable<long>();

    /// <summary>
    /// The 0-based index of this entry in the leaderboard display.
    /// </summary>
    public Bindable<int> LeaderboardDisplayIndex { get; } = new Bindable<int>();
    public BindableBool VisibleInLeaderboard { get; } = new BindableBool(false);

    private Func<ScoringMode, long>? getDisplayScoreFunction;

    public Func<ScoringMode, long> GetDisplayScore
    {
        set => getDisplayScoreFunction = value;
    }

    public IUser? User { get; }

    private IBindable<ScoringMode> scoreDisplayMode = null!;

    private bool isFriend;

    public readonly bool IsTracking;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        isFriend = User != null && api.LocalUserState.Friends.Any(u => User.OnlineID == u.TargetID);

        scoreDisplayMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);
        scoreDisplayMode.BindValueChanged(v => updateScore());
        TotalScore.BindValueChanged(_ => updateScore(), true);

        HasQuit.BindValueChanged(_ => updatePanelState());
        LeaderboardDisplayIndex.BindValueChanged(_ => updatePanelState());
        ScorePosition.BindValueChanged(_ => updatePanelState(), true);

        Combo.BindValueChanged(v => comboSprite.Text = $@"{v.NewValue:N0}x", true);

        FinishTransforms(true);
    }

    #region background colors

    private static readonly Color4 quit_color = new Color4(80, 80, 80, 150);
    private static readonly Color4 first_place_color = new Color4(97, 190, 255, 150);
    private static readonly Color4 normal_color = new Color4(31, 115, 153, 150);
    private static readonly Color4 friend_color = new Color4(255, 97, 175, 180);
    private static readonly Color4 tracked_color = new Color4(250, 250, 250, 150);

    // this color exists in stable but the purpose is undetermined since the code is heavily obfuscated.
    [SuppressMessage("Style", "IDE0052", Justification = "Mimicking stable behaviour")]
    private static readonly Color4 unknown_color = new Color4(255, 69, 0, 150);

    #endregion

    private static readonly Color4 quit_name_color = new Color4(236, 39, 81, 150);

    private const float rank_fade_duration = 200;

    private const float fade_in_duration = 400;

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
        else if (ScorePosition.Value == 1)
        {
            backgroundColour = first_place_color;
        }
        else if (IsTracking)
        {
            backgroundColour = tracked_color;
        }
        else if (isFriend)
        {
            backgroundColour = friend_color;
        }

        nameSprite.FadeColour(nameColour, 150);
        backgroundSprite.FadeColour(backgroundColour, 50);
    }

    public void FlashBackground()
    {
        backgroundSprite.FlashColour(Color4.White, 200);
    }

    private void updateScore()
        => scoreSprite.Text = $"{getDisplayScoreFunction?.Invoke(scoreDisplayMode.Value) ?? TotalScore.Value:N0}";
}
