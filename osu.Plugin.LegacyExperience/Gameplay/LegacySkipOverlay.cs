using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.OnlinePlay.Multiplayer;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacySkipOverlay : CompositeDrawable, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    [Resolved]
    private GameplayClockContainer gameplayClock { get; set; } = null!;

    [Resolved]
    private DrawableRuleset drawableRuleset { get; set; } = null!;

    [Resolved]
    private InputCountController? inputCountController { get; set; }

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

    private LegacySkipDrawable drawable = null!;

    [BackgroundDependencyLoader]
    private void load(Player player, ISkinSource? skin)
    {
        RelativeSizeAxes = Axes.Both;

        if (!player.Configuration.AllowSkipping || !drawableRuleset.AllowGameplayOverlays)
            return;

        var skipOverlay = getLazerSkipOverlay(player);

        var hasAnimation = skin?.GetTexture("play-skip-0") is not null;

        skipOverlayContainer = new FadeContainer
        {
            RelativeSizeAxes = Axes.Both,
            State = { Value = Visibility.Hidden },
            Child = drawable = new LegacySkipDrawable()
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

    private readonly IBindableList<InputTrigger> gameplayTriggers = new BindableList<InputTrigger>();

    protected override void LoadComplete()
    {
        base.LoadComplete();

        // We are not contained within a RulesetInputManager, so IKeyBindingHandler<OsuActoin> thing won't work here.
        // Also, we have to add IKeyBindingHandler implementation for each ruleset if we want to support them all.
        // So we just manually bind to InputCountController's triggers here, this is how KeyCounter works as well.
        if (inputCountController is not null)
            gameplayTriggers.BindTo(inputCountController.Triggers);

        gameplayTriggers.BindCollectionChanged((_, __) =>
        {
            foreach (var t in gameplayTriggers.OfType<InputTrigger>())
            {
                // Although smoke is also considered as a skip trigger in stable,
                // it feels bad when you just want to draw something but the game starts instead.
                if (isOsuActionSmokeTrigger(t))
                    continue;

                t.OnActivate += _ =>
                {
                    // avoid spamming skip requests when the intro is already skipped.
                    if (!skipOverlayContainer.IsPresent || isGameStarted)
                        return;

                    drawable.TriggerClick();
                };
            }
        }, true);
    }

    private static bool isOsuActionSmokeTrigger(InputTrigger trigger)
    {
        // We use manual reflection here to avoid a hard dependency on osu.Game.Rulesets.Osu.
        if (trigger.GetType() != KeyCounterActionTriggerOsuAction)
            return false;

        var action = KeyCounterActionTriggerOsuAction_Action_getter?.Invoke(trigger, Array.Empty<object?>());

        if (action is null)
            return false;

        return (int)action is 2; // OsuAction.Smoke
    }

    private const string osuActionTypeFullyQualifiedName = "osu.Game.Rulesets.Osu.OsuAction, osu.Game.Rulesets.Osu";

    private static readonly Type? KeyCounterActionTriggerOsuAction =
        Type.GetType(osuActionTypeFullyQualifiedName) is Type osuActionType
            ? typeof(KeyCounterActionTrigger<>).MakeGenericType(osuActionType)
            : null;

    private static readonly MethodInfo? KeyCounterActionTriggerOsuAction_Action_getter =
        KeyCounterActionTriggerOsuAction?.GetProperty("Action", BindingFlags.Public | BindingFlags.Instance)?
            .GetMethod;

    private bool isGameStarted => gameplayClock.CurrentTime >= drawableRuleset.GameplayStartTime;

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

    private SkipOverlay? getLazerSkipOverlay(Player player)
    {
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

        protected override void PopOut()
        {
            this.FadeOut(200);
        }
    }
}
