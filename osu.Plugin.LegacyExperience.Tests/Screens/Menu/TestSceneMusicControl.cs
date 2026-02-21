using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Plugin.LegacyExperience.Screens.Menu;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneMusicControl : TestSceneWithBeatmap
{
    private MusicControl control = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(0.2f),
                },
                control = new MusicControl
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
            }
        });
    }
}
