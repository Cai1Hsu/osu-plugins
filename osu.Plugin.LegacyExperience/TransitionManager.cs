using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Plugin.LegacyExperience;

public partial class TransitionManager : CompositeDrawable
{
    private Box transitionBox = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            transitionBox = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.Black,
                Alpha = 0,
            },
        };
    }

    private enum FadeState
    {
        Idle,
        FadingOut,   // darkening: fadeLevel 0 → 100
        FadingIn,    // brightening: fadeLevel 100 → 0
    }

    private FadeState fadeState = FadeState.Idle;
    private double fadeLevel;
    private double currentFadeInRate;
    private double currentFadeOutRate;
    private Action? pendingAction;

    protected override void Update()
    {
        base.Update();

        if (fadeState == FadeState.Idle)
            return;

        double frameRatio = Time.Elapsed / (1000.0 / 60.0);

        switch (fadeState)
        {
            case FadeState.FadingOut:
                fadeLevel = Math.Min(100.0, fadeLevel + currentFadeInRate * frameRatio);

                if (fadeLevel >= 100.0)
                {
                    fadeState = FadeState.FadingIn;
                    pendingAction?.Invoke();
                    pendingAction = null;
                }

                break;

            case FadeState.FadingIn:
                fadeLevel = Math.Max(-1.0, fadeLevel - currentFadeOutRate * frameRatio);

                if (fadeLevel <= 0.0)
                {
                    fadeLevel = 0;
                    fadeState = FadeState.Idle;
                }

                break;
        }

        transitionBox.Alpha = Math.Clamp((float)fadeLevel, 0f, 100f) / 100f;
    }

    public void PlayTransition(Action action, double fadeOutRate = 10.0, double fadeInRate = 4)
    {
        pendingAction = action;
        currentFadeInRate = fadeInRate;
        currentFadeOutRate = fadeOutRate;

        if (fadeLevel >= 100.0)
        {
            // already fully dark — invoke immediately and begin brightening
            fadeState = FadeState.FadingIn;
            pendingAction?.Invoke();
            pendingAction = null;
        }
        else
        {
            fadeState = FadeState.FadingOut;
        }
    }
}
