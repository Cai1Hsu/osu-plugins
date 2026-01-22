using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Plugins;
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Play;

namespace osu.Plugin.LegacyExperience;

public sealed partial class LegacyExperiencePlugin
{
    private void applyLegacyBackgroundFillModeHook(OsuGame game)
    {
        var screenStack = game.GetScreenStack();
        var backgroundScreenStack = screenStack.Dependencies.Get<BackgroundScreenStack>();

        BackgroundScreenBeatmap? currentBeatmapBg = null;

        backgroundScreenStack.ScreenPushed += (p, n) => captureBackgroundScreenBeatmap(p, n, false);
        backgroundScreenStack.ScreenExited += (p, n) => captureBackgroundScreenBeatmap(p, n, true);

        void captureBackgroundScreenBeatmap(IScreen prev, IScreen next, bool exiting)
        {
            if (exiting && ReferenceEquals(currentBeatmapBg, prev))
                currentBeatmapBg = null;

            if (next is BackgroundScreenBeatmap newBg)
                currentBeatmapBg = newBg;
        }

        screenStack.ScreenPushed += (p, n) => capturePlayer(p, n, false);
        screenStack.ScreenExited += (p, n) => capturePlayer(p, n, true);

        void capturePlayer(IScreen prev, IScreen next, bool exiting)
        {
            if (currentBeatmapBg is null)
                return;

            if (next is Player)
                // stable's default fill mode in Player is Fit.
                updateFilllMode(FillMode.Fit);
            else if (exiting && prev is Player)
                // lazer's default fill mode outside of Player is Fill.
                updateFilllMode(FillMode.Fill);
        }

        void updateFilllMode(FillMode newMode)
        {
            var bg = backgroundField.GetValue(currentBeatmapBg) as Background;

            if (bg is null)
                return;

            bg.Sprite.FillMode = newMode;
        }
    }

    private static readonly FieldInfo backgroundField = typeof(BackgroundScreenBeatmap)
        .GetField("Background", BindingFlags.NonPublic | BindingFlags.Instance)!;
}
