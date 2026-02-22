using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Plugins;
using osu.Plugin.LegacyExperience.Audio;

namespace osu.Plugin.LegacyExperience.Screens.Menu;

internal partial class MenuButton : CompositeDrawable
{
    public new required string Name { get; init; }
    public LegacySample HoverSample { get; set; } = LegacySample.menuclick;
    public LegacySample ClickSample { get; set; } = LegacySample.menuhit;
    public Action? Action { get; set; }

    private Sprite hoverSprite = null!;
    private Container spriteContainer = null!;

    [Resolved]
    private AudioEngine audioEngine { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var textureKey = $"UI/menu-button-{Name}";
        var hoverTextureKey = $"{textureKey}-over";

        AutoSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            spriteContainer = new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        Texture = textures.GetAutoSized(textureKey)
                    },
                    hoverSprite = new Sprite
                    {
                        Alpha = 0,
                        Texture = textures.GetAutoSized(hoverTextureKey),
                        BypassAutoSizeAxes = Axes.Both,
                    }
                }
            },
        };

        // i didn't find any "-hover" suffixed samples but stable has this logic so here we are
        if (HoverSample is LegacySample.menuclick
            && Enum.TryParse<LegacySample>($"menu_{Name}_hover", false, out var hover))
            HoverSample = hover;

        if (ClickSample is LegacySample.menuhit
            && Enum.TryParse<LegacySample>($"menu_{Name}_click", false, out var click))
            ClickSample = click;

        // i donno if this should be here but whatever
        if (Name is "back")
            ClickSample = LegacySample.menuback;
    }

    protected override bool OnHover(HoverEvent e)
    {
        audioEngine.PlaySamplePositional(HoverSample, null);
        spriteContainer.MoveToX(20 * LegacyExperiencePlugin.StableRatio, 580, Easing.OutElastic);
        hoverSprite.FadeIn(30, Easing.None);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        spriteContainer.MoveToX(0, 400, Easing.OutCubic);
        hoverSprite.FadeOut(500, Easing.None);
    }

    protected override bool OnClick(ClickEvent e)
    {
        audioEngine.PlaySamplePositional(ClickSample, null);
        Action?.Invoke();
        return true;
    }
}
