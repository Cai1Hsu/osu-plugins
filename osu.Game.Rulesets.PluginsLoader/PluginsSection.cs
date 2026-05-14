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

    private readonly SortedList<string, PluginSubsection> pluginSubsections = new SortedList<string, PluginSubsection>(StringComparer.OrdinalIgnoreCase);

    public void Add(PluginSubsection pluginSubsection)
    {
        // avoid plugins with the same name
        var key = $"{pluginSubsection.DisplayName}, {pluginSubsection.GetType().AssemblyQualifiedName}";

        pluginSubsections.Add(key, pluginSubsection);
        FlowContent.Add(pluginSubsection);

        int position = 0;

        foreach (var (k, v) in pluginSubsections)
            FlowContent.SetLayoutPosition(v, position++);
    }
}
