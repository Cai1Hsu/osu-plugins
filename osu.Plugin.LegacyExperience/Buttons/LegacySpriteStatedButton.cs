using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;

namespace osu.Plugin.LegacyExperience.Buttons;

public abstract partial class LegacySpriteStatedButton<TState> : LegacySpriteButton
    where TState : struct
{
    public readonly Bindable<TState> State = new Bindable<TState>();

    [BackgroundDependencyLoader]
    private void load()
    {
        State.BindValueChanged(v =>
        {
            var textureName = GetTextureNameForState(v.NewValue);
            SetTexture(textureName);
        }, true);
    }

    protected override bool OnClick(ClickEvent e)
    {
        State.Value = GetNewState(State.Value);
        return base.OnClick(e);
    }

    protected abstract TState GetNewState(TState currentState);

    protected abstract string? GetTextureNameForState(TState state);
}
