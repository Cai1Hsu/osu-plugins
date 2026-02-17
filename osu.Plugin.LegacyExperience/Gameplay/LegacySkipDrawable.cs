using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Audio;
using osuTK;

namespace osu.Plugin.LegacyExperience.Gameplay;

public partial class LegacySkipDrawable : CompositeDrawable, IKeyBindingHandler<GlobalAction>
{
    private const float stable_ratio = 1.6f;

    [Resolved]
    private TextureStore? textures { get; set; }

    [Resolved]
    private ISkinSource? skinSource { get; set; }

    public Action? SkipRequested { get; set; }

    [Resolved]
    private AudioEngine audioEngine { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        var firstFrame = skinSource?.GetTexture("play-skip-0");

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
        activated();
        return true;
    }

    private void activated()
    {
        audioEngine.PlaySamplePositional(LegacySample.menuhit, null);
        SkipRequested?.Invoke();
    }

    public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
    {
        if (e.Repeat)
            return false;

        if (e.Action is GlobalAction.SkipCutscene)
        {
            activated();
            return true;
        }

        return false;
    }

    public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
    {
    }
}
