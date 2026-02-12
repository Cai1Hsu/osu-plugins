using osu.Game.Users;
using static osu.Game.Users.UserActivity;

namespace osu.Plugin.LegacyExperience.Online;

partial class LegacyUserPanel
{
    /// <summary>
    /// Get the legacy user status and beatmap string based on user activity and status.
    /// </summary>
    /// <param name="status">The user status.</param>
    /// <param name="activity">The user activity.</param>
    /// <returns>A tuple containing the legacy user status and beatmap string.</returns>
    public static (LegacyUserStatus, string?) GetLegacyUserStatusAndBeatmap(UserStatus status, UserActivity? activity)
    {
        if (status is UserStatus.Offline)
            return (LegacyUserStatus.Unknown, null);

        switch (activity)
        {
            case ModdingBeatmap modding:
                return (LegacyUserStatus.Modding, modding.BeatmapDisplayTitle);

            case TestingBeatmap testing:
                return (LegacyUserStatus.Testing, testing.BeatmapDisplayTitle);

            case EditingBeatmap editing:
                return (LegacyUserStatus.Editing, editing.BeatmapDisplayTitle);

            case SpectatingUser spectate:
                return (LegacyUserStatus.Watching, $"{spectate.PlayerName} play {spectate.BeatmapDisplayTitle}");

            case WatchingReplay replay:
                return (LegacyUserStatus.Watching, $"{replay.PlayerName} play {replay.BeatmapDisplayTitle}");

            case InMultiplayerGame multiplayer:
                return (LegacyUserStatus.Multiplaying, multiplayer.BeatmapDisplayTitle);

            case SpectatingMultiplayerGame spectatingMultiplayer:
                // stable doesn't support spectating multiplayer games, however, this is like being in a lobby but not playing, which is closest to multiplayer.
                return (LegacyUserStatus.Multiplayer, spectatingMultiplayer.BeatmapDisplayTitle);

            case InDailyChallengeLobby:
                return (LegacyUserStatus.Idle, null);

            case InLobby:
                // lazer doesn't provide beatmap info in multiplayer.
                return (LegacyUserStatus.Multiplayer, null);

            case SearchingForLobby:
                // lazer doesn't provide beatmap info in lobby.
                return (LegacyUserStatus.Lobby, null);

            case InPlaylistGame playlist:
                return (LegacyUserStatus.Playing, playlist.BeatmapDisplayTitle);
            case PlayingDailyChallenge daily:
                return (LegacyUserStatus.Playing, daily.BeatmapDisplayTitle);
            case InSoloGame inGame:
                return (LegacyUserStatus.Playing, inGame.BeatmapDisplayTitle);

            default:
                // lazer doesn't provide afk info.
                return (LegacyUserStatus.Idle, null);
        }
    }
}
