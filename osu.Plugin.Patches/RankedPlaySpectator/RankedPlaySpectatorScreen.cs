using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;

namespace osu.Plugin.Patches.RankedPlaySpectator;

public partial class RankedPlaySpectatorScreen : MultiSpectatorScreen
{
    public RankedPlaySpectatorScreen(Room room, APIUser[] users)
        : base(room, users.Select(u => new MultiplayerRoomUser(u.Id) { User = u }).ToArray())
    {
    }
}
