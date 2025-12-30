using osu.Framework.Graphics.Sprites;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Rulesets.PluginsLoader;

internal partial class PluginNotification : SimpleNotification
{
    public PluginNotification()
    {
        Icon = FontAwesome.Solid.PuzzlePiece;
    }
}