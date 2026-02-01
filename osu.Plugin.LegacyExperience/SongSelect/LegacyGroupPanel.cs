using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK;
using System.Diagnostics;
using osu.Game.Screens.SelectV2;
using osu.Framework.Localisation;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;
using osu.Framework.Extensions.LocalisationExtensions;

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

    protected override void SkinChanged()
    {
        base.SkinChanged();

        Scheduler.Add(updatePanelState);
    }

    private void updatePanelState()
    {
        titleText.Colour = Expanded.Value || Selected.Value ? PanelColors.ActiveText : PanelColors.InactiveText;
    }

    public override MenuItem[]? ContextMenuItems
    {
        get
        {
            if (Item is null)
                return Array.Empty<MenuItem>();

            return new MenuItem[]
            {
                new OsuMenuItem(Expanded.Value
                    ? WebCommonStrings.ButtonsCollapse.ToSentence()
                    : WebCommonStrings.ButtonsExpand.ToSentence(), MenuItemType.Highlighted, () => TriggerClick())
            };

        }
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

    protected override Colour4 GetBackgroundColor()
    {
        if (Selected.Value || Expanded.Value)
            return PanelColors.Active;

        // TODO: stable's behavior:
        // 
        // base.UnselectedColour = ContainsCurrent 
        //      ? BeatmapTreeItem.colourRootInactiveContainsCurrent
        //      : BeatmapTreeItem.colourRootInactive;
        //
        // I haven't yet figured out how ContainsCurrent is different from Expanded/Selected.
        // So for now, we just use Inactive color.

        return PanelColors.Inactive;
    }
}