using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using osu.Framework.Allocation;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Online;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Plugins;

namespace osu.Plugin.Patches;

/// <summary>
/// A hotfix plugin to disable stateful reconnect of signalR connection, which is causing various issues such as causing frequent network interruption in the latest tachyon release.
/// This is intended to be a temporary hotfix until the underlying issue is properly fixed in the next release, and should not be used as a long-term solution.
/// </summary>
public partial class DisableStatefulReconnectPlugin : OsuPlugin
{
    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame)
            return;

        if (serviceProviderField is null)
        {
            Logger.Log("A required field of signalR connection was missing.", LoggingTarget.Network);
            return;
        }

        Enabled.Disabled = false; // not intended to be toggled

        gameBase.InvokeWhenReady(d =>
        {
            var game = (OsuGame)d;

            var metadataClient = game.Dependencies.Get<MetadataClient>();
            var multiplayerClient = game.Dependencies.Get<MultiplayerClient>();
            var spectatorClient = game.Dependencies.Get<SpectatorClient>();

            processHubClient(metadataClient);
            processHubClient(multiplayerClient);
            processHubClient(spectatorClient);
        });
    }

    private void processHubClient(Drawable client)
    {
        Debug.Assert(client is not null);

        var clientType = client.GetType();

        // accurate matching fails for some reason
        var connectorField = clientType.GetRuntimeFields().FirstOrDefault(f => f.FieldType == typeof(IHubClientConnector));

        if (connectorField is null)
        {
            Logger.Log($"Failed to process {clientType.ReadableName()}", LoggingTarget.Network);
            return;
        }

        // proxy di activator is probably better, but i don't want to copy code for now
        client.InvokeWhenReady(d =>
        {
            var connector = connectorField.GetValue(client) as IHubClientConnector;

            if (connector is null)
            {
                Logger.Log($"Failed to process {clientType.ReadableName()}", LoggingTarget.Network);
                return;
            }

            connector.ConfigureConnection += configureHubConnection;

            // ensure the new http connection options are applied
            if (connector.CurrentConnection is not null)
                connector.Reconnect().FireAndForget(onError: ex => Logger.Log($"Failed to reconnect {clientType.ReadableName()}: {ex}", LoggingTarget.Network));
        });
    }

    private static readonly FieldInfo? serviceProviderField = typeof(HubConnection).GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance);

    private static void configureHubConnection(HubConnection connection)
    {
        var services = serviceProviderField?.GetValue(connection) as IServiceProvider;
        var httpConnectionOption = services?.GetService<IOptions<HttpConnectionOptions>>();

        httpConnectionOption?.Value.UseStatefulReconnect = false;
    }
}
