using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Plugin.StableIntegrationPlugin;

[SupportedOSPlatform("windows")]
public partial class WindowsStableController : StableController
{
    [Resolved]
    private LegacyImportManager? legacyImportManager { get; set; } = null!;

    private StableStorage? stableStorage => legacyImportManager?.GetCurrentStableStorage();

    private const string stable_executable = "osu!.exe";
    private const string stable_process_name = "osu!";

    private string? stableExecutable => stableStorage?.GetFullPath(stable_executable);

    public override bool IsAvailable => stableStorage is not null &&
        stableStorage.Exists(stable_executable);

    private const string emptyReplayFileName = "empty.osr";

    public override Task MuteStable()
    {
        var stableStorage = this.stableStorage;

        if (stableStorage is null)
            return Task.CompletedTask;

        var stableProcess = getStableProcess();

        if (stableProcess is null)
            return Task.CompletedTask;

        return mute();

        async Task mute()
        {
            if (!stableStorage.Exists(emptyReplayFileName))
            {
                // create a proper empty replay file
                if (!await createEmptyReplayFile())
                    return;
            }

            Debug.Assert(stableStorage.Exists(emptyReplayFileName));

            var filePath = stableStorage.GetFullPath(emptyReplayFileName);

            string stableExecutable = this.stableExecutable!;

            Debug.Assert(stableExecutable is not null);

            var existingStableWindow = stableProcess.MainWindowHandle;

            try
            {
                // import a replay may bring stable to foreground, so we hide it first
                if (existingStableWindow != IntPtr.Zero)
                    Win32.ShowWindow(existingStableWindow, Win32.ShowWindowCommands.Hide);

                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = stableExecutable,
                    Arguments = $"\"{filePath}\"",
                });

                if (p is null)
                    return;

                await p.WaitForExitAsync();
                await Task.Delay(500); // wait a bit for stable to process the replay
            }
            finally
            {
                // restore stable window
                if (existingStableWindow != IntPtr.Zero)
                    Win32.ShowWindow(existingStableWindow, Win32.ShowWindowCommands.ShowNA);
            }
        }
    }

    private async Task<LegacyScoreEncoder?> createEmptyReplay()
    {
        const string osuRulesetAssemblyQualifiedName = "osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu";

        // FIXME: this works, but a random beatmap still plays music in stable when importing the replay.
        // Maybe also mute via win32 API? This operation is still required to make stable running in a known low-resource state.
        // string? md5Hash = await getRandomBeatmapHashAsync();

        // hard coded hash for stable's builtin beatmap "circles!", comfirmed to be the same between stable and lazer,
        // but this beatmap seems to be missing in stable installation sometimes...
        // is it because christmas-themed version is used these days?
        string md5Hash = "54309531bb7402174969a8d837aead31";

        if (md5Hash is null)
            return null;

        var scoreInfo = new ScoreInfo
        {
            BeatmapInfo = new BeatmapInfo
            {
                // we have to provide a existing beatmap hash here,
                // as stable refuse to load replay if the beatmap is not found.
                MD5Hash = md5Hash,
            },
            Date = DateTime.Now,
            OnlineID = -1,
            Rank = ScoreRank.SH,
            User = new APIUser
            {
                Id = 0,
                Username = "player",
            },
            // Mustbe non-zero to be considered valid.
            TotalScore = 1,
            TotalScoreWithoutMods = 1,
            LegacyOnlineID = 0,
            Ruleset = new RulesetInfo()
            {
                Available = true,
                OnlineID = 0, // osu!standard
                // the replay creation requires to instante the ruleset,
                // here we assume all game instance have osu! ruleset.
                // for development scenario, you may add the package reference,
                // or create a mock ruleset, since it only requires to get mods list.
                InstantiationInfo = osuRulesetAssemblyQualifiedName,
            }
        };

        var replay = new Replay()
        {
            Frames = new List<ReplayFrame>
            {
                new LegacyReplayFrame(0, null, null, ReplayButtonState.None),
            }
        };

        return new LegacyScoreEncoder(new Score
        {
            ScoreInfo = scoreInfo,
            Replay = replay
        }, null);
    }

    [SuppressMessage("Style", "IDE0051", Justification = "Known unused private method.")]
    private async Task<string?> getRandomBeatmapHashAsync()
    {
        var stableStorage = this.stableStorage;

        Debug.Assert(stableStorage is not null);

        var songsStorage = stableStorage.GetSongStorage();
        var songs = songsStorage.GetDirectories(".");

        var beatmapContent = getRandomSong();

        if (beatmapContent is null)
            return null;

        var md5Bytes = await MD5.HashDataAsync(beatmapContent);
        return Convert.ToHexString(md5Bytes).ToLowerInvariant();

        Stream? getRandomSong()
        {
            foreach (var song in songs)
            {
                var beatmaps = songsStorage.GetFiles(song, "*.osu");

                foreach (var beatmap in beatmaps)
                {
                    if (songsStorage.Exists(beatmap))
                    {
                        try
                        {
                            return songsStorage.GetStream(beatmap, FileAccess.Read);
                        }
                        catch { }
                    }
                }
            }

            return null;
        }
    }

    private async Task<bool> createEmptyReplayFile()
    {
        var storage = stableStorage;
        Debug.Assert(storage is not null);

        if (storage.Exists(emptyReplayFileName))
            return false;

        var score = await createEmptyReplay();

        if (score is null)
            return false;

        using var stream = storage.GetStream(emptyReplayFileName, FileAccess.Write);
        score.Encode(stream);

        return true;
    }

    public override Task OpenInStable(BeatmapInfo beatmap)
    {
        string? protocolUrl = GetStableProtocolUrl(beatmap);
        Storage? storage = stableStorage;

        if (protocolUrl is null || storage is null)
            return Task.CompletedTask;

        string stableExecutable = storage.GetFullPath(stable_executable);

        PreSwitchToStable(); // basically mute ourself

        var existingStableProcess = getStableProcess();

        var p = Process.Start(new ProcessStartInfo
        {
            FileName = stableExecutable,
            Arguments = protocolUrl,
        });

        if (existingStableProcess is null)
        {
            focusStableWindow(p);
            return Task.CompletedTask;
        }
        else
        {
            return p switch
            {
                null => Task.CompletedTask,
                _ => Task.Run(async () =>
                {
                    await p.WaitForExitAsync().ConfigureAwait(false);
                    focusStableWindow(existingStableProcess);
                })
            };
        }
    }

    private Process? getStableProcess()
    {
        var stableExecutable = this.stableExecutable;
        if (stableExecutable is null)
            return null;

        var processes = Process.GetProcessesByName(stable_process_name)
            .FirstOrDefault(p => string.Equals(p.MainModule?.FileName, stableExecutable, StringComparison.OrdinalIgnoreCase) &&
                p.MainWindowHandle != IntPtr.Zero);

        return processes;
    }

    private static void focusStableWindow(Process? process)
    {
        if (process is null)
            return;

        var hWnd = process.MainWindowHandle;

        if (hWnd == IntPtr.Zero)
            return;

        try
        {
            IntPtr foregroundWindow = Win32.GetForegroundWindow();

            if (foregroundWindow == hWnd)
                return;

            if (foregroundWindow != IntPtr.Zero)
                Win32.ShowWindow(foregroundWindow, Win32.ShowWindowCommands.ForceMinimize);

            Win32.ShowWindow(hWnd, Win32.ShowWindowCommands.Restore);
            Win32.SetForegroundWindow(hWnd);
        }
        catch (Exception e)
        {
            Logger.Log($"Failed to focus stable window: {e.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
        }
    }

    static partial class Win32
    {
        public enum ShowWindowCommands : int
        {
            Hide = 0,
            ShowNA = 8,
            Restore = 9,
            ForceMinimize = 11
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

        [LibraryImport("user32.dll")]
        public static partial IntPtr GetForegroundWindow();
    }
}
