using System.Collections.Frozen;

namespace osu.Plugin.LegacyExperience.Leaderboards;

public partial class ScoreEntrySpriteText : LegacySpriteText
{
    private const char comma = ',';
    private const char dot = '.';
    private const char percent = '%';

    private static readonly FrozenDictionary<char, string> mappings = new Dictionary<char, string>
    {
        { comma, "comma" },
        { dot, "dot" },
        { percent, "percent" },
    }.ToFrozenDictionary();

    public ScoreEntrySpriteText()
        : base("scoreentry")
    {
        CustomMappings = mappings;
    }
}
