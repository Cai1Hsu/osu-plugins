// This file is adapted from osu!lazer's FpsCounter.
// Original file: https://github.com/ppy/osu/blob/master/osu.Game/Graphics/UserInterface/FPSCounter.cs

using System.Collections.Frozen;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Plugins.Legacy;
using osuTK;
using osuTK.Graphics;
using static osu.Game.Plugins.Legacy.LegacySpriteText;
using LegacySpriteTextContainer = osu.Game.Plugins.Legacy.LegacySpriteTextContainer;
using ISerialisableDrawable = osu.Game.Skinning.ISerialisableDrawable;
using osu.Game.Plugins;

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

    private FpsLargeSpriteTextContainer fpsText = null!;
    private FpsSmallSpriteTextContainer targetRefreshRateText = null!;
    private FpsLargeSpriteTextContainer frameTimeText = null!;

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
    private void load(TextureStore textures, FrameworkConfigManager? config)
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
                                Texture = textures.GetAutoSized("UI/fps-box"),
                                Colour = color_okay,
                            },
                            fpsText = new FpsLargeSpriteTextContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.CentreRight,
                                X = -7,
                            },
                            targetRefreshRateText = new FpsSmallSpriteTextContainer
                            {
                                Anchor = Anchor.BottomRight,
                                Origin = Anchor.BottomRight,
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
                            frameTimeBackground = new Sprite
                            {
                                Name = "background",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Texture = textures.GetAutoSized("UI/fps-box"),
                                Colour = color_okay,
                            },
                            frameTimeText = new FpsLargeSpriteTextContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
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

        currentDisplay.BindTo(window.CurrentDisplayBindable);
        currentDisplayMode.BindTo(window.CurrentDisplayMode);

        currentDisplay.BindValueChanged(_ => updateTargetRefreshRate());
        currentDisplayMode.BindValueChanged(_ => updateTargetRefreshRate(), true);
    }

    private IBindable<Display> currentDisplay = new Bindable<Display>();
    private IBindable<DisplayMode> currentDisplayMode = new Bindable<DisplayMode>();

    private IBindable<FrameSync>? frameSyncMode;

    private ThrottledFrameClock drawClock = null!;

    private volatile float targetRefreshRate = 60;

    private void updateTargetRefreshRate()
    {
        var displayRefreshRate = host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 60;

        // Don't use clock.MaximumUpdateHz as it's somtimes unreliable
        int multipler = (frameSyncMode?.Value) switch
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

        var newColor = displayFrameTime switch
        {
            < 8.0 => color_okay,
            < 16.0 => color_warning,
            _ => color_danger,
        };

        frameTimeBackground.FadeColour(newColor, background_fade_time);
    }

    private const float background_fade_time = 200;

    private double displayFps = 0;
    private void updateFpsDisplay()
    {
        int displayFpsRounded = (int)Math.Min(displayFps, currentAimFps);
        fpsText.Text = $"{displayFpsRounded}";

        Color4 newColor;

        if (drawClock.Throttling)
        {
            double throttlingTarget = drawClock.MaximumUpdateHz;

            newColor = displayFpsRounded switch
            {
                _ when targetRefreshRate >= throttlingTarget * 0.95f ||
                    displayFpsRounded >= targetRefreshRate => color_okay,
                _ when displayFpsRounded > throttlingTarget - 10.0 ||
                    displayFpsRounded > throttlingTarget * 0.75f => color_warning,
                _ => color_danger,
            };
        }
        else
        {
            // FIXME: this is incorrect, as multiplier is included in targetRefreshRate
            newColor = displayFpsRounded switch
            {
                _ when displayFpsRounded >= (4 * targetRefreshRate) => color_okay,
                _ when displayFpsRounded >= (2 * targetRefreshRate) => color_warning,
                _ => color_danger,
            };
        }

        fpsBackground.FadeColour(newColor, background_fade_time);
    }

    internal static TextureLookupDelegate CreateTextureLookup(IReadOnlyDependencyContainer dependencies)
    {
        var textures = dependencies.Get<TextureStore>()
            ?? throw new InvalidOperationException($"Could not retrieve {nameof(TextureStore)} from dependency container.");
        return textures.GetAutoSized;
    }

    private partial class FpsLargeSpriteTextContainer : LegacySpriteTextContainer
    {
        public const char ms = 'm';
        public const char comma = ',';
        public const char dot = '.';

        private static readonly FrozenDictionary<char, string> mappings = new Dictionary<char, string>
        {
            { ms, "ms" },
            { comma, "comma" },
            { dot, "dot" },
        }.ToFrozenDictionary();

        private partial class FpsLargeSpriteText : LegacySpriteText
        {
            public FpsLargeSpriteText(string fontPrefix) : base(fontPrefix)
            {
            }

            private static readonly char[] fixedWidthExcludeCharacters = new[] { comma, dot, ms };

            protected override char[] FixedWidthExcludeCharacters => fixedWidthExcludeCharacters;
        }

        protected override TextureLookupDelegate CreateTextureLookup(IReadOnlyDependencyContainer dependencies)
            => LegacyFpsDisplay.CreateTextureLookup(dependencies);

        protected override LegacySpriteText CreateSpriteText(string fontPrefix)
        {
            var spriteText = new FpsLargeSpriteText(fontPrefix);
            spriteText.FixedWidth = false;
            spriteText.FontOverlap = 1f;
            spriteText.CustomMappings = mappings;
            return spriteText;
        }

        public FpsLargeSpriteTextContainer() : base("UI/fps")
        {
            FontHeight = 11;
        }
    }

    private partial class FpsSmallSpriteTextContainer : LegacySpriteTextContainer
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

        protected override LegacySpriteText CreateSpriteText(string fontPrefix)
        {
            var spriteText = base.CreateSpriteText(fontPrefix);
            spriteText.FixedWidth = false;
            spriteText.FontOverlap = 1f;
            spriteText.CustomMappings = mappings;
            return spriteText;
        }

        protected override TextureLookupDelegate CreateTextureLookup(IReadOnlyDependencyContainer dependencies)
            => LegacyFpsDisplay.CreateTextureLookup(dependencies);

        public FpsSmallSpriteTextContainer() : base("UI/fpss")
        {
            FontHeight = 10;
        }
    }
}
