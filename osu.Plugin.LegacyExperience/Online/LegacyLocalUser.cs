using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Plugins;
using osu.Game.Skinning;
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

    public event Action UserUpdated = null!;

    private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

    [Resolved]
    private IAPIProvider api { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(UserStatisticsWatcher? userStatisticsWatcher)
    {
        // user-bg's size
        Size = new Vector2(330, 86) * LegacyExperiencePlugin.StableRatio;

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

            userPanel?.FadeOut(200).Expire();
            AddInternal(userPanel = new LegacyUserPanel(user.NewValue)
            {
                ExtendedStyle = { Value = true },
            });

            userPanel.FadeInFromZero(200);

            // ensure the user panel is always at the back of the hierarchy so that it doesn't cover any other elements.
            ChangeInternalChildDepth(userPanel, float.MaxValue);

            UserUpdated?.Invoke();
        }, true);

        LatestUpdate.BindValueChanged(update =>
        {
            if (update.NewValue is null)
                return;

            var change = update.NewValue;
            var prev = change.Before;
            var curr = change.After;

            Scheduler.AddOnce(userPanel.UpdateStatistics, curr);

            Scheduler.Add(() => userPanel.InvokeWhenReady(_ =>
            {
                // note that PlayerInfoText has a constant size to truncate the text,
                // InnerFlow is the actual text container that moves when the text changes, 
                // so we need to use its position for the animation.
                var playInfoText = userPanel.PlayerInfoText.InnerFlow;

                var textPosition = userPanel.PlayerInfoText.Position + new Vector2(playInfoText.DrawWidth / 2, 0);
                var textMovement = new Vector2(playInfoText.DrawWidth / 2 + 2, 0);

                var scoreChanged = curr.RankedScore - prev.RankedScore;

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

                var accChanged = curr.Accuracy - prev.Accuracy;

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
                var rankChanged = prev.GlobalRank - curr.GlobalRank;

                if (rankChanged.HasValue && rankChanged != 0)
                {
                    var text = new FontText
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopRight,
                        Text = wrapNumericString($"{rankChanged}", rankChanged > 0),
                        Font = LegacyFont.Default.With(size: 30),
                        Colour = Colour4.White,
                        Position = userPanel.RankText.Position,
                    };

                    AddInternal(text);

                    text.MoveToOffset(new Vector2(0, -19f) * LegacyExperiencePlugin.StableRatio, 1000, Easing.Out)
                        .FadeOut(4000)
                        .Expire();
                }
            }));
        }, true);
    }

    private static string wrapNumericString(string text, bool positive)
    {
        if (positive)
            return $"+{text}";

        return text;
    }
}
