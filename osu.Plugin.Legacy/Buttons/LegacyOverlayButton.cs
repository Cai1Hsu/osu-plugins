using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play;

namespace osu.Plugin.Legacy.Buttons;

public abstract partial class LegacyOverlayButton : LegacySpriteStatedButton<Visibility>
{
    protected readonly Bindable<Visibility> OverlayVisibility = new Bindable<Visibility>();
    public readonly BindableBool KeepShown = new BindableBool();
    private readonly IBindable<LocalUserPlayingState> userPlayingState = new Bindable<LocalUserPlayingState>();

    public override void Hide() => this.FadeOut(FadeDuration);

    public override void Show() => this.FadeIn(FadeDuration);

    [BackgroundDependencyLoader]
    private void load(ILocalUserPlayInfo? localUserInfo)
    {
        // don't use BindTo so that we know who triggered the change.
        OverlayVisibility.BindValueChanged(v =>
        {
            State.Value = v.NewValue;

            if (v.NewValue is Visibility.Visible)
                Show();
            else if (!KeepShown.Value)
                Hide();
        });

        State.BindValueChanged(v => OverlayVisibility.Value = v.NewValue);

        if (localUserInfo is not null)
            userPlayingState.BindTo(localUserInfo.PlayingState);

        userPlayingState.BindValueChanged(state =>
        {
            KeepShown.Value = state.NewValue is not LocalUserPlayingState.Playing;
        }, true);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // OverlayVisibility is usually bound in derived classes' load methods.
        // Wait until bound finished so that OverlayVisibility has correct initial value.
        KeepShown.BindValueChanged(v =>
        {
            if (v.NewValue)
                Show();
            else if (OverlayVisibility.Value is Visibility.Hidden)
                Hide();
        }, true);
    }

    protected override Visibility GetNewState(Visibility currentState)
        => (Visibility)(((int)currentState + 1) % 2);

    public string? TextureVisible { get; set; }
    public string? TextureHidden { get; set; }

    protected override string? GetTextureNameForState(Visibility visibility) => visibility switch
    {
        Visibility.Visible => TextureVisible,
        Visibility.Hidden => TextureHidden,
        _ => throw new InvalidOperationException($"Unhandled {nameof(Visibility)} value: {visibility}"),
    };
}
