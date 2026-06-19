using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.StateChanges;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Plugin.Trainer.ShadowPlay;

internal partial class ShadowPlayMod : Mod, IUpdatableByPlayfield, IApplicableToDrawableRuleset<OsuHitObject>
{
    private readonly Score score = null!;

    public override bool Ranked => false;

    public override string Name => "Shadow Play";

    public override string Acronym => "SP";

    public override LocalisableString Description => "Play with the movement of the replay. For training purposes.";

    // i would like it to be as same as 0.1 of OsuModAutopilot
    // but as a trainer mod that doesn't affect score, let's just make it 1x for now
    public override double ScoreMultiplier => 1;

    public override Type[] IncompatibleMods => new[]
    {
            typeof(OsuModSpunOut),
            typeof(ModRelax),
            typeof(ModAutoplay),
            typeof(OsuModMagnetised),
            typeof(OsuModRepel),
            // typeof(ModTouchDevice),
        };

    public override ModType Type => ModType.Automation;

    public override bool AlwaysValidForSubmission => false;

    public override IconUsage? Icon => FontAwesome.Solid.Video;

    public ShadowPlayMod() { }

    public ShadowPlayMod(Score score)
    {
        this.score = score;
    }

    public override Mod DeepClone()
    {
        return new ShadowPlayMod(score);
    }

    private OsuInputManager inputManager = null!;

    private List<OsuReplayFrame> replayFrames = null!;

    private int currentFrame = -1;

    public void Update(Playfield playfield)
    {
        // copied from OsuModAutopilot

        if (currentFrame >= replayFrames.Count - 1) return;

        double time = playfield.Clock.CurrentTime;

        // Very naive implementation of autopilot based on proximity to replay frames.
        // Special case for the first frame is required to ensure the mouse is in a sane position until the actual time of the first frame is hit.
        // TODO: this needs to be based on user interactions to better match stable (pausing until judgement is registered).
        if (currentFrame < 0 || Math.Abs(replayFrames[currentFrame + 1].Time - time) <= Math.Abs(replayFrames[currentFrame].Time - time))
        {
            currentFrame++;
            new MousePositionAbsoluteInput { Position = playfield.ToScreenSpace(replayFrames[currentFrame].Position) }.Apply(inputManager.CurrentState, inputManager);
        }

        // TODO: Implement the functionality to automatically spin spinners
    }

    public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
    {
        // Grab the input manager to disable the user's cursor, and for future use
        inputManager = ((DrawableOsuRuleset)drawableRuleset).KeyBindingInputManager;
        inputManager.AllowUserCursorMovement = false;

        // when replay a score with SP mod, 
        // the replay frames are not loaded for some reason.
        // this is expected, as the replay frames are stored in the database.
        // But it's not necessary to load the replay frames from the database, since the playing replay already recorded those frames
        // so we just create a empty replay frames list and pretend this mod doesn't exist.
        // Debug.Assert(score != null, "Score should not be null when applying ShadowPlayMod.");

        replayFrames = score?.Replay
                            .Frames
                            .Cast<OsuReplayFrame>()
                            .ToList() ?? new();
    }
}
