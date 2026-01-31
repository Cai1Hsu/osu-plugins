using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.SongSelect;

public class LegacyPanelColors
{
    private static readonly SkinCustomColourLookup activeTextLookup = new("SongSelectActiveText");
    private static readonly SkinCustomColourLookup inactiveTextLookup = new("SongSelectInactiveText");

    public static LegacyPanelColors CreateDefault() => new LegacyPanelColors
    {
        ActiveText = Color4.Black,
        InactiveText = Color4.White,
        InactiveTextFaded = Color4.White.Opacity(0.5f),
    };

    public void SyncFromSkin(ISkinSource? skin)
    {
        ActiveText = skin?.GetConfig<SkinCustomColourLookup, Color4>(activeTextLookup)?.Value ?? Color4.Black;
        InactiveText = skin?.GetConfig<SkinCustomColourLookup, Color4>(inactiveTextLookup)?.Value ?? Color4.White;
        InactiveTextFaded = InactiveText.Opacity(0.5f);
    }

    private static readonly Color4 Colour_Active = new Color4(163, 240, 44, 255);
    private static readonly Color4 Colour_Inactive = new Color4(35, 50, 143, 255);
    private static readonly Color4 Colour_InactiveSelected = new Color4(35, 90, 193, 255);
    private static readonly Color4 Colour_Orange = new Color4(233, 104, 0, 240);
    private static readonly Color4 Colour_Pink = new Color4(235, 73, 153, 240);
    private static readonly Color4 Colour_Blue = new Color4(0, 150, 236, 240);
    private static readonly Color4 Colour_LightBlue = Lighten2(Colour_Blue, 0.3f);
    private static readonly Color4 Colour_White = new Color4(255, 255, 255, 220);
    private static readonly Color4 Colour_NewBeatmap = Color4.MediumSlateBlue.Opacity(240 / 255f);
    private static readonly Color4 Colour_InactiveCover = new Color4(50, 50, 50, 255);

    public Color4 Active => Colour_Active;
    public Color4 Inactive => Colour_Inactive;
    public Color4 InactiveSelected => Colour_InactiveSelected;
    public Color4 Orange => Colour_Orange;
    public Color4 Pink => Colour_Pink;
    public Color4 Blue => Colour_Blue;
    public Color4 LightBlue => Colour_LightBlue;
    public Color4 White => Colour_White;
    public Color4 NewBeatmap => Colour_NewBeatmap;
    public Color4 InactiveCover => Colour_InactiveCover;
    public Color4 ActiveText { get; private set; }
    public Color4 InactiveText { get; private set; }
    public Color4 InactiveTextFaded { get; private set; }

    private static Color4 Lighten2(Color4 color, float amount)
    {
        amount *= 0.5f;

        // TODO: investigate if this is correct
        return new Color4(
            Math.Min(1f, color.R * (1f + 0.5f * amount) + amount),
            Math.Min(1f, color.G * (1f + 0.5f * amount) + amount),
            Math.Min(1f, color.B * (1f + 0.5f * amount) + amount),
            color.A);
    }
}