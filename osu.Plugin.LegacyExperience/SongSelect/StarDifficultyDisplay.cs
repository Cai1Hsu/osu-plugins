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
                Origin = Anchor.CentreLeft,
                Position = new Vector2(i * 12, 0), // TODO: spacing
                Scale = default_scale, // TODO: investigate scale?
            };

            stars[i] = star;
        }

        InternalChildren = stars;

        if (skin is not null)
            skin.SourceChanged += onSkinChanged;

        onSkinChanged(); // update textures initially
    }

    private static readonly Vector2 default_scale = new Vector2(0.35f);

    private static readonly Colour4 active_colour = Colour4.White;
    private static readonly Colour4 inactive_colour = Colour4.White.Opacity(30 / 255f);

    private const float fade_duration = 600;
    private const float scale_duration = 500;

    protected override void LoadComplete()
    {
        base.LoadComplete();

        Current.BindValueChanged(v =>
        {
            double value = v.NewValue;

            // TODO: this animation is not exactly faithful to the stable's implementation.
            for (int i = 0; i < stars.Length; i++)
            {
                var star = stars[i];

                Colour4 target_colour;
                Vector2 target_scale;

                double appear_ratio = Math.Clamp(value - i, 0, 1);

                target_colour = appear_ratio > 0 ? active_colour : inactive_colour;
                target_scale = default_scale * (float)Interpolation.Lerp(0.6f, 1.0f, (float)appear_ratio);

                star.FadeColour(target_colour, fade_duration)
                    .ScaleTo(target_scale, scale_duration, Easing.OutBack);
            }
        }, true);
    }

    private void onSkinChanged()
    {
        float offsetX = 0;

        var texture = skin.GetSkinTexture("star", textures, "UI");

        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];

            // we've packed a fallback texture, so this should never be null
            Debug.Assert(texture is not null);

            star.Texture = texture;
            star.Size = texture.DisplaySize;

            // reposition stars based on texture size
            star.X = offsetX * LegacyExperiencePlugin.StableRatio;

            // Although textures are expected to be the same, we use the actual width to be safe.
            offsetX += texture.DisplayWidth * 0.625f * 0.6f;
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
