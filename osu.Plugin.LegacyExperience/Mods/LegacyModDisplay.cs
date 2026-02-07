using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Plugins;
using osu.Game.Skinning;

namespace osu.Plugin.LegacyExperience.Mods;

public partial class LegacyModDisplay : Sprite
{
    public LegacyMod Mod { get; }

    public LegacyModDisplay(LegacyMod mod)
    {
        Mod = mod;
    }

    [Resolved]
    private TextureStore textures { get; set; } = null!;

    [Resolved]
    private ISkinSource? skin { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        updateTexture();
        skin?.SourceChanged += updateTexture;
    }

    private void updateTexture()
    {
        sampleHover = skin?.GetSample(sampleHoverInfo);

        var texture = skin.GetSkinTexture($"selection-mod-{textureName}", textures, "UI");

        Debug.Assert(texture is not null); // we've packed default icons, so this should never be null.

        Texture = texture;
        Size = Texture.DisplaySize;
    }

    private string textureName => Mod.ToString().ToLowerInvariant();

    private static readonly SampleInfo sampleHoverInfo = new SampleInfo("click-short");

    private ISample? sampleHover;

    protected override bool OnHover(HoverEvent e)
    {
        sampleHover?.Play();
        return base.OnHover(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateTexture;
    }
}
