using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Game.Scoring;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osu.Game.Plugins;
using osuTK;
using System.Collections.Frozen;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyRankSpritePool : CompositeDrawable
{
    private const int initial_pool_size = 10;

    private FrozenDictionary<ScoreRank, RankPool> pools = FrozenDictionary<ScoreRank, RankPool>.Empty;

    [BackgroundDependencyLoader]
    private void load()
    {
        var rankPools = new Dictionary<ScoreRank, RankPool>();

        // TODO: SS, SH, X, XH ranks are generally rare,
        // consider having lower initial sizes or using lazy initialization.
        foreach (var rank in (ScoreRank[])Enum.GetValues(typeof(ScoreRank)))
        {
            if (rank < ScoreRank.D)
                continue;

            var pool = new RankPool(rank, initial_pool_size);
            rankPools.Add(rank, pool);
            AddInternal(pool);
        }

        this.pools = rankPools.ToFrozenDictionary();
    }

    public LegacyRankSprite Get(ScoreRank rank)
    {
        if (pools.TryGetValue(rank, out var pool))
            return pool.Get();

        throw new InvalidOperationException($"No pool found for rank {rank}");
    }

    private partial class RankPool : DrawablePool<LegacyRankSprite>
    {
        private readonly ScoreRank rank;

        public ScoreRank Rank => rank;

        public RankPool(ScoreRank rank, int initialSize, int? maximumSize = null)
            : base(initialSize, maximumSize)
        {
            this.rank = rank;
        }

        protected override LegacyRankSprite CreateNewDrawable()
            => new LegacyRankSprite(rank);
    }


    public partial class LegacyRankSprite : PoolableDrawable
    {
        [Obsolete("Use LegacyRankSprite(ScoreRank rank) instead.", true)]
        public LegacyRankSprite()
        {
        }

        private readonly ScoreRank rank;

        public LegacyRankSprite(ScoreRank rank)
        {
            this.rank = rank;
        }

        public ScoreRank Rank => rank;

        [Resolved]
        private ISkinSource? skin { get; set; }

        [Resolved]
        private TextureStore? textures { get; set; }

        private Sprite sprite = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;

            InternalChild = sprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };

            if (skin is not null)
                skin.SourceChanged += updateTexture;

            updateTexture();
        }

        private void updateTexture()
        {
            Texture? texture = skin?.GetSkinTexture(getRankTextureLookup(rank), textures, "UI");
            sprite.Texture = texture;
            sprite.Size = texture is null ? Vector2.Zero : texture.DisplaySize;
        }

        private static string getRankTextureLookup(ScoreRank rank) => rank switch
        {
            ScoreRank.XH => "ranking-XH-small",
            ScoreRank.X => "ranking-X-small",
            ScoreRank.SH => "ranking-SH-small",
            ScoreRank.S => "ranking-S-small",
            ScoreRank.A => "ranking-A-small",
            ScoreRank.B => "ranking-B-small",
            ScoreRank.C => "ranking-C-small",
            ScoreRank.D => "ranking-D-small",
            _ => string.Empty,
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin is not null)
                skin.SourceChanged -= updateTexture;
        }
    }
}
