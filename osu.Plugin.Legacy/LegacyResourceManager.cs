using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;

namespace osu.Game.Plugins.Legacy;

public partial class LegacyResourceManager : Drawable
{
    [BackgroundDependencyLoader]
    private void load(OsuGameBase game)
    {
        var resources = game.Resources;
        resources.AddStore(new DllResourceStore(typeof(LegacyResources).Assembly));
    }
}
