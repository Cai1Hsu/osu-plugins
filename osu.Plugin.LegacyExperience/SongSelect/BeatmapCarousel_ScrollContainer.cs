using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Plugin.LegacyExperience.SongSelect;

partial class BeatmapCarousel
{
    protected partial class LegacyScrollContainer : ScrollContainer
    {
        private const float default_decay = 0.996f;

        private InputManager inputManager = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager();
        }

        protected override void ScrollToAbsolutePosition(Vector2 screenSpacePosition)
        {
            ScrollToAbsolutePosition(screenSpacePosition, decay: default_decay);
        }

        public void ScrollToAbsolutePosition(Vector2 screenSpacePosition, float decay = default_decay)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(decay, 1.0f);

            float fromScrollbarPosition = FromScrollbarPosition(screenSpacePosition.Y);
            float scrollbarCentreOffset = FromScrollbarPosition(Scrollbar.DrawHeight) * 0.5f;

            var target = Clamp(fromScrollbarPosition - scrollbarCentreOffset);

            ScrollToPosition(target, decay);
        }

        protected override bool IsDragging => base.IsDragging || absoluteScrolling;

        public new bool AbsoluteScrolling => absoluteScrolling;

        private bool absoluteScrolling;
        private double scrollDistance;
        private double scrollVelocity;
        private double scrollDecay;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button is MouseButton.Right)
            {
                absoluteScrolling = true;

                bool shouldBlock = hasAnyPanelHovered()
                    ? false  // don't block, context menu requires right click to propagate.
                    : true; // prevent song select reveal

                return shouldBlock;
            }

            return base.OnMouseDown(e);
        }

        private bool hasAnyPanelHovered() 
            => inputManager.HoveredDrawables
                // TODO: may require Parent to be this.Panels
                .OfType<LegacyPanel>()
                .Any();

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button is MouseButton.Right)
            {
                absoluteScrolling = false;
                return;
            }

            base.OnMouseUp(e);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (!e.CurrentState.Mouse.IsPressed(MouseButton.Left) && absoluteScrolling)
            {
                ScrollToAbsolutePosition(e.MousePosition, 0.992f);
            }

            return base.OnMouseMove(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // allow for controlling volume when alt is held.
            // mostly for compatibility with osu-stable.
            if (e.AltPressed) return false;

            var direction = -Math.Sign(e.ScrollDelta.Y);

            scrollVelocity += direction * 0.4 * (1.0 + Math.Min(Math.Abs(scrollVelocity) / 2.0, 5.0));
            scrollDecay = 0.994;

            return true;
        }

        public void UpdateScrollPosition()
        {
            if (!IsDragged)
            {
                double velocity = scrollVelocity;
                scrollVelocity *= Math.Pow(scrollDecay, Time.Elapsed);
                scrollDistance = (velocity != scrollVelocity) ? ((scrollVelocity - velocity) / Math.Log(scrollDecay)) : velocity;
                if (Precision.AlmostEquals(scrollVelocity, 0, 0.01))
                {
                    scrollVelocity = 0.0;
                    scrollDecay = default_decay;
                }
                if (Precision.AlmostEquals(scrollDistance, 0, 0.01))
                    scrollDistance = 0;
            }

            // Catch any scroll request and manage it ourselves.
            if (Target != Current)
                ScrollToPosition(Target);

            if (DrawHeight > 0.0 && scrollDistance != 0.0)
            {
                // use ScrollTo here to ensure OsuScrollContainer don't touch Current
                ScrollTo(Current + scrollDistance, false);
            }
        }

        private void ScrollToPosition(double newPosition, double decay = default_decay)
        {
            if (decay == 0.0)
            {
                scrollVelocity = 0.0;
                ScrollTo(newPosition, false);
            }
            else
            {
                double delta = newPosition - Current;
                scrollDecay = decay;
                scrollVelocity = -delta * Math.Log(scrollDecay);
            }
        }
    }
}
