using System.Collections.Concurrent;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Online;
using osu.Game.Plugins;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking;

namespace osu.Plugin.Trainer.ShadowPlay;

/// <summary>
/// A plugin that implements a similar feature to the mod Autopilot, 
/// but instead of controlling the cursor with an algorithm, 
/// it allows you play with the movement of a replay.
/// </summary>
public partial class ShadowPlayPlugin : OsuPlugin
{
    private void registerShadowPlayMod()
    {
        var osuRuleset = new OsuRuleset();

        var mods = typeof(Ruleset).GetField("mod_reference_cache", BindingFlags.NonPublic | BindingFlags.Static)?
            .GetValue(null) as ConcurrentDictionary<string, IMod[]>;

        if (mods is not null)
        {
            var osuMods = osuRuleset.AllMods.Where(m => m is not ShadowPlayMod)
                                            .Append(new ShadowPlayMod())
                                            .ToArray();

            mods[osuRuleset.ShortName] = osuMods;
        }
    }

    public override void OnLoad(OsuGameBase gameBase, Scheduler scheduler)
    {
        if (gameBase is not OsuGame game)
            return;

        var selectedScore = new Bindable<ScoreInfo?>();

        game.InvokeWhenReady(d =>
        {
            registerShadowPlayMod();

            var screenStack = game.ScreenStack;

            screenStack.ScreenPushed += onScreenSwitched;
            screenStack.ScreenExited += onScreenSwitched;
        });

        void onScreenSwitched(IScreen oldScreen, IScreen newScreen)
        {
            if (oldScreen is SoloResultsScreen)
                selectedScore.UnbindAll();

            // donno if we should use ResultsScreen or SoloResultsScreen,
            // but let's just use SoloResultsScreen for now since it's more specific.
            if (newScreen is not SoloResultsScreen soloResultsScreen)
                return;

            selectedScore.BindTo(soloResultsScreen.SelectedScore);

            soloResultsScreen.InvokeWhenReady(d =>
            {
                var resultsScreen = (ResultsScreen)d;

                // we should at least be able to watch the replay to use this plugin, so if it's not allowed, we won't show the button at all.
                if (!resultsScreen.AllowWatchingReplay)
                    return;

                var watchReplayButton = resultsScreen.ChildrenOfType<ReplayDownloadButton>().FirstOrDefault();

                // somehow this may be null, but if it is, we won't show the button at all.
                if (watchReplayButton is null)
                    return;

                // i guess non-specificly targeting the container is fine since we only care about adding a button.
                var buttonsContainer = watchReplayButton.Parent;

                if (buttonsContainer is null)
                    return;

                var shadowPlayButton = new ShadowPlayButton()
                {
                    SelectedScore = { BindTarget = selectedScore },
                };

                if (replayDownloadStateField != null)
                {
                    var replayStateObj = replayDownloadStateField.GetValue(watchReplayButton);
                    var replayDownloadState = replayStateObj as Bindable<DownloadState>;

                    if (replayDownloadState != null)
                    {
                        shadowPlayButton.ReplayDownloadState.BindTarget = replayDownloadState;
                    }
                }

                buttonsContainer.AddInternal(shadowPlayButton);
            });
        }
    }

    // we may want static binding for compilation-time safety
    private static FieldInfo? replayDownloadStateField = typeof(ReplayDownloadButton)
        .GetField("State", BindingFlags.NonPublic | BindingFlags.Instance);
}