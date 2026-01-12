namespace osu.Plugin.LegacyExperience.Buttons;

public partial class LegacySpriteToggleButton : LegacySpriteStatedButton<bool>
{
    public string? ToggledTexture { get; set; }

    public string? DefaultTexture
    {
        get => Texture;
        set => Texture = value;
    }

    protected override string? GetTextureNameForState(bool state)
        => state ? ToggledTexture : Texture;

    protected override bool GetNewState(bool currentState) => !currentState;
}
