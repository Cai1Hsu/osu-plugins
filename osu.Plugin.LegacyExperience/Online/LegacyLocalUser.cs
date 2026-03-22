using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Plugins;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Plugin.LegacyExperience.Graphics;
using osuTK;
using LegacyFont = osu.Plugin.LegacyExperience.Graphics.LegacyFont;

namespace osu.Plugin.LegacyExperience.Online;

public partial class LegacyLocalUser : CompositeDrawable, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    public Bindable<ScoreBasedUserStatisticsUpdate?> LatestUpdate { get; } = new Bindable<ScoreBasedUserStatisticsUpdate?>();

    private LegacyUserPanel userPanel = null!;

    public LegacyUserPanel UserPanel => userPanel;

    public event Action? UserUpdated;

    private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    [Resolved]
    private IBindable<RulesetInfo> ruleset { get; set; } = null!;

    [Resolved]
    private LocalUserStatisticsProvider? localUserStatisticsProvider { get; set; } = null!;

    internal readonly Bindable<UserStatistics> userStatistics = new Bindable<UserStatistics>();

    [BackgroundDependencyLoader]
    private void load(UserStatisticsWatcher? userStatisticsWatcher)
    {
        // user-bg's size
        Size = new Vector2(330, 86);

        localUser.BindTo(api.LocalUser);

        if (userStatisticsWatcher is not null)
            ((IBindable<ScoreBasedUserStatisticsUpdate?>)LatestUpdate).BindTo(userStatisticsWatcher.LatestUpdate);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        localUser.BindValueChanged(user =>
        {
            if (user.NewValue is null)
                return;

            // we have to schedule to avoid ruleset's event overwrite old statistics when user is changed.
            Scheduler.AddOnce(user =>
            {
                userPanel?.FadeOut(200).Expire();

                var newPanel = userPanel = new LegacyUserPanel(user)
                {
                    ExtendedStyle = { Value = true },
                };

                AddInternal(newPanel);

                newPanel.FadeInFromZero(200);

                // ensure the user panel is always at the back of the hierarchy so that it doesn't cover any other elements.
                ChangeInternalChildDepth(newPanel, float.MaxValue);

                if (newPanel.IsGuest)
                {
                    userStatistics.Value = null!;
                }
                else
                {
                    var ruleset = this.ruleset.Value;

                    // don't know why this value is still sometimes wrong, but this is the best we can do.
                    localUserStatisticsProvider?.RefetchStatistics(ruleset, v =>
                    {
                        updateRulesetStatistics(ruleset);
                    });
                }

                UserUpdated?.Invoke();
            }, user.NewValue);
        }, true);

        ruleset.BindValueChanged(r =>
        {
            if (!r.NewValue.IsLegacyRuleset())
                return;

            updateRulesetStatistics(r.NewValue);
        }, true);

        LatestUpdate.BindValueChanged(u =>
        {
            if (u.NewValue is not { } update)
                return;

            if (update.Score.Ruleset != ruleset.Value)
                return;

            userStatistics.Value = update.After;
        }, true);

        userStatistics.BindValueChanged(v =>
        {
            playUpdateAnimation(v.OldValue, v.NewValue);
        });
    }

    private void updateRulesetStatistics(RulesetInfo ruleset)
    {
        var stats = localUserStatisticsProvider?.GetStatisticsFor(ruleset) ?? new();

        userStatistics.Value = stats;
    }

    private void playUpdateAnimation(UserStatistics previous, UserStatistics current)
    {
        Scheduler.AddOnce(s => userPanel.InvokeWhenReady(d =>
        {
            var userPanel = (LegacyUserPanel)d;

            userPanel.UpdateStatistics(s ?? new());

            // if previous is null, it means this is the first time we set the statistics, so we shouldn't play any animation.
            if (previous is null || current is null)
                return;

            // note that PlayerInfoText has a constant size to truncate the text,
            // InnerFlow is the actual text container that moves when the text changes, 
            // so we need to use its position for the animation.
            var playInfoText = userPanel.PlayerInfoText.InnerFlow;

            var flowPosition = ToLocalSpace(playInfoText.ScreenSpaceDrawQuad.TopLeft);

            var textPosition = flowPosition + new Vector2(playInfoText.DrawWidth / 2, 0);
            var textMovement = new Vector2(playInfoText.DrawWidth / 2 + 2, 0);

            var scoreChanged = current.RankedScore - previous.RankedScore;

            if (scoreChanged != 0)
            {
                var text = new FontText
                {
                    Text = wrapNumericString($"{scoreChanged:0,0}", scoreChanged > 0),
                    Font = LegacyFont.Default.With(size: 10),
                    Colour = Colour4.YellowGreen,
                    Position = textPosition,
                };

                AddInternal(text);

                text.MoveToOffset(textMovement, 1000, Easing.Out)
                    .FadeOut(6000)
                    .Expire();
            }

            var accChanged = current.Accuracy - previous.Accuracy;

            if (accChanged != 0)
            {
                var text = new FontText
                {
                    Text = wrapNumericString($"{accChanged:0.##}%", accChanged > 0),
                    Font = LegacyFont.Default.With(size: 10),
                    Colour = accChanged > 0 ? Colour4.YellowGreen : Colour4.OrangeRed,
                    Position = textPosition,
                };

                AddInternal(text);

                text.Y += text.DrawHeight; // wrap line

                text.MoveToOffset(textMovement, 1000, Easing.Out)
                    .FadeOut(6000)
                    .Expire();
            }

            // stable uses +XXX for improvements and -XXX for drops, so we follow the same convention here.
            var rankChanged = previous.GlobalRank - current.GlobalRank;

            if (rankChanged.HasValue && rankChanged != 0)
            {
                var text = new FontText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopRight,
                    Text = wrapNumericString($"{rankChanged}", rankChanged > 0),
                    Font = LegacyFont.Default.With(size: 30),
                    Colour = Colour4.White,
                    Position = ToLocalSpace(userPanel.RankText.ScreenSpaceDrawQuad.TopRight),
                };

                AddInternal(text);

                text.MoveToOffset(new Vector2(0, -19f) * LegacyExperiencePlugin.StableRatio, 1000, Easing.Out)
                    .FadeOut(4000)
                    .Expire();
            }
        }), current);
    }

    private static string wrapNumericString(string text, bool positive)
    {
        if (positive)
            return $"+{text}";

        return text;
    }
}
