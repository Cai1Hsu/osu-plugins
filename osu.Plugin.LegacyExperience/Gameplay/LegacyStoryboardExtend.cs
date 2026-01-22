using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Plugins;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards.Drawables;
using static osu.Game.Storyboards.Drawables.DrawableStoryboardLayer;

namespace osu.Plugin.LegacyExperience.Gameplay;

/// <summary>
/// Adds extended support for storyboard that mimics stable's behavior.
/// </summary>
public partial class LegacyStoryboardExtend : CompositeDrawable, ISerialisableDrawable
{
    public bool UsesFixedAnchor { get; set; } = true;

    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    [Resolved]
    private Player? player { get; set; }

    public LegacyStoryboardExtend()
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (drawable_storyboard_field is null || LayerElementContainer_getter is null)
        {
            Logger.Log("Failed to apply LegacyStoryboardExtend because of missing reflection field. Consider reporting this issue.", level: LogLevel.Error);
            return;
        }

        if (player?.DimmableStoryboard is not DimmableStoryboard dimmableStoryboard)
            return;

        var drawable_storyboard = drawable_storyboard_field.GetValue(dimmableStoryboard) as DrawableStoryboard;

        if (drawable_storyboard is null)
            return;

        handleBackgroundLayer(drawable_storyboard.Children.FirstOrDefault(l => l.Name == background_layer_name));
    }

    private readonly static FieldInfo drawable_storyboard_field = typeof(DimmableStoryboard)
        .GetField("drawableStoryboard", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly static MethodInfo LayerElementContainer_getter = typeof(DrawableStoryboardLayer)
        .GetProperty("ElementContainer", BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetGetMethod(nonPublic: true)!;

    private const string background_layer_name = "Background";

    private Drawable? backgroundSprite;

    private void handleBackgroundLayer(DrawableStoryboardLayer? backgroundLayer)
    {
        if (backgroundLayer is null)
            return;

        var elementContainer = LayerElementContainer_getter.Invoke(backgroundLayer, null) as LayerElementContainer;

        if (elementContainer is null)
            return;

        string backgroundFile = beatmap.Value.Beatmap.Metadata?.BackgroundFile ?? string.Empty;

        // TODO: I'm not sure if this is enough.
        if (string.IsNullOrEmpty(backgroundFile))
            return;

        // FIXME: 
        // `elementContainer` is a LifetimeManagementContainer which doesn't support remove child when dead.
        // When creating multiple instances of LegacyStoryboardExtend, multiple BackgroundSprites are added to the container,
        // and never removed, causing memory leak.
        // We need to figure out a way to at least avoid adding multiple BackgroundSprites.
        LoadComponentAsync(backgroundSprite = new BackgroundSprite(beatmap.Value), elementContainer.AddInternal);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        // at least make it invisible
        backgroundSprite?.Expire();
    }

    private partial class BackgroundSprite : Sprite
    {
        private readonly WorkingBeatmap workingBeatmap;

        public override bool RemoveWhenNotAlive => false;

        public BackgroundSprite(WorkingBeatmap working)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            FillMode = FillMode.Fit;
            RelativeSizeAxes = Axes.Both;

            workingBeatmap = working;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Texture = workingBeatmap.GetBackground();

            Name = workingBeatmap.Beatmap.Metadata?.BackgroundFile is string f ? $"{f}" : "Beatmap Background";
        }
    }
}
