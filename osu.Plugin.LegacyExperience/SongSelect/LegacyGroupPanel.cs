using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK;
using System.Diagnostics;
using osu.Game.Screens.SelectV2;
using osu.Framework.Localisation;

namespace osu.Plugin.LegacyExperience.SongSelect;

public partial class LegacyGroupPanel : LegacyPanel
{
    private OsuSpriteText titleText = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        // TODO: do we have to truncate?
        AddInternal(titleText = new OsuSpriteText()
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: 24f * LegacyExperiencePlugin.StableRatio),
            Position = new Vector2(15f, 0f) * LegacyExperiencePlugin.StableRatio,
            AllowMultiline = false,
        });

        Selected.BindValueChanged(_ => updatePanelState());
        Expanded.BindValueChanged(_ => updatePanelState(), true);
    }

    private void updatePanelState()
    {
        titleText.Colour = Expanded.Value || Selected.Value ? PanelColors.ActiveText : PanelColors.InactiveText;
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        Debug.Assert(Item is not null);

        titleText.Text = FormatGroupTitle(Item.Model) ?? string.Empty;
        titleText.Colour = PanelColors.InactiveText;
    }

    protected virtual LocalisableString? FormatGroupTitle(object? model)
    {
        var title = GetGroupTitle(model);

        if (title is null)
            return null;

        return LocalisableString.Format("{0} ({1} maps)", title, Item?.NestedItemCount ?? 0);
    }

    protected virtual LocalisableString? GetGroupTitle(object? model)
    {
        if (model is null)
            return null;

        if (model is string str)
            return str;

        if (model is RankDisplayGroupDefinition rankGroup)
            return GetGroupTitle(rankGroup);

        if (model is StarDifficultyGroupDefinition starGroup)
            return GetGroupTitle(starGroup);

        if (model is RankedStatusGroupDefinition rankedStatusGroup)
            return GetGroupTitle(rankedStatusGroup);

        if (model is GroupDefinition group)
            return GetGroupTitle(group);

        return $"Unsupported group type: {model.GetType()}";
    }

    protected virtual LocalisableString GetGroupTitle(RankDisplayGroupDefinition rankDisplayGroup)
    {
        return rankDisplayGroup.Title;
    }

    protected virtual LocalisableString GetGroupTitle(StarDifficultyGroupDefinition starGroup)
    {
        return starGroup.Title;
    }

    protected virtual LocalisableString GetGroupTitle(RankedStatusGroupDefinition rankedStatusGroup)
    {
        return rankedStatusGroup.Title;
    }

    protected virtual LocalisableString GetGroupTitle(GroupDefinition group)
    {
        return group.Title;
    }
}