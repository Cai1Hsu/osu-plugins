using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacySkipDrawable : CompositeDrawable
{
    private const float stable_ratio = 1.6f;

    [Resolved]
    private TextureStore? textures { get; set; }

    [Resolved]
    private ISkinSource? skinSource { get; set; }

    public Action? SkipRequested { get; set; }

    private static readonly SampleInfo menuhit_sample_info = new SampleInfo("menuhit");

    private PoolableSkinnableSample? clickSample;

    [BackgroundDependencyLoader]
    private void load()
    {
        var firstFrame = skinSource?.GetTexture("play-skip-0");

        // static 
        if (firstFrame is null)
        {
            InternalChild = new Sprite()
            {
                Texture = skinSource?.GetSkinTexture("play-skip", textures, "UI"),
            };

            AutoSizeAxes = Axes.Both;
        }
        else
        {
            InternalChild = skinSource?.GetAnimation("play-skip", true, true, startAtCurrentTime: false, applyConfigFrameRate: true) ?? Empty();
            Size = firstFrame.DisplaySize;
        }

        Masking = true;

        Anchor = Anchor.TopRight;
        Origin = Anchor.BottomRight;

        Position = new Vector2(0, 480) * stable_ratio;
        Alpha = 0.6f;

        // TODO: Same as BreakOverlay, sample from skin wasn't used in test scene.
        AddInternal(clickSample = new PoolableSkinnableSample(menuhit_sample_info));
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.FadeTo(1, 300);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.FadeTo(0.6f, 300);
        base.OnHoverLost(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        SkipRequested?.Invoke();
        clickSample?.Play();
        return base.OnClick(e);
    }
}
