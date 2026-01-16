// This file is adapted from osu!lazer's BeatmapCarousel test scene to test LegacyExperience's BeatmapCarousel integration.
// Original files:
// - https://github.com/ppy/osu/blob/master/osu.Game.Tests/Visual/SongSelectV2/BeatmapCarouselTestScene.cs
// - https://github.com/ppy/osu/blob/master/osu.Game.Tests/Visual/SongSelectV2/TestSceneBeatmapCarousel.cs

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using LegacyBeatmapCarousel = osu.Plugin.LegacyExperience.SongSelect.BeatmapCarousel;

namespace osu.Plugin.LegacyExperience.Tests.SongSelect;

public partial class TestSceneBeatmapCarousel : LocalSkinTestScene
{
    private SkinProvidingContainer skinContainer = null!;

    private LegacyBeatmapCarousel carousel = null!;
    private OsuTextFlowContainer stats = null!;

    [Cached(typeof(BeatmapStore))]
    private BeatmapStore store;

    [Cached]
    private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

    protected readonly BindableList<BeatmapSetInfo> BeatmapSets = new BindableList<BeatmapSetInfo>();

    public TestSceneBeatmapCarousel()
    {
        store = new TestBeatmapStore
        {
            BeatmapSets = { BindTarget = BeatmapSets }
        };

        BeatmapSets.BindCollectionChanged((b, n) =>
        {
            IEnumerable<BeatmapSetInfo>? newItems = n.NewItems?.Cast<BeatmapSetInfo>();
            IEnumerable<BeatmapSetInfo>? oldItems = n.OldItems?.Cast<BeatmapSetInfo>();
        });
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Dependencies.Cache(Realm);
    }

    [SetUpSteps]
    public void SetUpSteps()
    {
        var skin = new DefaultLegacySkin(this);
        skinContainer = new SkinProvidingContainer(skin);

        Add(skinContainer);
        Add(stats = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 14))
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Margin = new MarginPadding(10),
        });

        AddStep("Add carousel", () =>
        {
            carousel = new LegacyBeatmapCarousel
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 800,
                NewItemsPresented = _ => { },
                RequestSelection = b =>
                {
                    carousel.CurrentGroupedBeatmap = b;
                },
                RequestRecommendedSelection = groupedBeatmaps =>
                {
                },
            };

            skinContainer.Add(carousel);

            Schedule(() =>
            {
                carousel.Filter(new FilterCriteria { Sort = SortMode.Title });
            });
        });
    }

    private void updateStats()
    {
        if (carousel is null)
            return;

        stats.Clear();
        createHeader("beatmap store");
        stats.AddParagraph($"""
                                sets: {BeatmapSets.Count}
                                """);
        createHeader("carousel");
        stats.AddParagraph($"""
                                filtering: {carousel.IsFiltering} (total {carousel.FilterCount} times)
                                filtering: {carousel.IsFiltering} (total {carousel.FilterCount} times)
                                tracked: {carousel.ItemsTracked}
                                displayable: {carousel.DisplayableItems}
                                displayed: {carousel.VisibleItems}
                                selected: {carousel.CurrentGroupedBeatmap}
                                """);

        void createHeader(string text)
        {
            stats.AddParagraph(string.Empty);
            stats.AddParagraph(text, cp =>
            {
                cp.Font = cp.Font.With(size: 18, weight: FontWeight.Bold);
            });
        }
    }

    [Test]
    [Explicit]
    public void TestAddBeatmaps()
    {
        AddBeatmaps(1);
        AddBeatmaps(5);
        AddBeatmaps(10);
    }

    protected void AddBeatmaps(int count, int? fixedDifficultiesPerSet = null, bool randomMetadata = false) => AddStep($"add {count} beatmaps{(randomMetadata ? " with random data" : "")}", () =>
    {
        var beatmaps = new List<BeatmapSetInfo>();

        for (int i = 0; i < count; i++)
            beatmaps.Add(CreateTestBeatmapSetInfo(fixedDifficultiesPerSet, randomMetadata));

        BeatmapSets.AddRange(beatmaps);

        updateStats();
    });


    protected static BeatmapSetInfo CreateTestBeatmapSetInfo(int? fixedDifficultiesPerSet, bool randomMetadata)
    {
        var beatmapSetInfo = TestResources.CreateTestBeatmapSetInfo(fixedDifficultiesPerSet ?? RNG.Next(1, 4));

        if (randomMetadata)
        {
            char randomCharacter = getRandomCharacter();

            var metadata = new BeatmapMetadata
            {
                // Create random metadata, then we can check if sorting works based on these
                Artist = $"{randomCharacter}ome Artist " + RNG.Next(0, 9),
                Title = $"{randomCharacter}ome Song (set id {beatmapSetInfo.OnlineID:000}) {Guid.NewGuid()}",
                Author = { Username = $"{randomCharacter}ome Guy " + RNG.Next(0, 9) },
            };

            foreach (var beatmap in beatmapSetInfo.Beatmaps)
                beatmap.Metadata = metadata.DeepClone();
        }

        return beatmapSetInfo;
    }


    private static long randomCharPointer;

    private static char getRandomCharacter()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz*";
        return chars[(int)((randomCharPointer++ / 2) % chars.Length)];
    }

    private partial class TestBeatmapStore : BeatmapStore
    {
        public readonly BindableList<BeatmapSetInfo> BeatmapSets = new BindableList<BeatmapSetInfo>();
        public override IBindableList<BeatmapSetInfo> GetBeatmapSets(CancellationToken? cancellationToken) => BeatmapSets.GetBoundCopy();
    }
}
