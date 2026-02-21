using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Screens.Menu;
using osu.Game.Input;

namespace osu.Plugin.LegacyExperience.Tests.Screens.Menu;

public partial class TestSceneButtonSystem : TestSceneWithBeatmap
{
    [Cached(typeof(IAmplitudesProvider))]
    private AmplitudesProvider amplitudesProvider = new AmplitudesProvider();

    [Cached]
    private IdleTracker idleTracker = new IdleTracker(1000);

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            amplitudesProvider,
            idleTracker,
            new ButtonSystem
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }
        });
    }
}
