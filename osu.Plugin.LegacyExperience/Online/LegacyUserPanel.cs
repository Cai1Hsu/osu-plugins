using System.Diagnostics;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace osu.Plugin.LegacyExperience.Online;

public partial class LegacyUserPanel : CompositeDrawable
{
    public APIUser User { get; }

    public Action? Action { get; set; }

    public LegacyUserPanel(APIUser user)
    {
        User = user;
    }

    public BindableBool ExtendedStyle { get; } = new BindableBool();

    private Sprite spriteBackground = null!;
    private Sprite spriteBorder = null!;

    private OsuSpriteText rankText = null!;
    private OsuTextFlowContainer playerInfoText = null!;
    private PlayerStatusDisplay statusDisplay = null!;
    private Sprite rulesetIcon = null!;
    private LevelProgressBar levelBar = null!;

    [Resolved]
    private TextureStore textures { get; set; } = null!;

    [Resolved]
    private MetadataClient? metadata { get; set; }

    [Resolved]
    private ISkinSource? skin { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;

        var userStat = User.Statistics;

        InternalChildren =
        [
            spriteBackground = new Sprite
            {
                Texture = textures.GetAutoSized(@"UI/user-bg"),
            },
            spriteBorder = new Sprite
            {
                Texture = textures.GetAutoSized(@"UI/user-border"),
            },
            new UpdateableAvatar(User, isInteractive: false)
            {
                // Should be new Vector2(6) to accurately center the avatar, use 6.2 here to match stable's position for consistency.
                // 6.2 is calcuate as follow:
                // 1. stable applies an offset of Vector2(-4) to background and border.
                // 2. stable uses Center origin for avatar and Vector2(23) for position, relative to top left corner of the panel, which is Vector2(23 + 4) = Vector2(27).
                // 3. apply LegacyExperiencePlugin.StableRatio, resulted in Vector2(27 * 1.6) = Vector2(43.2).
                // 4. set origin to TopLeft, the final position is Vector2(43.2 - 37) = Vector2(6.2).
                Position = new Vector2(6.2f),
                Size = new Vector2(74),
            },
            rulesetIcon = new Sprite
            {
                Position = new Vector2(180f, 4f) * LegacyExperiencePlugin.StableRatio,
                Colour = Colour4.White.Opacity(70 / 255f),
            },
            rankText = new OsuSpriteText()
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopRight,
                Position = new Vector2(204, 11) * LegacyExperiencePlugin.StableRatio,
                Font = OsuFont.Default.With(size: 36 * LegacyExperiencePlugin.StableRatio),
                Shadow = false,
                Margin = new MarginPadding { Right = 4 },
            }.With(updateRankText),
            new OsuSpriteText
            {
                Position = new Vector2(52, 2) * LegacyExperiencePlugin.StableRatio,
                Text = User.Username,
                Font = OsuFont.Default.With(size: 14 * LegacyExperiencePlugin.StableRatio),
                Shadow = false,
            },
            playerInfoText = new OsuTextFlowContainer(configureText)
            {
                Masking = true,
                ParagraphSpacing = 0,
                Size = new Vector2(150, 33) * LegacyExperiencePlugin.StableRatio,
                Position = new Vector2(52, 16) * LegacyExperiencePlugin.StableRatio,
            },
            // TextFlowContainer adds line break in word wrap, we want line break occurs anywhere if needed.
            statusDisplay = new PlayerStatusDisplay
            {
                Masking = true,
                // yes, the size is different from playerInfoText in stable.
                Size = new Vector2(150, 32) * LegacyExperiencePlugin.StableRatio,
                Position = new Vector2(52, 16) * LegacyExperiencePlugin.StableRatio,
            },
            levelBar = new LevelProgressBar
            {
                Position = new Vector2(124, 66),
                // Bindable seems to be unnecessary since level info is only updated on user change.
                // However this helps for testing and also keeps the code cleaner.
                Progress = { Value = userStat.Level.Progress / 100f },
            },
        ];

        ExtendedStyle.BindValueChanged(updateStyle, true);

        updateRulesetIcon();
        FinishTransforms(true);

        updateSkin();
        skin?.SourceChanged += updateSkin;
    }

    private void updateStyle(ValueChangedEvent<bool> v)
    {
        rankText.MoveToY((v.NewValue ? 11 : 18) * LegacyExperiencePlugin.StableRatio, 200);

        if (v.NewValue)
        {
            levelBar.FadeIn(200);
            spriteBackground.Blending = BlendingParameters.Additive;
            spriteBorder.Alpha = 0;
        }
        else
        {
            levelBar.FadeOut(200);
            spriteBackground.Blending = BlendingParameters.Inherit;
            spriteBorder.Alpha = 1;
        }

        updatePlayerInfo();
        updateDisplayedInfo();
        updateAccentColour();
        updateColour();
    }

    private void updateDisplayedInfo()
    {
        var showStatus = !ExtendedStyle.Value && IsHovered;

        Drawable toHide, toShow;

        if (showStatus)
        {
            toHide = playerInfoText;
            toShow = statusDisplay;
        }
        else
        {
            toHide = statusDisplay;
            toShow = playerInfoText;
        }

        const float transition_duration = 100;

        toHide.FadeOut(transition_duration);
        toShow.FadeIn(transition_duration);
    }

    private void updatePlayerInfo()
    {
        var textBuilder = new StringBuilder();
        var userStat = User.Statistics;

        if (userStat.PP > 0)
        {
            textBuilder.AppendLine($"Performance:{userStat.PP:N0}pp");
        }
        else
        {
            var unit = string.Empty;
            float rankedScore = userStat.RankedScore;

            switch (rankedScore)
            {
                case > 1_000_000_000:
                    unit = "b";
                    rankedScore /= 1_000_000_000;
                    break;
                case > 1_000_000:
                    unit = "m";
                    rankedScore /= 1_000_000;
                    break;
                case > 1_000:
                    unit = "k";
                    rankedScore /= 1_000;
                    break;
            }

            textBuilder.Append($"Score:");

            if (rankedScore % 1 != 0)
                textBuilder.Append($"{rankedScore:#,0.0}");
            else
                textBuilder.Append($"{rankedScore:#,0}");

            textBuilder.AppendLine(unit);
        }

        textBuilder.AppendLine($"Accuracy:{userStat.Accuracy:0.00}%");

        if (ExtendedStyle.Value)
            textBuilder.Append($"Lv{userStat.Level.Current}");
        else
            textBuilder.Append($"Play Count: {userStat.PlayCount} (Lv{userStat.Level.Current:0})");

        playerInfoText.Text = textBuilder.ToString();
    }

    protected override void Update()
    {
        base.Update();

        updatePresence();
    }

    private UserStatus? lastStatus;
    private UserActivity? lastActivity;
    private int lastDataTimeMinute;

    private LegacyUserStatus? lastLegacyStatus;

    private void updatePresence()
    {
        // in lazer's code there's a comment saying "TODO: we probably don't want to do this every frame."
        // however, it didn't give a clear direction on when to update the presence. For simplicity, we can just update the presence every minute, since the presence information is not that time-sensitive and it can avoid unnecessary updates when the user is idle or offline.
        // stable communicates with bancho on a separate thread to get user presence every 20ms
        UserPresence? presence = metadata?.GetPresence(User.OnlineID);
        UserStatus status = presence?.Status ?? UserStatus.Offline;
        UserActivity? activity = presence?.Activity;
        int dataTimeMinute = DateTime.UtcNow.Minute;

        if (status == lastStatus && activity == lastActivity && dataTimeMinute == lastDataTimeMinute)
            return;

        lastStatus = status;
        lastActivity = activity;
        lastDataTimeMinute = dataTimeMinute;

        var (legacyStatus, beatmapString) = GetLegacyUserStatusAndBeatmap(status, activity);

        lastLegacyStatus = legacyStatus;

        var (timezoneOffset, countryName) = GetCountryInfo(User.CountryCode);
        var localTime = DateTime.UtcNow.AddMinutes(timezoneOffset);

        statusDisplay.LocationText.Text = $"{localTime:HH:mm} @ {countryName}";
        statusDisplay.StatusText.Text = $"{legacyStatus} {beatmapString ?? string.Empty}";

        updateAccentColour();
        updateColour();
    }

    private void updateAccentColour()
    {
        AccentColour = lastLegacyStatus switch
        {
            _ when ExtendedStyle.Value => new Colour4(1, 1, 1, 255),
            LegacyUserStatus.Afk => new Colour4(10, 10, 10, 255),
            LegacyUserStatus.Editing => new Colour4(160, 60, 60, 255),
            LegacyUserStatus.Modding => new Colour4(60, 160, 60, 255),
            LegacyUserStatus.Watching => new Colour4(60, 60, 160, 255),
            LegacyUserStatus.Testing => new Colour4(160, 60, 160, 255),
            LegacyUserStatus.Submitting => new Colour4(139, 238, 180, 255),
            LegacyUserStatus.Playing or LegacyUserStatus.Paused => new Colour4(140, 160, 160, 255),
            LegacyUserStatus.Multiplayer or LegacyUserStatus.Lobby => new Colour4(164, 108, 28, 255),
            LegacyUserStatus.Multiplaying => new Colour4(221, 190, 0, 255),
            _ => new Colour4(10, 29, 75, 255),
        };
    }

    private void updateRulesetIcon()
    {
        var playMode = User.PlayMode is "fruits" or "mania" or "taiko" or "osu" ? User.PlayMode : null;

        bool showIcon = User.Statistics.RankedScore > 0 ||
                        lastLegacyStatus is LegacyUserStatus.Playing or
                                            LegacyUserStatus.Multiplaying or
                                            LegacyUserStatus.Testing;

        if (playMode == null || !showIcon)
        {
            rulesetIcon.Hide();
        }
        else
        {
            var textureName = $"UI/mode-{playMode}-small";
            var texture = textures.GetAutoSized(textureName);

            Debug.Assert(texture is not null);

            rulesetIcon.Texture = texture;
            rulesetIcon.Size = texture.DisplaySize;

            rulesetIcon.Show();
        }
    }

    private void updateRankText(SpriteText rankText)
    {
        if (User.Statistics.GlobalRank is not { } rank)
            return;

        rankText.Colour = rank switch
        {
            > 200000 => Colour4.White.Opacity(20 / 255f),
            > 100000 => Colour4.White.Opacity(40 / 255f),
            > 50000 => Colour4.White.Opacity(60 / 255f),
            > 1000 => Colour4.White.Opacity(80 / 255f),
            > 10 => Colour4.White.Opacity(100 / 255f),
            > 1 => new Colour4(244, 218, 73, 120),
            _ => new Colour4(88, 171, 248, 120)
        };

        rankText.Text = $"#{rank}";
    }

    private Colour4 AccentColour { get; set; } = new Colour4(10, 29, 75, 255);

    private static readonly Vector4 hover_additive = new Vector4(new Vector3(40f / 255), 0);

    private void updateColour()
    {
        const float transition_duration = 200;

        var borderColour = IsHovered ? Colour4.White : AccentColour.Darken(0.2f);
        var backgroundColour = IsHovered ? new Colour4(AccentColour.Vector + hover_additive).Clamped() : AccentColour;

        spriteBorder.FadeColour(borderColour, transition_duration);
        spriteBackground.FadeColour(backgroundColour, transition_duration);
    }

    private const string hover_sample_name = "click-short";
    private const string click_sample_name = "click-short-confirm";

    private static readonly SampleInfo hover_sample_info = new SampleInfo(hover_sample_name);
    private static readonly SampleInfo click_sample_info = new SampleInfo(click_sample_name);

    private ISample? hoverSample;
    private ISample? clickSample;

    private void updateSkin()
    {
        hoverSample = skin?.GetSample(hover_sample_info);
        clickSample = skin?.GetSample(click_sample_info);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        updateColour();
        updateDisplayedInfo();
        base.OnHoverLost(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverSample?.Play();
        updateColour();
        updateDisplayedInfo();
        return base.OnHover(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        clickSample?.Play();
        Action?.Invoke();
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateSkin;
    }

    private static void configureText(SpriteText text)
    {
        text.Font = OsuFont.Default.With(size: 10 * LegacyExperiencePlugin.StableRatio);
        text.Shadow = false;
    }

    private partial class PlayerStatusDisplay : FillFlowContainer
    {
        public OsuSpriteText LocationText = null!;
        public OsuSpriteText StatusText = null!;

        public PlayerStatusDisplay()
        {
            Direction = FillDirection.Vertical;

            InternalChildren = new Drawable[]
            {
                LocationText = new OsuSpriteText().With(configureText),
                StatusText = new OsuSpriteText
                {
                    AllowMultiline = true,
                    RelativeSizeAxes = Axes.X,
                }.With(configureText),
            };
        }
    }
}
