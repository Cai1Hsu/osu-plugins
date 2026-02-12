using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Plugins;
using osu.Game.Skinning;
using osu.Plugin.LegacyExperience.Audio;
using osuTK;

namespace osu.Plugin.LegacyExperience;

public partial class LegacyButton : ClickableContainer
{
    public Colour4 BackgroundColour
    {
        get => field;
        set
        {
            field = value;
            updateColour();
        }
    }

    private Colour4 originalColour
    {
        get
        {
            var @base = Enabled.Value ? BackgroundColour : Colour4.Gray;
            return new Colour4(
                Math.Max(0, @base.R - (20 / 255f)),
                Math.Max(0, @base.G - (20 / 255f)),
                Math.Max(0, @base.B - (20 / 255f)),
                1);
        }
    }

    private void updateColour()
    {
        backgroundContainer.Colour = originalColour;
        label.Colour = Enabled.Value ? Colour4.White : Colour4.LightGray;
    }

    [Resolved]
    private TextureStore textures { get; set; } = null!;

    [Resolved]
    private ISkinSource? skin { get; set; }

    [Resolved]
    private AudioEngine audioEngine { get; set; } = null!;

    private OsuSpriteText label;

    private BufferedContainer backgroundContainer = null!;

    private readonly Vector2 dimensions;

    public LegacyButton(LocalisableString text, Vector2 dimensions, float? textSize = null)
    {
        this.dimensions = dimensions;
        Children = new Drawable[]
        {
            // We made a litte overlap to ensure the button textures can cover the whole button area without gaps.
            // However, this little overlap looks bad when alpha is not 0 or 1(blending issue).
            backgroundContainer = new BufferedContainer
            {
                RelativeSizeAxes = Axes.Both,
            },
            label = new OsuSpriteText
            {
                Text = text,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = Colour4.Purple,
                BypassAutoSizeAxes = Axes.Y,
                UseFullGlyphHeight = false,
            },
            Empty().With(d =>
            {
                d.Name = "Dimension Fitter";
                d.Size = dimensions * LegacyExperiencePlugin.StableRatio;
                d.Anchor = Anchor.Centre;
                d.Origin = Anchor.Centre;
            }),
        };
        BackgroundColour = Colour4.White;
        AutoSizeAxes = Axes.Both;

        textSize ??= 14f * dimensions.Y / 18f / ((text.ToString().IndexOf('\n') <= 0) ? 1 : 2); // wtf is this?
        label.Font = OsuFont.GetFont(size: textSize.Value * LegacyExperiencePlugin.StableRatio);

        Enabled.BindValueChanged(_ => updateColour(), true);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        backgroundContainer.AddRange(new Drawable[]
        {
            buttonMiddle = new Sprite(),
            buttonLeft = new Sprite(),
            buttonRight = new Sprite(),
        });

        updateSkin();
        skin?.SourceChanged += updateSkin;
    }

    private Sprite buttonLeft = null!;
    private Sprite buttonMiddle = null!;
    private Sprite buttonRight = null!;

    private void updateSkin()
    {
        Texture? getTexture(string name) => skin?.GetSkinTexture(name, textures, "UI");

        updateTexture(buttonLeft, getTexture("button-left"));
        updateTexture(buttonMiddle, getTexture("button-middle"));
        updateTexture(buttonRight, getTexture("button-right"));

        // we've packed the default textures with the plugin so there should always be valid textures
        // i don't want to handle NRE/DivideByZero exceptions here, so just log an error if the textures are missing and skip the layout update.
        try
        {
            float leftScale = dimensions.Y / (buttonLeft.DrawHeight * 0.625f);
            float rightScale = dimensions.Y / (buttonRight.DrawHeight * 0.625f);
            buttonLeft.Scale = new Vector2(leftScale);
            buttonRight.Scale = new Vector2(rightScale);

            float leftWidth = buttonLeft.DrawWidth * 0.625f * leftScale;
            buttonRight.Position = new Vector2(dimensions.X - leftWidth, 0) * LegacyExperiencePlugin.StableRatio;

            float middlePositionX = leftWidth * LegacyExperiencePlugin.StableRatio;
            float middleScaleX = (dimensions.X - leftWidth * 2f) / (float)buttonMiddle.Texture.DisplayWidth * LegacyExperiencePlugin.StableRatio;

            // on lazer there's a weird 1px gap between the middle and the left/right textures
            // we do a compensation by slightly increasing the scale of the middle texture and moving it a bit to the left
            middlePositionX -= 0.5f;
            middleScaleX += 2f / dimensions.X;

            buttonMiddle.Position = new Vector2(middlePositionX, 0);
            buttonMiddle.Scale = new Vector2(middleScaleX, rightScale);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update button textures. Please report this to the plugin developer.");
        }
    }

    void updateTexture(Sprite sprite, Texture? texture)
    {
        sprite.Texture = texture;
        sprite.Size = texture?.DisplaySize ?? Vector2.Zero;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!Enabled.Value)
            return false;

        audioEngine.Click(sample: LegacySample.click_short);
        backgroundContainer.FadeColour(BackgroundColour, 50);
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        backgroundContainer.FadeColour(originalColour, 50);
        base.OnHoverLost(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        // Action invoked in base.OnClick, so we want to make sure the sound and visual feedback are triggered.
        if (Enabled.Value)
        {
            audioEngine.PlaySample(sample: LegacySample.click_short_confirm);
            backgroundContainer.FlashColour(Colour4.White, 400);
        }

        return base.OnClick(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);

        skin?.SourceChanged -= updateSkin;
    }
}
