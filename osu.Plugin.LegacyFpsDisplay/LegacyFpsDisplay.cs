// This file is adapted from osu!lazer's FpsCounter.
// Original file: https://github.com/ppy/osu/blob/master/osu.Game/Graphics/UserInterface/FPSCounter.cs

using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using LegacySpriteText = osu.Game.Plugins.Legacy.LegacySpriteText;

namespace osu.Plugin.LegacyFpsDisplay;

public partial class LegacyFpsDisplay : CompositeDrawable, ISerialisableDrawable
{
    // private readonly static Color4 color_holyfuck = new Color4(255, 36, 0, 255);
    private readonly static Color4 color_danger = new Color4(255, 149, 24, 255);
    private readonly static Color4 color_warning = new Color4(255, 204, 34, 255);
    private readonly static Color4 color_okay = new Color4(172, 220, 25, 255);

    [Resolved]
    private GameHost host { get; set; } = null!;

    public bool UsesFixedAnchor { get; set; } = true;

    private FpsLargeSpriteText fpsText = null!;
    private FpsSmallSpriteText targetRefreshRateText = null!;
    private FpsLargeSpriteText frameTimeText = null!;

    private Sprite fpsBackground = null!;
    private Sprite frameTimeBackground = null!;

    public LegacyFpsDisplay()
    {
        Anchor = Anchor.BottomRight;
        Origin = Anchor.BottomRight;

        Margin = new MarginPadding()
        {
            Vertical = 5,
            Horizontal = 8,
        };
    }

    [BackgroundDependencyLoader]
    private void load(ISkinSource skin, FrameworkConfigManager? config)
    {
        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Name = "fps display",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(65, 20),
                        Children = new Drawable[]
                        {
                            fpsBackground = new Sprite
                            {
                                Name = "background",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Texture = skin.GetTexture("fps-box"),
                                Colour = color_okay,
                            },
                            new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.X,
                                Height = 11,
                                X = -7,
                                Child = fpsText = new FpsLargeSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Text = "999",
                                },
                            },
                            new Container
                            {
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
                                AutoSizeAxes = Axes.X,
                                Height = 10,
                                Child = targetRefreshRateText = new FpsSmallSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Text = "/240f",
                                },
                            },
                        }
                    },
                    new Container
                    {
                        Name = "Frame time display",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(65, 20),
                        Children = new Drawable[]
                        {
                            frameTimeBackground =new Sprite
                            {
                                Name = "background",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Texture = skin.GetTexture("fps-box"),
                                Colour = color_okay,
                            },
                            new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                AutoSizeAxes = Axes.X,
                                Height = 11,
                                Child = frameTimeText = new FpsLargeSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Text = "16ms",
                                }
                            }
                        }
                    }
                }
            }
        };

        drawClock = host.DrawThread.Clock;

        frameSyncMode = config?.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
        frameSyncMode?.BindValueChanged(_ => updateTargetRefreshRate());

        var window = host.Window;

        window.CurrentDisplayBindable.BindValueChanged(_ => updateTargetRefreshRate());
        window.CurrentDisplayMode.BindValueChanged(_ => updateTargetRefreshRate(), true);
    }

    private IBindable<FrameSync>? frameSyncMode;

    private ThrottledFrameClock drawClock = null!;

    private volatile float targetRefreshRate = 60;

    private void updateTargetRefreshRate()
    {
        var displayRefreshRate = host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60;

        // Don't use clock.MaximumUpdateHz as it's somtimes unreliable
        int multipler =  (frameSyncMode?.Value) switch
        {
            FrameSync.Limit2x => 2,
            FrameSync.Limit4x => 4,
            FrameSync.Limit8x => 8,
            _ => 1,
        };

        targetRefreshRate = MathF.Min(999, displayRefreshRate * multipler);

        targetRefreshRateText.Text = $"/{(int)MathF.Round(targetRefreshRate)}h";
    }

    private float currentAimFps = 999;
    private bool updateAimFPS()
    {
        const float max_display_fps = 999;

        float previousAimFps = currentAimFps;
        currentAimFps = (float)Math.Min(drawClock.MaximumUpdateHz, max_display_fps);

        return !Precision.AlmostEquals(previousAimFps, currentAimFps);
    }

    private const float damp_time = 100;
    private const double spike_time_ms = 20;
    private const double min_time_between_updates = 10;
    private double lastUpdate = double.MinValue;

    protected override void Update()
    {
        base.Update();

        updateAimFPS();

        // FIXME: this includes throttle time, which is not ideal.
        double elapsedDrawFrameTime = drawClock.ElapsedFrameTime;

        bool hasDrawSpike = displayFps > (1000 / spike_time_ms) && elapsedDrawFrameTime > spike_time_ms;

        displayFrameTime = Interpolation.DampContinuously(displayFrameTime, elapsedDrawFrameTime, hasDrawSpike ? 0 : damp_time, elapsedDrawFrameTime);

        if (hasDrawSpike)
            // show spike time using raw elapsed value, to account for `FramesPerSecond` being so averaged spike frames don't show.
            displayFps = 1000 / elapsedDrawFrameTime;
        else
            displayFps = Interpolation.DampContinuously(displayFps, drawClock.FramesPerSecond, damp_time, Time.Elapsed);


        if (Time.Current - lastUpdate > min_time_between_updates)
        {
            updateFpsDisplay();
            updateFrameTimeDisplay();

            lastUpdate = Time.Current;
        }
    }

    private double displayFrameTime = 0;
    private void updateFrameTimeDisplay()
    {
        frameTimeText.Text = displayFrameTime switch
        {
            < 1 => $"{displayFrameTime:F2}m",
            < 10 => $"{displayFrameTime:F1}m",
            _ => $"{(int)Math.Round(displayFrameTime)}m",
        };

        if (displayFrameTime < 8.0)
            frameTimeBackground.FadeColour(color_okay, background_fade_time);
        else if (displayFrameTime < 16.0)
            frameTimeBackground.FadeColour(color_warning, background_fade_time);
        else
            frameTimeBackground.FadeColour(color_danger, background_fade_time);
    }

    private const float background_fade_time = 200;

    private double displayFps = 0;
    private void updateFpsDisplay()
    {
        int displayFpsRounded = (int)Math.Min(displayFps, currentAimFps);
        fpsText.Text = $"{displayFpsRounded}";

        if (drawClock.Throttling)
        {
            double throttlingTarget = drawClock.MaximumUpdateHz;

            if (displayFpsRounded >= targetRefreshRate || displayFpsRounded >= throttlingTarget * 0.75f)
                fpsBackground.FadeColour(color_okay, background_fade_time);
            else if (displayFpsRounded > throttlingTarget - 10.0 && displayFpsRounded > throttlingTarget * 0.95)
                fpsBackground.FadeColour(color_warning, background_fade_time);
            else
                fpsBackground.FadeColour(color_danger, background_fade_time);
        }
        else
        {
            if (displayFpsRounded >= (4 * targetRefreshRate))
                fpsBackground.FadeColour(color_okay, background_fade_time);
            else if (displayFpsRounded >= (2 * targetRefreshRate))
                fpsBackground.FadeColour(color_warning, background_fade_time);
            else
                fpsBackground.FadeColour(color_danger, background_fade_time);
        }
    }

    private partial class FpsLargeSpriteText : LegacySpriteText
    {
        public const char ms = 'm';
        public const char comma = ',';
        public const char dot = '.';

        protected override char[] FixedWidthExcludeCharacters => new[] { comma, dot, ms };

        private static readonly FrozenDictionary<char, string> mappings = new Dictionary<char, string>
        {
            { ms, "ms" },
            { comma, "comma" },
            { dot, "dot" },
        }.ToFrozenDictionary();

        public FpsLargeSpriteText() : base("fps")
        {
            FontOverlap = 1;
            FixedWidth = false;
            CustomMappings = mappings;
        }
    }

    private partial class FpsSmallSpriteText : LegacySpriteText
    {
        private const char slash = '/';
        public const char fps = 'f';
        public const char hz = 'h';

        private static readonly FrozenDictionary<char, string> mappings = new Dictionary<char, string>
        {
            { slash, "slash" },
            { fps, "fps" },
            { hz, "hz" },
        }.ToFrozenDictionary();

        public FpsSmallSpriteText() : base("fpss")
        {
            FontOverlap = 1f;
            FixedWidth = false;
            CustomMappings = mappings;
        }
    }
}
