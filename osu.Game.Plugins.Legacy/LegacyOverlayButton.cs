using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyOverlayButton : LegacySpriteToggleButton
{
    protected readonly Bindable<Visibility> OverlayVisibility = new Bindable<Visibility>();
    public readonly BindableBool KeepShown = new BindableBool(true);

    [BackgroundDependencyLoader]
    private void load()
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
            {
                if (OverlayVisibility.Value is Visibility.Visible)
                    Show();
            }
            else
            {
                if (OverlayVisibility.Value is Visibility.Hidden)
                    Hide();
            }
        });

        State.BindValueChanged(v =>
        {
            if (v.NewValue)
                OverlayVisibility.Value = Visibility.Visible;
            else
                OverlayVisibility.Value = Visibility.Hidden;
        });
    }
}
