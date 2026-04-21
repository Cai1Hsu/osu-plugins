using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Plugin.Patches;

public partial class PatchesConfigManager : IniConfigManager<PatchesConfig>
{
    public PatchesConfigManager(Storage storage)
        : base(storage)
    {
    }

    protected override string Filename => "PatchesConfig.ini";

    protected override void InitialiseDefaults()
    {
        base.InitialiseDefaults();

        SetDefault(PatchesConfig.UnlimitedFps, false);
    }
}

public enum PatchesConfig
{
    /// <summary>
    /// When enabled and frame sync is set to unlimited, removes the FPS cap and allows the game to run at higher frame rates if the hardware allows it.
    /// </summary>
    UnlimitedFps,
}
