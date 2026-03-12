using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Utils;
using static osu.Game.Screens.Play.HUD.InputTrigger;
using OsuKeyTrigger = osu.Game.Screens.Play.HUD.KeyCounterActionTrigger<osu.Game.Rulesets.Osu.OsuAction>;

namespace osu.Plugin.Trainer;

/// <summary>
/// Base class for input training overlays that track key presses during osu! standard gameplay
/// and provide visual feedback indicating the expected next action.
/// </summary>
/// <remarks>
/// Subclasses implement <see cref="GetExpectedAction"/> to define the training rule.
/// The trainer shows a side flash (left or right) corresponding to the expected next key.
/// </remarks>
public abstract partial class InputTrainer : CompositeDrawable
{
    private const int box_width = 200;
    private const double flash_fade_in = 65;
    private const double flash_fade_out = 500;

    private Box leftFlash = null!;
    private Box rightFlash = null!;

    private PeriodTracker nonGameplayPeriods = null!;

    [SettingSource("Flash transparency", "The opacity of the flash indicating the expected next key.")]
    public BindableFloat FlashTransparency { get; } = new BindableFloat(1)
    {
        MinValue = 0,
        MaxValue = 1,
        Precision = 0.01f,
    };

    [SettingSource("Flash colour", "The colour of the flash indicating the expected next key.")]
    public BindableColour4 FlashColour { get; } = new BindableColour4(new Colour4(66, 187, 255, 255));

    [SettingSource("Flash spacing", "The horizontal spacing between the flash and the playfield.")]
    public BindableFloat FlashSpacing { get; } = new BindableFloat(1)
    {
        MinValue = 0,
        MaxValue = 2,
        Precision = 0.01f,
    };

    [Resolved]
    private GameplayClockContainer gameplayClock { get; set; } = null!;

    /// <summary>
    /// The last accepted (left/right) action during gameplay.
    /// Reset to <c>null</c> during non-gameplay periods and rewinds.
    /// </summary>
    protected OsuAction? LastAcceptedAction { get; private set; }

    /// <summary>
    /// Determines the expected next action based on the current training rule.
    /// </summary>
    /// <returns>The expected <see cref="OsuAction"/>, or <c>null</c> if any action is acceptable.</returns>
    protected abstract OsuAction? GetExpectedAction();

    [Resolved]
    private DrawableRuleset drawableRuleset { get; set; } = null!;

    private DrawableOsuRuleset drawableOsuRuleset => (DrawableOsuRuleset)drawableRuleset;

    public InputTrainer()
    {
        RelativeSizeAxes = Axes.Both;
        AlwaysPresent = true;
    }

    [BackgroundDependencyLoader]
    private void load(InputCountController inputCountController)
    {
        if (drawableRuleset is not DrawableOsuRuleset)
            return;

        initializePeriods();
        initializeVisuals();

        registerGameplayActionTriggers(inputCountController);

        gameplayClock.OnSeek += onRewind;

        FlashTransparency.BindValueChanged(t => flashContainer.Alpha = t.NewValue, true);

        FlashColour.BindValueChanged(c =>
        {
            // taken from MenuSideFlashes, as well as the default value of FlashColour.
            var gradientDark = c.NewValue.Opacity(0).ToLinear();
            var gradientLight = c.NewValue.Opacity(0.6f).ToLinear();

            leftFlash.Colour = ColourInfo.GradientHorizontal(gradientLight, gradientDark);
            rightFlash.Colour = ColourInfo.GradientHorizontal(gradientDark, gradientLight);
        }, true);

        FlashSpacing.BindValueChanged(s => flashContainer.Width = s.NewValue, true);
    }

    protected virtual bool ShouldFlash => true;

    private void initializePeriods()
    {
        var periods = new List<Period>();
        var objects = drawableRuleset.Objects.ToList();

        if (objects.Count > 0)
        {
            periods.Add(new Period(int.MinValue, getValidJudgementTime(objects[0]) - 1));

            foreach (BreakPeriod b in drawableOsuRuleset.Beatmap.Breaks)
            {
                var firstAfterBreak = objects.FirstOrDefault(h => h.StartTime >= b.EndTime);

                if (firstAfterBreak != null)
                    periods.Add(new Period(b.StartTime, getValidJudgementTime(firstAfterBreak) - 1));
            }

            static double getValidJudgementTime(HitObject hitObject)
                => hitObject.StartTime - hitObject.HitWindows.WindowFor(HitResult.Meh);
        }

        nonGameplayPeriods = new PeriodTracker(periods);
    }

    private Container flashContainer = null!;

    private void initializeVisuals()
    {
        InternalChildren = new Drawable[]
        {
            flashContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    leftFlash = new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.Y,
                        Width = box_width,
                        Height = 1.5f,
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                    },
                    rightFlash = new Box
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        RelativeSizeAxes = Axes.Y,
                        Width = box_width,
                        Height = 1.5f,
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                    },
                }
            }
        };
    }

    protected override void Update()
    {
        base.Update();

        if (gameplayClock is null || nonGameplayPeriods is null)
            return;

        if (nonGameplayPeriods.IsInAny(gameplayClock.CurrentTime))
            LastAcceptedAction = null;
    }

    private void onRewind()
    {
        LastAcceptedAction = null;
        fadeOutBox(leftFlash);
        fadeOutBox(rightFlash);
    }

    private void flashBox(Box box)
    {
        box.FadeTo(0.8f, flash_fade_in);
    }

    private void fadeOutBox(Box box)
    {
        box.FadeOut(flash_fade_out, Easing.OutQuint);
    }

    private void flashExpectedAction()
    {
        var expected = GetExpectedAction();

        if (expected == null)
        {
            fadeOutBox(leftFlash);
            fadeOutBox(rightFlash);
            return;
        }

        var (correct, theOther) = (leftFlash, rightFlash);

        if (expected is OsuAction.RightButton)
            (correct, theOther) = (theOther, correct);

        if (ShouldFlash)
        {
            flashBox(correct);
            fadeOutBox(theOther);
        }
        else
        {
            fadeOutBox(correct);
            fadeOutBox(theOther);
        }
    }

    private readonly IBindableList<InputTrigger> gameplayTriggers = new BindableList<InputTrigger>();
    private readonly Dictionary<OsuKeyTrigger, OnActivateCallback> triggerHandlers = new();

    private void registerGameplayActionTriggers(InputCountController? inputCountController)
    {
        if (inputCountController is not null)
            gameplayTriggers.BindTo(inputCountController.Triggers);

        gameplayTriggers.BindCollectionChanged((_, arg) =>
        {
            var oldTriggers = arg.OldItems?.OfType<OsuKeyTrigger>() ?? Array.Empty<OsuKeyTrigger>();
            var newTriggers = arg.NewItems?.OfType<OsuKeyTrigger>() ?? Array.Empty<OsuKeyTrigger>();

            OnActivateCallback activateHandler(OsuKeyTrigger trigger)
                => _ => handleAction(trigger.Action);

            foreach (var t in oldTriggers.Where(t => triggerHandlers.ContainsKey(t)))
            {
                t.OnActivate -= triggerHandlers[t];
                triggerHandlers.Remove(t);
            }

            foreach (var t in newTriggers)
                t.OnActivate += triggerHandlers[t] = activateHandler(t);
        }, true);
    }

    private void handleAction(OsuAction action)
    {
        // The time when the first note is hit may be considered non-gameplay, this is OsuAlternateMod's bug. 
        // But since we are training for alternate mod, let's respect this "bug" so that indicators are always consistent with the mod's behavior.
        if (gameplayClock != null && nonGameplayPeriods.IsInAny(gameplayClock.CurrentTime))
            return;

        switch (action)
        {
            case OsuAction.LeftButton:
            case OsuAction.RightButton:
                break;

            default:
                return;
        }

        LastAcceptedAction = action;
        flashExpectedAction();
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        foreach (var (trigger, handler) in triggerHandlers)
            trigger.OnActivate -= handler;

        if (gameplayClock is not null)
            gameplayClock.OnSeek -= onRewind;
    }
}
