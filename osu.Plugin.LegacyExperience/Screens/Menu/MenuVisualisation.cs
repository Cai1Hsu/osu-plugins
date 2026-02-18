using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Layout;
using osu.Game.Plugins;
using osuTK;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class MenuVisualisation : CompositeDrawable
{
    [Resolved]
    private IAmplitudesProvider amplitudes { get; set; } = null!;

    // used in intro stage
    public static readonly Colour4 intro_colour = new Colour4(0, 114, 255, 255);

    public static readonly Colour4 default_colour = new Colour4(128, 128, 160, 255);

    private readonly Sprite[] sprites = new Sprite[IAmplitudesProvider.SampleSize];

    private LayoutValue radiusLayout = new LayoutValue(Invalidation.None);

    public float Radius
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            radiusLayout.Invalidate();
        }
    }

    public MenuVisualisation()
    {
        AddLayout(radiusLayout);
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Colour = default_colour;

        var texture = textures.GetAutoSized("UI/menu-vis");

        // Currently using sprites, may migrate to custom draw node if performance becomes an issue, but seems fine for now.
        for (int i = 0; i < sprites.Length; i++)
        {
            // negative to make the direction agree with stable, don't know why.
            double angle = -Math.Tau * (0.4 + (double)i / sprites.Length * (Overshoot * 0.5));
            directions[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

            AddInternal(sprites[i] = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreLeft,
                Alpha = 0.2f,
                Blending = BlendingParameters.Additive,
                Scale = new Vector2(0, 0.5f),
                Texture = texture,
                Rotation = (float)(angle * 180 / Math.PI)
            });
        }
    }

    private readonly Vector2[] directions = new Vector2[IAmplitudesProvider.SampleSize];

    protected override void Update()
    {
        base.Update();

        if (!radiusLayout.IsValid)
        {
            var value = Radius;

            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i].Position = directions[i] * value * LegacyExperiencePlugin.StableRatio;
            }

            radiusLayout.Validate();
        }

        updateVisualisation(Clock.ElapsedFrameTime);
    }

    public float MaxAlpha { get; set; } = 0.4f;

    public float Overshoot { get; init; } = 8f;

    private double startOffset;

    private double columnCurrentMilliseconds;

    private void updateVisualisation(double elapsed)
    {
        if (elapsed > 1000)
            return;

        const double column_step = 10.0;

        while (elapsed > 0.0)
        {
            double remainingInCurrentColumn = column_step - columnCurrentMilliseconds;
            double elapsedForCurrentColumn = Math.Min(elapsed, remainingInCurrentColumn);

            updateColumn(elapsedForCurrentColumn);

            columnCurrentMilliseconds += elapsedForCurrentColumn;
            elapsed -= elapsedForCurrentColumn;

            if (columnCurrentMilliseconds < column_step)
                break;

            columnCurrentMilliseconds = 0.0;
            startOffset = (startOffset + 50.0) % sprites.Length;
        }
    }

    private void updateColumn(double elapsed)
    {
        const double sixty_fps = 1000.0 / 60;

        double frameRatio = Math.Pow(0.95, elapsed / sixty_fps);

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];

            Vector2 scale = sprite.Scale;
            scale.X = Math.Max(scale.X, amplitudes.Data[(int)((i + startOffset) % amplitudes.Data.Length)] * 3) * (float)frameRatio;

            if (scale.X < 0.01f)
            {
                scale.X = 0f;
                sprite.Alpha = 0f;
            }
            else
            {
                sprite.Alpha = Math.Max(0f, MaxAlpha * Math.Min(1f, (scale.X - 0.04f) / 0.08f));
            }

            sprite.Scale = scale;
        }
    }
}
