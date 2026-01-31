using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osuTK.Graphics.ES30;
using osuTK;
using osu.Framework.Extensions.EnumExtensions;

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

    [BackgroundDependencyLoader]
    private void load(ShaderManager shaders)
    {
        shader = shaders.Load(@"LegacyJudgements", "LegacyJudgements");

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

        private IVertexBatch<JudgementSparkVertex> vertexBatch = null!;
        private IUniformBuffer<JudgementsParameters> parametersBuffer = null!;

        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);

            // no need to draw anything. also prevent divide by zero in shader.
            if (sparkLifetime <= 0)
                return;

            vertexBatch ??= renderer.CreateQuadBatch<JudgementSparkVertex>(max_sparks, 1);
            parametersBuffer ??= renderer.CreateUniformBuffer<JudgementsParameters>();
            parametersBuffer.Data = parametersBuffer.Data with
            {
                Time = time,
                SparkLifetime = sparkLifetime,
            };

            shader.Bind();
            shader.BindUniformBlock(@"m_JudgementsParameters", parametersBuffer);

            renderer.SetBlend(BlendingParameters.Additive);
            renderer.PushLocalMatrix(DrawInfo.Matrix);

            Vector2 halfSparkSize = sparkDrawSize / 2;

            var averageColorLinear = DrawColourInfo.Colour.AverageColour.Linear;

            foreach (var spark in sparks)
            {
                if (spark.InvalidationId == unused_invalidation_id)
                    continue;

                if (spark.Time + sparkLifetime < time)
                    continue;

                // TODO: we should interpolate color for each vertex here,
                // but since color tinting should never happen to legacy judgements in practice,
                // and the reason we apply DrawColourInfo is that the whole judgement's alpha may changes during a fade animation, 
                // we just sample the average color once for all vertices to make alpha correct.
                var color = (spark.Color * averageColorLinear).MultiplyAlpha(initial_spark_alpha);

                // bottom left
                vertexBatch.Add(new JudgementSparkVertex
                {
                    Position = new Vector2(spark.Position.X - halfSparkSize.X, spark.Position.Y + halfSparkSize.Y),
                    Color = color,
                    Time = spark.Time
                });

                // bottom right
                vertexBatch.Add(new JudgementSparkVertex
                {
                    Position = new Vector2(spark.Position.X + halfSparkSize.X, spark.Position.Y + halfSparkSize.Y),
                    Color = color,
                    Time = spark.Time
                });

                // top right
                vertexBatch.Add(new JudgementSparkVertex
                {
                    Position = new Vector2(spark.Position.X + halfSparkSize.X, spark.Position.Y - halfSparkSize.Y),
                    Color = color,
                    Time = spark.Time
                });

                // top left
                vertexBatch.Add(new JudgementSparkVertex
                {
                    Position = new Vector2(spark.Position.X - halfSparkSize.X, spark.Position.Y - halfSparkSize.Y),
                    Color = color,
                    Time = spark.Time
                });
            }

            vertexBatch.Draw();
            renderer.PopLocalMatrix();

            shader.Unbind();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            vertexBatch?.Dispose();
            parametersBuffer?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct JudgementsParameters
        {
            public UniformFloat Time;
            public UniformFloat SparkLifetime;
            private readonly UniformPadding8 padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JudgementSparkVertex : IVertex, IEquatable<JudgementSparkVertex>
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Colour4 Color;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Time;

            public bool Equals(JudgementSparkVertex other)
            {
                return Position.Equals(other.Position) &&
                    Color.Equals(other.Color) &&
                    Time.Equals(other.Time);
            }
        }
    }
}
