using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Utils;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class StarDifficultyDisplay : CompositeDrawable, IHasCurrentValue<double>
{
    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private TextureStore? textures { get; set; }

    private const int star_count = 10;

    private Star[] stars = new Star[star_count];

    public ReadOnlySpan<Star> Stars => stars;

    public Bindable<double> Current => ((IHasCurrentValue<double>)this).Current;

    Bindable<double> IHasCurrentValue<double>.Current { get; set; } = new BindableDouble();

    public void UpdateStarColor(Color4 color, bool additive)
    {
        Colour = color;
        Blending = additive ? BlendingParameters.Additive : BlendingParameters.Inherit;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;

        for (int i = 0; i < stars.Length; i++)
        {
            var star = new Star
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Scale = new Vector2(base_scale),
                Colour = inactive_colour,
            };

            stars[i] = star;
        }

        InternalChildren = stars;

        if (skin is not null)
            skin.SourceChanged += onSkinChanged;

        onSkinChanged(); // update textures initially
    }

    private const float base_scale = 0.35f;

    private static readonly Colour4 active_colour = Colour4.White;
    private static readonly Colour4 inactive_colour = Colour4.White.Opacity(30 / 255f);

    private const float fade_duration = 600;
    private const float scale_duration = 500;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Current.BindValueChanged(updateStarState, true);
    }

    private void updateStarState(ValueChangedEvent<double> v)
    {
        double targetStars = Math.Clamp(v.NewValue, 0, star_count);

        for (int i = 0; i < stars.Length; i++)
        {
            double starFillAmount = targetStars - i;

            // FIXME: stable uses a much more complex formula as commented below.
            // But that gives a very weird scaling effect for some stars, so we uses a simpler one for now.
            float scale = base_scale * (float)Interpolation.Lerp(0.6f, 1.0f, (float)Math.Clamp(starFillAmount, 0.0, 1.0));

            // float scale = (starFillAmount <= 0.0)
            //     ? 0.6f
            //     : (0.6f * (float)Math.Max(0.5, Math.Min(1.0, starFillAmount) + i * 0.04));

            int starsAppearanceOffset = (int)Math.Floor(i - Math.Min(targetStars, v.OldValue));
            int delaySteps = (v.OldValue <= targetStars)
                ? starsAppearanceOffset
                : ((int)Math.Floor(v.OldValue - targetStars) - starsAppearanceOffset - 1);

            var color = starFillAmount > 0.0 ? active_colour : inactive_colour;

            stars[i].Delay(delaySteps * 80)
                .FadeColour(color, fade_duration)
                .ScaleTo(scale, scale_duration, Easing.OutBack);
        }
    }

    private void onSkinChanged()
    {
        var texture = skin.GetSkinTexture("star", textures, "UI");

        Debug.Assert(texture is not null, "Failed to load star texture from skin.");

        var starSize = texture.DisplaySize * 0.625f * 0.6f;

        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];

            // we've packed a fallback texture, so this should never be null
            Debug.Assert(texture is not null);

            star.Texture = texture;
            star.Size = texture.DisplaySize;

            // reposition stars based on texture size
            star.Position = new Vector2((0.5f + i) * starSize.X, 0) * 1.6f;
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        if (skin is not null)
            skin.SourceChanged -= onSkinChanged;
    }

    public partial class Star : Sprite
    {
    }
}
