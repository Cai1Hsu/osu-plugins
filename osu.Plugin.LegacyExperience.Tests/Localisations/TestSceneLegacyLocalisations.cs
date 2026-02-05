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

    [BackgroundDependencyLoader]
    private void load(Storage storage)
    {
        FillFlowContainer stringsContainer;

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

        foreach (var key in legacy_strings)
        {
            stringsContainer.Add(new OsuSpriteText
            {
                Text = LocalisableString.Format("{0}={1}", key,
                    new TranslatableString(LegacyLocalisationManager.GetKey(key), @"LOAD FAILED")),
            });
        }
    }

    // some samples of strings that were used in osu!stable
    private static readonly string[] legacy_strings = new[]
    {
        "Lets_Do_This",
        "General_Cancel",
        "General_Confirm",
        "General_Back",
        "General_Never",
        "General_Always",
        "Options_Audio_Effect",
        "Options_Audio_Master",
        "Options_Audio_Music",
        "Options_Audio_Offset",
        "Options_Audio_OffsetWizard",
        "Options_Audio_Offset_Description",
        "Options_Audio_Volume",
        "Options_DeleteAllUnrankedMaps",
        // "Options_DeleteWarning", // this string requires a parameter 
        "Options_ForceFolderPermissions",
        "Options_ForceFolderPermissions_Successful",
        "Options_ForceFolderPermissions_Tooltip",
        "Options_Graphics_Combo",
        "Options_Graphics_Combo_Tooltip",
        "Options_Graphics_CustomResolution",
        "Options_Graphics_ResolutionBorderless",
        "Options_Graphics_Detail",
        "Options_Graphics_DirectX_Tooltip",
        "Options_Graphics_Fire",
        "Options_Graphics_Fire_Tooltip",
        "Options_Graphics_FpsCounter",
        "Options_Graphics_FpsCounter_Tooltip",
        "Options_Graphics_LowEnd_Tooltip",
    };
}
