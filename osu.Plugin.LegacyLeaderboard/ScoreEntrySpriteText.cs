using System.Collections.Frozen;
using osu.Game.Plugins.Legacy;

namespace osu.Plugin.LegacyLeaderboard;

public partial class ScoreEntrySpriteText : LegacySpriteTextContainer
{
    private const char comma = ',';
    private const char dot = '.';

    private static readonly FrozenDictionary<char, string> mappings = new Dictionary<char, string>
    {
        { comma, "comma" },
        { dot, "dot" },
    }.ToFrozenDictionary();

    public ScoreEntrySpriteText()
        : base("scoreentry")
    {
        FontHeight = 14;
        CustomMappings = mappings;
    }
}
