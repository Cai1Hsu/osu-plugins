using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;

namespace osu.Plugin.LegacyExperience;

public interface IHasLegacyTooltip : IHasCustomTooltip<LocalisableString>
{
    LocalisableString TooltipText { get; }

    ITooltip<LocalisableString> IHasCustomTooltip<LocalisableString>.GetCustomTooltip() => new LegacyTooltip();
    LocalisableString IHasCustomTooltip<LocalisableString>.TooltipContent => TooltipText;
}
