using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Settings;
using osu.Game.Plugins;
using osu.Game.Screens;

namespace osu.Plugin.Patches.RankedPlaySpectator;

public partial class RankedPlaySpectatorPlugin : OsuPlugin
{
    private readonly Bindable<string> roomId = new Bindable<string>();

    public override IEnumerable<Drawable>? CreateSettingsControls() => new Drawable[]
    {
        new SettingsItemV2(new FormNumberBox
        {
            Caption = "Room ID",
            Current = roomId,
        }),
        new SettingsButtonV2
        {
            Text = "Start Spectating",
            Action = startSpectating,
        },
    };

    private void startSpectating()
    {
        if (!long.TryParse(roomId.Value, out var parsedRoomId))
        {
            Logger.Log($"Invalid Room ID: {roomId.Value}", level: LogLevel.Error);
            return;
        }

        if (parsedRoomId <= 0)
        {
            Logger.Log($"Room ID must be a positive integer. Provided: {parsedRoomId}", level: LogLevel.Error);
            return;
        }

        var getRoomReq = new GetRoomRequest(parsedRoomId);

        getRoomReq.Failure += e =>
        {
            Logger.Log($"Failed to retrieve room details for Room ID {parsedRoomId}: {e}", level: LogLevel.Error);
        };

        getRoomReq.Success += r =>
        {
            scheduler.Add(() =>
            {
                var spectatorScreen = new RankedPlaySpectatorLoader(r, r.RecentParticipants.ToArray());

                screenPerformer.PerformFromScreen(s => s.Push(spectatorScreen));
            });
        };

        api.Queue(getRoomReq);
    }

    private IAPIProvider api { get; set; } = null!;
    private IPerformFromScreenRunner screenPerformer { get; set; } = null!;
    private Scheduler scheduler { get; set; } = null!;

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        this.scheduler = scheduler;

        game.InvokeWhenReady(d =>
        {
            var osuGame = (OsuGame)d;

            api = osuGame.Dependencies.Get<IAPIProvider>();
            screenPerformer = osuGame.Dependencies.Get<IPerformFromScreenRunner>();
        });
    }
}