using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Utils;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacyJudgements : Drawable
{
    private const int max_sparks = 1024;
    private const long unused_invalidation_id = -1;
    private const float initial_spark_alpha = 0.4f;

    private readonly JudgementSpark[] sparks = new JudgementSpark[max_sparks];
    private int sparkIndex = 0;

    private Vector2 sparkDrawSize;
    private float time = 0;

    public Axes SparkRelativeSizeAxes { get; set; } = Axes.None;
    public Vector2 SparkSize { get; set; } = new Vector2(3, 10);
    public float SparkLifetime { get; set; } = 10000; // 10s

    private IShader shader = null!;

    public LegacyJudgements()
    {
        Blending = BlendingParameters.Additive;
    }

    [BackgroundDependencyLoader]
    private void load(ShaderManager shaders)
    {
        shader = shaders.Load(
            VertexShaderDescriptor.TEXTURE_2,
            FragmentShaderDescriptor.TEXTURE);

        Clear();
    }

    protected override void Update()
    {
        base.Update();

        time = (float)Time.Current;

        sparkDrawSize = SparkSize;

        if (SparkRelativeSizeAxes.HasFlagFast(Axes.X))
            sparkDrawSize.X *= DrawSize.X;
        if (SparkRelativeSizeAxes.HasFlagFast(Axes.Y))
            sparkDrawSize.Y *= DrawSize.Y;

        Invalidate(Invalidation.DrawNode);
    }

    public void Clear()
    {
        for (int i = 0; i < max_sparks; i++)
            sparks[i].InvalidationId = unused_invalidation_id;
    }

    public void SpawnSpark(Vector2 position, Colour4 color)
    {
        sparks[sparkIndex].Position = position;
        sparks[sparkIndex].Color = color;
        sparks[sparkIndex].Time = time;
        ++sparks[sparkIndex].InvalidationId;

        sparkIndex = (sparkIndex + 1) % max_sparks;
    }

    protected override DrawNode CreateDrawNode() => new LegacyJudgementsDrawNode(this);

    private struct JudgementSpark
    {
        public Vector2 Position;
        public Colour4 Color;
        public float Time;
        public long InvalidationId;
    }

    partial class LegacyJudgementsDrawNode : DrawNode
    {
        private new LegacyJudgements Source { get; set; }
        private readonly JudgementSpark[] sparks = new JudgementSpark[max_sparks];

        public LegacyJudgementsDrawNode(LegacyJudgements source) : base(source)
        {
            Source = source;
        }

        private IShader shader = null!;
        private float time;
        private float sparkLifetime;
        private Vector2 sparkDrawSize;

        public override void ApplyState()
        {
            base.ApplyState();

            shader = Source.shader;
            time = Source.time;
            sparkLifetime = Source.SparkLifetime;
            sparkDrawSize = Source.sparkDrawSize;

            Source.sparks.CopyTo(sparks, 0);
        }
        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            // no need to draw anything. also prevent divide by zero in shader.
            if (sparkLifetime <= 0)
                return;

            var averageColorLinear = DrawColourInfo.Colour.AverageColour.Linear;

            if (averageColorLinear.A <= 0)
                return;

            shader.Bind();

            Vector2 inflationAmount = DrawInfo.MatrixInverse.ExtractScale().Xy;
            Vector2 inflationPercentage = new Vector2(
                sparkDrawSize.X == 0 ? 0 : inflationAmount.X / sparkDrawSize.X,
                sparkDrawSize.Y == 0 ? 0 : inflationAmount.Y / sparkDrawSize.Y);

            foreach (var spark in sparks)
            {
                if (spark.InvalidationId == unused_invalidation_id)
                    continue;

                if (spark.Time + sparkLifetime < time)
                    continue;

                float alpha = Interpolation.ValueAt(time, initial_spark_alpha, 0, spark.Time, spark.Time + sparkLifetime);

                // TODO: we should interpolate color for each vertex here,
                // but since color tinting should never happen to legacy judgements in practice,
                // and the reason we apply DrawColourInfo is that the whole judgement's alpha may changes during a fade animation, 
                // we just sample the average color once for all vertices to make alpha correct.
                var colour = (spark.Color * averageColorLinear).MultiplyAlpha(alpha);

                Quad quad = new RectangleF(
                    spark.Position - (sparkDrawSize / 2),
                    sparkDrawSize).Inflate(inflationAmount);

                renderer.DrawQuad(
                    renderer.WhitePixel,
                    quad * DrawInfo.Matrix,
                    colour,
                    null,
                    null,
                    inflationPercentage
                );
            }

            shader.Unbind();
        }
    }
}
