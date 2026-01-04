using osu.Framework.Graphics.Containers;

namespace osu.Plugin.LegacyLeaderboard;

public partial class LegacyLeaderboardBase : CompositeDrawable
{
    private const int max_entries = 6;

    public static float GetEntryAlpha(int index, int itemCount = max_entries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        return MathF.Max(0f, 0.8f - (float)index / itemCount * 0.3f);
    }
}
