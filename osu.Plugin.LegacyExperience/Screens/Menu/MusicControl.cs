using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Audio;
using osu.Plugin.LegacyExperience.Graphics;
using osuTK;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class MusicControl : CompositeDrawable
{
    private Container playingInfoContainer = null!;
    private FontText songTitle = null!;

    private Container permanentContainer = null!;

    private ProgressBar progressBar = null!;

    // TODO: we don't have a config persistence mechanism yet.
    private readonly BindableBool permanentSongInfo = new BindableBool(true);

    [Resolved]
    private MusicController musicController { get; set; } = null!;

    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Size = new Vector2(150, 43) * LegacyExperiencePlugin.StableRatio;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                Child = playingInfoContainer = new Container
                {
                    Anchor = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Texture = textures.GetAutoSized("UI/menu-np"),
                        },
                        songTitle = new FontText
                        {
                            Text = "No music playing",
                            BypassAutoSizeAxes = Axes.Both,
                            Font = LegacyFont.Default.With(size: 14),
                            Position = new Vector2(100, 0),
                            // TODO: remove these two lines when migrated to NativeText.
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                    }
                }
            },
            permanentContainer = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 20 * LegacyExperiencePlugin.StableRatio,
                Size = new Vector2(136, 22) * LegacyExperiencePlugin.StableRatio,
                ChildrenEnumerable = new ControlButton[]
                {
                    new(FontAwesome.Solid.StepBackward)
                    {
                        TooltipText = "Previous track",
                        // TODO: we didn't implement stable's NotificationManager yet.
                        Action = () => musicController.PreviousTrack(allowProtectedTracks: true),
                    },
                    new(FontAwesome.Solid.Play)
                    {
                        TooltipText = "Play",
                        Action = () => musicController.Play(),
                    },
                    new(FontAwesome.Solid.Pause)
                    {
                        TooltipText = "Pause",
                        Action = () => musicController.Stop(true),
                    },
                    new(FontAwesome.Solid.Stop)
                    {
                        TooltipText = "Stop the music!",
                        Action = () =>
                        {
                            // Don't use musicController.SeekTo, it schedules internally.
                            musicController.CurrentTrack.Seek(0);
                            musicController.Stop(true);
                        },
                    },
                    new(FontAwesome.Solid.StepForward)
                    {
                        TooltipText = "Next track",
                        Action = () => musicController.NextTrack(allowProtectedTracks: true),
                    },
                    new(FontAwesome.Solid.Info)
                    {
                        TooltipText = "View song info",
                        Action = () =>
                        {
                            permanentSongInfo.Toggle();
                            ShowNowPlaying();
                        }
                    },
                    new(FontAwesome.Solid.Bars)
                    {
                        TooltipText = "Jump To window",
                    },
                }.Select(configureButton)
                 .Concat(new Drawable[]
                 {
                     progressBar = new ProgressBar
                     {
                         Anchor = Anchor.TopRight,
                         Origin = Anchor.TopRight,
                         Position = new Vector2(-9, 20) * LegacyExperiencePlugin.StableRatio,
                         SeekRequested = (progress) => musicController.SeekTo(progress * musicController.CurrentTrack.Length),
                     }
                 }),
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        beatmap.BindValueChanged(v =>
        {
            var metadata = v.NewValue.BeatmapInfo.Metadata;

            var artist = new RomanisableString(metadata.ArtistUnicode, metadata.Artist);
            var title = new RomanisableString(metadata.TitleUnicode, metadata.Title);
            songTitle.Text = LocalisableString.Interpolate($"{artist} - {title}");

            ShowNowPlaying();
        }, true);
    }

    protected override void Update()
    {
        base.Update();

        var track = musicController.CurrentTrack;
        var progress = track.Length > 0 ? track.CurrentTime / track.Length : 0;

        progressBar.Progress.Value = Math.Clamp(progress, 0, 1);
    }

    internal void ShowNowPlaying()
    {
        permanentContainer.MoveToY(20 * LegacyExperiencePlugin.StableRatio, 400);

        float targetX = playingInfoContainer.ToLocalSpace(songTitle.ScreenSpaceDrawQuad.TopRight).X;

        // FIXME: trailing space are less than stable, 
        // investigate this when FontText are using NativeText for rendering.
        playingInfoContainer
            .MoveToX(-targetX + 80)
            .MoveToX(-targetX - 10, 1000, Easing.Out)
            // I think peppy wanted to use EasingTypes.Out here.
            // 
            //     Transformation transformation = new Transformation(new Vector2(-80f + titleWidth, 0f), new Vector2(10f + titleWidth, 0f), GameBase.Time, GameBase.Time + 1000);
            //     transformation.Easing = EasingTypes.Out;
            //     Transformation transformation2 = new Transformation(TransformationType.Fade, 0f, 1f, GameBase.Time, GameBase.Time + 1000);
            //     transformation.Easing = EasingTypes.Out;
            //
            // but he applied EasingTypes.Out to movement transformation twice?
            .FadeInFromZero(1000);

        if (!permanentSongInfo.Value)
        {
            using (BeginDelayedSequence(6000))
                HideNowPlaying();
        }
    }

    internal void HideNowPlaying()
    {
        float titleWidth = songTitle.DrawWidth;

        playingInfoContainer
            .MoveToX(-titleWidth + 80 * 1.6f, 2000, Easing.In)
            .FadeOut(2000, Easing.In);

        permanentContainer
            .Delay(1600)
            .MoveToY(0, 400);
    }

    private static ControlButton configureButton(ControlButton b, int index)
    {
        b.Anchor = Anchor.TopRight;
        b.Origin = Anchor.Centre;
        b.Position = new Vector2(-136 + index * 20, 11) * LegacyExperiencePlugin.StableRatio;

        b.FontSize = 14;
        return b;
    }

    // osu!framework's FontAwesome icons generally look resemble to stable's,
    // but they are a bit "rounded corners" than stable's icons, 
    // so just use the loaded font to render the icons to make them look exactly the same as stable's.
    private partial class ControlButton : FontAwesomeIcon, IHasLegacyTooltip
    {
        public ControlButton(IconUsage icon)
        {
            Icon = icon;
        }

        public LocalisableString TooltipText { get; set; }

        [Resolved]
        private AudioEngine audioEngine { get; set; } = null!;

        protected override bool OnHover(HoverEvent e)
        {
            audioEngine.Click(sample: LegacySample.click_short);
            this.ScaleTo(1.2f, 100, Easing.Out);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.ScaleTo(1, 200, Easing.Out);

            base.OnHoverLost(e);
        }

        public Action? Action { get; set; }

        protected override bool OnClick(ClickEvent e)
        {
            audioEngine.Click(sample: LegacySample.click_short_confirm);
            this.ScaleTo(1.2f, 200, Easing.Out);

            Action?.Invoke();
            return true;
        }
    }

    private partial class ProgressBar : CompositeDrawable, IHasLegacyTooltip
    {
        public LocalisableString TooltipText => "Click to seek to a specific point in the song.";

        public readonly BindableDouble Progress = new BindableDouble(0)
        {
            MinValue = 0,
            MaxValue = 1,
        };

        public Action<double>? SeekRequested { get; set; }

        private Box background = null!;
        private Box fill = null!;

        private static readonly Colour4 bgDefaultColour = new Colour4(20, 20, 20, 128);
        private static readonly Colour4 bgHoverColour = new Colour4(60, 60, 60, 128);

        [BackgroundDependencyLoader]
        private void load()
        {
            Blending = BlendingParameters.Additive;

            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    Size = new Vector2(134, 3) * LegacyExperiencePlugin.StableRatio,
                    Colour = bgDefaultColour,
                },
                fill = new Box
                {
                    Scale = new Vector2(0, 1),
                    BypassAutoSizeAxes = Axes.Both,
                    Size = new Vector2(134, 3) * LegacyExperiencePlugin.StableRatio,
                    Colour = Colour4.White.Opacity(128 / 255f),
                }
            };

            Progress.BindValueChanged(v => fill.Scale = new Vector2((float)v.NewValue, 1), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(bgHoverColour, 100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(bgDefaultColour, 100);
        }

        private void seekTo(Vector2 screenSpaceCursorPos)
        {
            var localPos = ToLocalSpace(screenSpaceCursorPos);
            float progress = Math.Clamp(localPos.X / background.DrawWidth, 0, 1);

            SeekRequested?.Invoke(progress);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            return true; // OnDrag requires OnDragStart to return true
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            // don't call base as we didn't call it in OnDragStart
        }

        // stable doesn't allow you drag to seek,
        // but i think it's better to allow it in our implementation, so let's just seek to the position while dragging.
        protected override void OnDrag(DragEvent e)
        {
            seekTo(e.ScreenSpaceMousePosition);
        }

        protected override bool OnClick(ClickEvent e)
        {
            seekTo(e.ScreenSpaceMousePosition);
            return true;
        }
    }
}
