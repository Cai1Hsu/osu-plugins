using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyOverlayButton : LegacySpriteToggleButton
{
    protected readonly Bindable<Visibility> OverlayVisibility = new Bindable<Visibility>();
    public readonly BindableBool KeepShown = new BindableBool();
    private readonly IBindable<LocalUserPlayingState> userPlayingState = new Bindable<LocalUserPlayingState>();

    public override void Hide() => this.FadeOut(FadeDuration);

    public override void Show() => this.FadeIn(FadeDuration);

    [BackgroundDependencyLoader]
    private void load(ILocalUserPlayInfo? localUserInfo)
    {
        OverlayVisibility.BindValueChanged(v =>
        {
            State.Value = v.NewValue is Visibility.Visible;

            if (v.NewValue is Visibility.Visible)
                Show();
            else if (!KeepShown.Value)
                Hide();
        });

        KeepShown.BindValueChanged(v =>
        {
            if (v.NewValue)
                Show();
            else if (OverlayVisibility.Value is Visibility.Hidden)
                Hide();
        });

        State.BindValueChanged(v =>
        {
            OverlayVisibility.Value = v.NewValue ? Visibility.Visible : Visibility.Hidden;
        });

        if (localUserInfo is not null)
            userPlayingState.BindTo(localUserInfo.PlayingState);

        userPlayingState.BindValueChanged(state =>
        {
            KeepShown.Value = state.NewValue is not LocalUserPlayingState.Playing;
        }, true);
    }
}
