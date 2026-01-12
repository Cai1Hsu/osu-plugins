using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using static osu.Plugin.Legacy.Buttons.LegacyPlaybackRateButton;

namespace osu.Plugin.Legacy.Buttons;

public partial class LegacyPlaybackRateButton : LegacySpriteStatedButton<LegacyPlaybackRate>, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    protected override LegacyPlaybackRate GetNewState(LegacyPlaybackRate currentState) => allowPlaybackRateControl ?
        currentState switch
        {
            LegacyPlaybackRate.Normal => LegacyPlaybackRate.Double,
            LegacyPlaybackRate.Double => LegacyPlaybackRate.Half,
            LegacyPlaybackRate.Half => LegacyPlaybackRate.Normal,
            LegacyPlaybackRate.Other => LegacyPlaybackRate.Normal,
            _ => throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null)
        } : currentState;

    protected override string? GetTextureNameForState(LegacyPlaybackRate state) => state switch
    {
        LegacyPlaybackRate.Half => "UI/overlay-half",
        LegacyPlaybackRate.Normal => "UI/overlay-1x",
        LegacyPlaybackRate.Double => "UI/overlay-2x",
        LegacyPlaybackRate.Other => "UI/overlay-1x", // we use 1x's texture and a color to indicate the next speed is 1x
        _ => throw new InvalidOperationException($"Unhandled {nameof(LegacyPlaybackRate)} value: {state}"),
    };

    public LegacyPlaybackRateButton()
    {
        Anchor = Anchor.CentreRight;
        Origin = Anchor.CentreRight;
    }

    private bool allowPlaybackRateControl = false;

    public readonly Bindable<double> UserPlaybackRate = new BindableDouble(1);

    [BackgroundDependencyLoader]
    private void load(Player? player, GameplayClockContainer? gameplayClockContainer)
    {
        State.BindValueChanged(v =>
        {
            NormalColour = (v.NewValue is LegacyPlaybackRate.Other || !allowPlaybackRateControl)
                ? Colour4.DarkGray
                : Colour4.White;
            Sprite.FadeColour(NormalColour, FadeDuration);
        }, true);

        if (player is ReplayPlayer replayPlayer &&
            gameplayClockContainer is MasterGameplayClockContainer master)
        {
            UserPlaybackRate.BindTo(master.UserPlaybackRate);
            allowPlaybackRateControl = true;
        }

        UserPlaybackRate.BindValueChanged(rate => State.Value = ParseLegacyRate(rate.NewValue), true);
        State.BindValueChanged(r => UserPlaybackRate.Value = r.NewValue switch
        {
            LegacyPlaybackRate.Half => 0.5,
            LegacyPlaybackRate.Normal => 1.0,
            LegacyPlaybackRate.Double => 2.0,
            LegacyPlaybackRate.Other => UserPlaybackRate.Value, // keep current rate
            _ => throw new ArgumentOutOfRangeException(),
        });
    }

    public static LegacyPlaybackRate ParseLegacyRate(double speed)
    {
        // the UI control's precision is 0.01
        const double epsilon = 0.01;

        if (Precision.AlmostEquals(speed, 0.5, epsilon))
            return LegacyPlaybackRate.Half;

        if (Precision.AlmostEquals(speed, 1.0, epsilon))
            return LegacyPlaybackRate.Normal;

        if (Precision.AlmostEquals(speed, 2.0, epsilon))
            return LegacyPlaybackRate.Double;

        return LegacyPlaybackRate.Other;
    }

    public enum LegacyPlaybackRate
    {
        Half,
        Normal,
        Double,
        Other, // lazer supports arbitrary speeds, so we need an extra state here.
    }
}
