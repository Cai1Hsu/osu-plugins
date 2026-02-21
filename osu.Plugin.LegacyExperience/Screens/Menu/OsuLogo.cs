using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Audio;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class OsuLogo : BeatSyncedContainer
{
    private Sprite logo_glow = null!;

    [Resolved]
    private IBeatSyncProvider beatSyncSource { get; set; } = null!;

    [Resolved]
    private MusicController musicController { get; set; } = null!;

    private CircularContainer logoContainer = null!;

    private MenuVisualisation visualisation;

    public MenuVisualisation Visualisation => visualisation;

    [Resolved]
    private ISkinSource? skin { get; set; } = null;

    private readonly Container rippleContainer;

    private readonly DrawablePool<MenuRipple> ripplePool;

    public OsuLogo()
    {
        ripplePool = new DrawablePool<MenuRipple>(10);
        rippleContainer = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
        };
        visualisation = new LogoVisualisation
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AlwaysPresent = true,
            RelativeSizeAxes = Axes.Both,
        };
    }

    /// <summary>
    /// Creates a proxy container for the ripple and visualisation effects.
    /// This allows the effects to be drawn at a different position in the scene graph
    /// (e.g. behind menu buttons) while remaining in sync with the logo's position.
    /// </summary>
    public Drawable CreateEffectsProxy() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            rippleContainer.CreateProxy(),
            visualisation.CreateProxy(),
        }
    };

    [BackgroundDependencyLoader]
    private void load(TextureStore texture, IAPIProvider api)
    {
        var logoTexture = texture.GetAutoSized("UI/menu-osu");

        Debug.Assert(logoTexture is not null, "Failed to load menu logo texture.");

        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            ripplePool,
            rippleContainer,
            visualisation,
            logoContainer = new CircularContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                // TODO: investigate whether this is the correct size to use for the logo.
                Size = new Vector2(300 * LegacyExperiencePlugin.StableRatio),
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        Texture = logoTexture,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                }
            },
            logo_glow = new Sprite
            {
                Texture = logoTexture,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0.5f,
            },
        };

        localUser.BindTo(api.LocalUser);

        skin?.SourceChanged += updateSkin;
        localUser.BindValueChanged(_ => updateSkin(), true);
    }

    private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

    private void updateSkin()
    {
        var color = MenuVisualisation.default_colour;

        // We could remove the supporter requirement technically,
        // but i decide to respect ppy's decision to make menu glow a supporter feature.
        if (localUser.Value.IsSupporter)
            color = skin?.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? color;

        visualisation.Colour = color;
    }

    public override bool HandlePositionalInput => true;

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        => logoContainer.ReceivePositionalInputAt(screenSpacePos);

    [Resolved]
    private AudioEngine audioEngine { get; set; } = null!;

    protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
    {
        logo_glow.Blending = effectPoint.KiaiMode ? BlendingParameters.Additive : BlendingParameters.Inherit;
        savedProgressMultiplier = menuAlpha2;

        if (IsHovered)
            audioEngine.PlaySample(LegacySample.heartbeat);

        spawnRipple();
    }

    private void spawnRipple()
    {
        var ripple = ripplePool.Get();

        var scale = logoContainer.Scale;

        ripple.Scale = scale;
        ripple.Alpha = 0.1f * menuAlpha2;

        rippleContainer.Add(ripple);

        ripple.FadeOut(1000, Easing.None)
              .ScaleTo(scale * 1.4f, 1000, Easing.Out)
              .Expire();
    }

    private const double sixty_fps = 1000.0 / 60.0;

    private float lastFrameBeatProgress;
    private float hoverBonus;

    private int lastSixtyFrameIndex = -1;

    // wtf is this name? i can't figure out what this does, so copy the name from stable for now.
    private float menuAlpha2 = 0.5f;
    private float savedProgressMultiplier;

    protected override void Update()
    {
        base.Update();

        var sixtyFrameIndex = (int)(Time.Current / sixty_fps);

        if (sixtyFrameIndex != lastSixtyFrameIndex)
        {
            float combinedChannelLevel = 32768;

            if (musicController.IsPlaying)
            {
                var amplitudes = beatSyncSource.CurrentAmplitudes;
                combinedChannelLevel *= amplitudes.LeftChannel + amplitudes.RightChannel;
            }

            float targetMenuGlowAlpha = IsKiaiTime ? 1f : (0.6f + Math.Clamp((float)(combinedChannelLevel - 30000) / 35536f, 0f, 1f) * 0.4f);
            menuAlpha2 = menuAlpha2 * 0.8f + 0.2f * targetMenuGlowAlpha;
        }

        lastSixtyFrameIndex = sixtyFrameIndex;

        double frameRatio = Time.Elapsed / sixty_fps;

        if (!IsHovered && hoverBonus >= 0f)
            hoverBonus = Math.Max(hoverBonus - (float)(0.012 * frameRatio), 0f);
        else
            hoverBonus = Math.Min(hoverBonus + (float)(0.012 * frameRatio), 0.1f);

        var beatLength = TimeSinceLastBeat + TimeUntilNextBeat;
        var beatProgress = beatLength > 0 ? TimeSinceLastBeat / beatLength : 0.0;

        float smoothingDecay = (float)Math.Pow(0.5, frameRatio);

        float smoothedBeatProgress = lastFrameBeatProgress * smoothingDecay
            + (float)Math.Clamp(1f - (beatProgress * 0.5f + 0.5f), 0f, 1f) * (1f - smoothingDecay);

        lastFrameBeatProgress = smoothedBeatProgress;

        float valueAt(float start, float end, float progress, Easing easing) => Interpolation.ValueAt(progress, start, end, 0, 1, easing);

        logoContainer.Scale = new Vector2(valueAt(1.05f + hoverBonus, 1f + hoverBonus, smoothedBeatProgress, Easing.OutQuad));
        logo_glow.Alpha = valueAt(IsKiaiTime ? 0.1f : 0.4f, 0f, smoothedBeatProgress, Easing.OutQuad) * savedProgressMultiplier;
        logo_glow.Scale = new Vector2(valueAt(1.05f + hoverBonus, 1.08f + hoverBonus, smoothedBeatProgress, Easing.OutQuad));

        if (visualisation.AlwaysPresent)
        {
            // TODO: parallax affects visualisation alpha as well, 
            // but we don't have a way to determine the parallax amount.
            // screen space position may work, but it doesn't work in test scene(test scene is not centered in the game window).
            visualisation.Alpha = (IsKiaiTime ? 1f : 0.7f) * 0.7f;
            visualisation.Radius = valueAt(1.05f + hoverBonus, 1.08f + hoverBonus, 1 - smoothedBeatProgress, Easing.InQuad) * 150f * LegacyExperiencePlugin.StableRatio;
        }
    }

    public Action? Action { get; set; }

    protected override bool OnClick(ClickEvent e)
    {
        hoverBonus -= 0.08f;

        Action?.Invoke();
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateSkin;
    }

    private partial class LogoVisualisation : MenuVisualisation
    {
        public override void Hide()
        {
            // workaround to hide the visualisation, the alpha is constantly changed in OsuLogo.Update, so we can't just set alpha to 0.
            Alpha = 0;
            AlwaysPresent = false;
        }

        public override void Show() => AlwaysPresent = true;
    }

    private partial class MenuRipple : PoolableDrawable
    {
        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;

            InternalChild = new Sprite
            {
                Texture = textures.GetAutoSized("UI/menu-osu-shockwave"),
                Blending = BlendingParameters.Additive,
            };
        }
    }
}
