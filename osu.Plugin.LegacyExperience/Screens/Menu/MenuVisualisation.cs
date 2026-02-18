using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Plugins;
using osuTK;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

public partial class MenuVisualisation : Drawable
{
    [Resolved]
    private IAmplitudesProvider amplitudes { get; set; } = null!;

    // used in intro stage
    public static readonly Colour4 intro_colour = new Colour4(0, 114, 255, 255);

    public static readonly Colour4 default_colour = new Colour4(128, 128, 160, 255);

    private readonly VisualisationColumn[] columns = new VisualisationColumn[IAmplitudesProvider.SampleSize];

    public float Radius { get; set; }

    private Texture texture = null!;
    private IShader shader = null!;

    /// <summary>
    /// Initializes a new <see cref="MenuVisualisation"/> and configures rendering to use additive blending.
    /// </summary>
    public MenuVisualisation()
    {
        Blending = BlendingParameters.Additive;
    }

    /// <summary>
    /// Loads the menu visualisation texture and shader and sets the drawable colour to <see cref="default_colour"/>.
    /// </summary>
    /// <remarks>
    /// Asserts that the built-in texture "UI/menu-vis" was loaded; failure indicates a missing resource.
    /// </remarks>
    [BackgroundDependencyLoader]
    private void load(TextureStore textures, ShaderManager shaders)
    {
        texture = textures.GetAutoSized("UI/menu-vis")!;

        // this is a built-in texture and should always be present, so it's fine to throw if it isn't found.
        Debug.Assert(texture != null, "Failed to load menu visualisation texture.");

        shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);

        Colour = default_colour;
    }

    /// <summary>
/// Creates the draw node responsible for rendering this MenuVisualisation.
/// </summary>
/// <returns>A new <see cref="MenuVisualisationDrawNode"/> instance bound to this visualisation.</returns>
protected override DrawNode CreateDrawNode() => new MenuVisualisationDrawNode(this);

    /// <summary>
    /// Advances the visualisation's animation by the current frame's elapsed time and schedules a redraw of the draw node.
    /// </summary>
    protected override void Update()
    {
        base.Update();
    
        updateVisualisation(Clock.ElapsedFrameTime);
    
        Invalidate(Invalidation.DrawNode);
    }
    protected override void Update()
    {
        base.Update();

        updateVisualisation(Clock.ElapsedFrameTime);

        Invalidate(Invalidation.DrawNode);
    }

    public float MaxAlpha { get; set; } = 0.4f;

    public float Overshoot { get; set; } = 8f;

    private double startOffset;

    private double columnCurrentMilliseconds;

    /// <summary>
    /// Advances the visualisation state by the given elapsed time.
    /// </summary>
    /// <param name="elapsed">Time in milliseconds to advance the visualisation; values greater than 1000 are ignored.</param>
    /// <remarks>
    /// The method processes time in discrete 10ms column steps. For each completed 10ms step it updates column state and increments the internal start offset by 50, wrapping modulo the number of columns.
    /// </remarks>
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
            startOffset = (startOffset + 50.0) % columns.Length;
        }
    }

    /// <summary>
    /// Updates each column's horizontal scale and transparency based on elapsed time and current amplitude samples.
    /// </summary>
    /// <param name="elapsed">Elapsed time in milliseconds used to compute the decay factor applied to existing column scales.</param>
    private void updateColumn(double elapsed)
    {
        const double sixty_fps = 1000.0 / 60;

        float frameRatio = (float)Math.Pow(0.95, elapsed / sixty_fps);

        for (int i = 0; i < columns.Length; i++)
        {
            ref VisualisationColumn column = ref columns[i];

            column.ScaleX = Math.Max(column.ScaleX, amplitudes.Data[(int)((i + startOffset) % amplitudes.Data.Length)] * 3) * frameRatio;

            if (column.ScaleX < 0.01f)
            {
                column.ScaleX = 0f;
            }
            else
            {
                column.Alpha = Math.Max(0f, MaxAlpha * Math.Min(1f, (column.ScaleX - 0.04f) / 0.08f));
            }
        }
    }

    private struct VisualisationColumn
    {
        public float Alpha;
        public float ScaleX;
    }

    private class MenuVisualisationDrawNode : DrawNode
    {
        protected new MenuVisualisation Source => (MenuVisualisation)base.Source;

        private readonly VisualisationColumn[] columns = new VisualisationColumn[IAmplitudesProvider.SampleSize];

        /// <summary>
        /// Creates a draw node responsible for rendering the provided MenuVisualisation.
        /// </summary>
        /// <param name="source">The MenuVisualisation instance that this draw node will read state from.</param>
        public MenuVisualisationDrawNode(MenuVisualisation source)
            : base(source)
        {
        }

        private float overshoot;

        private Texture texture = null!;
        private IShader shader = null!;

        private float radius;

        private Vector2 center;

        /// <summary>
        /// Syncs visible state from the source MenuVisualisation into this draw node.
        /// </summary>
        /// <remarks>
        /// Copies the source's Overshoot, Radius, shader, texture, draw center, and per-column visual data into the draw node so the renderer has a local, immutable snapshot of the visual state for drawing.
        /// </remarks>
        public override void ApplyState()
        {
            base.ApplyState();

            overshoot = Source.Overshoot;
            radius = Source.Radius;
            shader = Source.shader;
            texture = Source.texture;
            center = Source.DrawSize / 2;
            Source.columns.CopyTo(columns, 0);
        }

        private IVertexBatch<TexturedVertex2D>? vertexBatch;

        const float scaleY = 0.5f;

        /// <summary>
        /// Renders the visualisation columns as textured quads arranged around the control's center, using each column's scale, rotation, and alpha to position, size, and tint the quads.
        /// </summary>
        /// <param name="renderer">The renderer used to draw the textured quads.</param>
        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(columns.Length, 1);

            shader.Bind();
            renderer.PushLocalMatrix(DrawInfo.Matrix);

            for (int i = 0; i < columns.Length; i++)
            {
                VisualisationColumn column = columns[i];

                if (column.Alpha <= 0 || column.ScaleX <= 0)
                    continue;

                // negative to make the direction agree with stable, don't know why.
                float rotation = -MathF.Tau * (0.4f + (float)i / columns.Length * (overshoot * 0.5f));

                float rotationCos = MathF.Cos(rotation);
                float rotationSin = MathF.Sin(rotation);

                Vector2 origin = center + new Vector2(rotationCos, rotationSin) * radius;
                Vector2 size = texture.DisplaySize * new Vector2(column.ScaleX, scaleY);

                float halfThickness = size.Y / 2;

                Vector2 outwardDirection = new Vector2(rotationCos, rotationSin) * size.X;
                Vector2 tangentDirection = new Vector2(-rotationSin, rotationCos) * halfThickness;

                var quad = new Quad(
                    origin - tangentDirection,
                    origin + outwardDirection - tangentDirection,
                    origin + tangentDirection,
                    origin + outwardDirection + tangentDirection
                );

                var colour = DrawColourInfo.Colour.MultiplyAlpha(column.Alpha);

                renderer.DrawQuad(texture, quad, colour, null, vertexBatch.AddAction);
            }

            renderer.PopLocalMatrix();

            shader.Unbind();
        }

        /// <summary>
        /// Disposes resources held by the draw node, including the vertex batch.
        /// </summary>
        /// <param name="isDisposing">True when called from Dispose, false when called from a finalizer.</param>
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            vertexBatch?.Dispose();
        }
    }
}