using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.PluginsLoader;

public partial class PluginsSection : SettingsSection
{
    public override LocalisableString Header => "Plugins";

    public override Drawable CreateIcon() => new SpriteIcon
    {
        Icon = FontAwesome.Solid.PuzzlePiece,
    };
}
