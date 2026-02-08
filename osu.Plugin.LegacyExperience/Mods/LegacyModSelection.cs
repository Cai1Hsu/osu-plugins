using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Plugin.LegacyExperience.Localisations;

namespace osu.Plugin.LegacyExperience.Mods;

[Cached(typeof(IModHoverManager))]
public partial class LegacyModSelection : LegacyDialog, IModHoverManager
{
    public OsuSpriteText MultiplierText { get; private set; } = null!;

    public SelectionGroup ReductionGroup { get; private set; } = null!;

    public SelectionGroup IncreaseGroup { get; private set; } = null!;

    public SelectionGroup SpecialGroup { get; private set; } = null!;

    public LegacyModSelection()
    {
        TitleText.Text = LegacyStrings.ModSelection_Title;

        Content.AddRange(new Drawable[]
        {
            MultiplierText = new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Font = OsuFont.Default.With(size: 30f * LegacyExperiencePlugin.StableRatio),
                // match stable's currentVerticalSpace usage
                Margin = new MarginPadding
                {
                    Top = 30 * LegacyExperiencePlugin.StableRatio,
                    Bottom = 9 * LegacyExperiencePlugin.StableRatio,
                },
            },
            ReductionGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Reduction,
                    Colour = Colour4.LimeGreen,
                },
            },
            IncreaseGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Increase,
                    Colour = Colour4.OrangeRed,
                },
            },
            SpecialGroup = new SelectionGroup
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Label =
                {
                    Text = LegacyStrings.ModSelection_Special,
                    Colour = Colour4.White,
                },
            },
        });
    }

    private double lastHoverSampleTime = double.MinValue;
    private const double hoverSampleDebounceTime = 50;

    bool IModHoverManager.RequestHoverSample()
    {
        double currentTime = Time.Current;

        if (currentTime - lastHoverSampleTime < hoverSampleDebounceTime)
            return false;

        lastHoverSampleTime = currentTime;
        return true;
    }
}
