using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osu.Game.Tests.Visual;
using osu.Plugin.LegacyExperience.Localisations;
using osuTK;
using LazerLanguage = osu.Game.Localisation.Language;

namespace osu.Plugin.LegacyExperience.Tests.Localisations;

public partial class TestSceneLegacyLocalisations : OsuTestScene
{
    [Resolved]
    private OsuGameBase game { get; set; } = null!;

    [Resolved]
    private LegacyLocalisationManager legacyLocalisationManager { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load(Storage storage)
    {
        FillFlowContainer stringsContainer;

        OsuTextFlowContainer cultureInfoText = null!;

        Add(new FillFlowContainer
        {
            Margin = new MarginPadding { Top = 20 },
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            AutoSizeAxes = Axes.Y,
            Width = 500,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Children = new Drawable[]
            {
                cultureInfoText = new OsuTextFlowContainer()
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                },
                new SettingsButton
                {
                    Text = "Open game storage folder",
                    Action = () => storage.PresentExternally(),
                },
                new SettingsEnumDropdown<LazerLanguage>
                {
                    LabelText = "Game Language",
                    Current = game.CurrentLanguage,
                    AlwaysShowSearchBar = true,
                },
                new OsuScrollContainer(Direction.Vertical)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 500,
                    ScrollbarAnchor = Anchor.CentreRight,
                    ScrollbarVisible = true,
                    Child = stringsContainer = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        RelativeSizeAxes = Axes.X,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                    },
                },
            }
        });

        foreach (var (key, data) in legacy_strings)
        {
            stringsContainer.Add(new OsuSpriteText
            {
                Text = LocalisableString.Format("{0}={1}", key,
                    new TranslatableString(LegacyLocalisationManager.GetKey(key), @"LOAD FAILED", data)),
            });
        }
        
        legacyLocalisationManager.CurrentLegacyLanguage.BindValueChanged(lang =>
        {
            var cultureInfo = lang.NewValue.GetEffectiveCultureInfo();

            cultureInfoText.Clear();
            cultureInfoText.AddParagraph($"Current Legacy Language: {lang.NewValue}\n"
                + $"Culture Info: {cultureInfo.Name} - {cultureInfo.EnglishName} - {cultureInfo.NativeName}");
        }, true);
    }

    private static (string, object?[]) entry(string key, params object?[] args) => (key, args);

    // some samples of strings that were used in osu!stable
    private static readonly (string, object?[])[] legacy_strings = new[]
    {
        entry("Options_LoggedIn", Environment.UserName),
        entry("Lets_Do_This"),
        entry("General_Cancel"),
        entry("General_Confirm"),
        entry("General_Back"),
        entry("General_Never"),
        entry("General_Always"),
        entry("Options_Audio_Effect"),
        entry("Options_Audio_Master"),
        entry("Options_Audio_Music"),
        entry("Options_Audio_Offset"),
        entry("Options_Audio_OffsetWizard"),
        entry("Options_Audio_Offset_Description"),
        entry("Options_Audio_Volume"),
        entry("Options_DeleteAllUnrankedMaps"),
        entry("Options_DeleteWarning", 42),
        entry("Options_ForceFolderPermissions"),
        entry("Options_ForceFolderPermissions_Successful"),
        entry("Options_ForceFolderPermissions_Tooltip"),
        entry("Options_Graphics_Combo"),
        entry("Options_Graphics_Combo_Tooltip"),
        entry("Options_Graphics_CustomResolution"),
        entry("Options_Graphics_ResolutionBorderless"),
        entry("Options_Graphics_Detail"),
        entry("Options_Graphics_DirectX_Tooltip"),
        entry("Options_Graphics_Fire"),
        entry("Options_Graphics_Fire_Tooltip"),
        entry("Options_Graphics_FpsCounter"),
        entry("Options_Graphics_FpsCounter_Tooltip"),
        entry("Options_Graphics_LowEnd_Tooltip"),
        entry("Player_ToggleScoreboard", "Tab"),
    };
}
