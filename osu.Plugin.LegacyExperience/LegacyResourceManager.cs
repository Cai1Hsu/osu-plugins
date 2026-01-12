using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;
using osu.Game;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyResourceManager : Drawable
{
    [BackgroundDependencyLoader]
    private void load(OsuGameBase game)
    {
        var resources = game.Resources;
        resources.AddStore(new DllResourceStore(LegacyResources.ResourceAssembly));
    }
}
