using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacySkipOverlay : CompositeDrawable, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    [Resolved]
    private GameplayClockContainer gameplayClock { get; set; } = null!;

    [Resolved]
    private DrawableRuleset drawableRuleset { get; set; } = null!;

    private VisibilityContainer skipOverlayContainer = null!;

    [SettingSource("Lazer Skip Overlay Opacity", "The opacity of the skip overlay introduced in osu!lazer.")]
    public BindableFloat LazerSkipOverlayOpacity { get; private set; } = new BindableFloat(1)
    {
        MinValue = 0,
        MaxValue = 1,
        Precision = 0.01f,
        Default = 1,
    };

    // Since multiplayer's overlay displays requested players and legacy does not have that feature,
    // we can just have a separate opacity setting for it so that user can still know when someone requested a skip.
    [SettingSource("Multiplayer Skip Overlay Opacity", "The opacity of the skip overlay used in multiplayer.")]
    public BindableFloat MultiplayerSkipOverlayOpacity { get; private set; } = new BindableFloat(1)
    {
        MinValue = 0,
        MaxValue = 1,
        Precision = 0.01f,
        Default = 1,
    };

    [SettingSource("Immediately Fade Lazer Skip Overlay", "Whether to immediately fade out the lazer skip overlay even when loading asynchronously.")]
    public BindableBool ImmediatelyFadeLazerOverlay { get; private set; } = new BindableBool(false)
    {
        Default = false,
    };

    [BackgroundDependencyLoader]
    private void load(Player? player, ISkinSource? skin)
    {
        RelativeSizeAxes = Axes.Both;

        var skipOverlay = getLazerSkipOverlay(player);

        var hasAnimation = skin?.GetTexture("play-skip-0") is not null;

        skipOverlayContainer = new FadeContainer
        {
            RelativeSizeAxes = Axes.Both,
            State = { Value = Visibility.Hidden },
            Child = new LegacySkipDrawable()
            {
                SkipRequested = skipOverlay?.RequestSkip,
            }
        };

        if (hasAnimation)
        {
            // FIXME: some skins use high framerate animation with many large textures, causing performance issues.
            // We try to load this asynchronously to at least avoid blocking UI drawing.
            LoadComponentAsync(skipOverlayContainer, s =>
            {
                AddInternal(s);

                // hook up lazer's overlay later so that user can still skip when managing to load the skip overlay.
                if (!ImmediatelyFadeLazerOverlay.Value)
                    hookupSkipOverlayOpacity(static (alpha, skip) => skip.FadeTo(alpha, 300));
            });
        }
        else
        {
            AddInternal(skipOverlayContainer);
        }

        if (!hasAnimation || ImmediatelyFadeLazerOverlay.Value)
            hookupSkipOverlayOpacity(static (alpha, skip) => skip.Alpha = alpha);

        void hookupSkipOverlayOpacity(Action<float, SkipOverlay>? action = null)
        {
            if (skipOverlay is null)
                return;

            var bindable = skipOverlay.GetType() == typeof(MultiplayerSkipOverlay)
                ? MultiplayerSkipOverlayOpacity
                : LazerSkipOverlayOpacity;

            bindable.BindValueChanged(o => skipOverlay.Alpha = o.NewValue);

            action?.Invoke(bindable.Value, skipOverlay);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!skipOverlayContainer.IsLoaded)
            return;

        double fadeOutBeginTime = drawableRuleset.GameplayStartTime - MasterGameplayClockContainer.MINIMUM_SKIP_TIME;

        skipOverlayContainer.State.Value = gameplayClock.CurrentTime >= fadeOutBeginTime
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    private SkipOverlay? getLazerSkipOverlay(Player? player)
    {
        if (player is null)
            return null;

        return SkipIntroOverlay_getter?.Invoke(player, Array.Empty<object?>()) as SkipOverlay;
    }

    private static readonly MethodInfo? SkipIntroOverlay_getter = typeof(Player)
        .GetProperty("SkipIntroOverlay", BindingFlags.NonPublic | BindingFlags.Instance)?
        .GetMethod;

    private partial class FadeContainer : VisibilityContainer
    {
        protected override void PopIn()
        {
            this.FadeIn(400);
        }

        override protected void PopOut()
        {
            this.FadeOut(200);
        }
    }
}
