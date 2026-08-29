using System;

namespace Game.Cutscenes.Api
{
    /// <summary>Completion barrier for movement, dialogue, camera transitions, and other cutscene work.</summary>
    public interface ICutsceneOperation
    {
        bool IsComplete { get; }
    }

    public sealed class CompletedCutsceneOperation : ICutsceneOperation
    {
        public static readonly CompletedCutsceneOperation Instance = new CompletedCutsceneOperation();
        private CompletedCutsceneOperation() { }
        public bool IsComplete => true;
    }

    /// <summary>Authoritative gameplay seam. Implementations resolve semantic actor ids to runtime actors.</summary>
    public interface ICutsceneActorController
    {
        bool Contains(CutsceneActorId actor);
        void PlaceAt(CutsceneActorId actor, CutsceneStagePoint destination);
        ICutsceneOperation MoveTo(CutsceneActorId actor, CutsceneStagePoint destination, int durationHintMilliseconds);
        ICutsceneOperation FaceActor(CutsceneActorId actor, CutsceneActorId target);
        ICutsceneOperation FacePoint(CutsceneActorId actor, CutsceneStagePoint target);
    }

    /// <summary>Adapter implemented by a concrete gameplay actor; kept engine-independent for deterministic tests.</summary>
    public interface ICutsceneActorRuntime
    {
        CutsceneInt3 Position { get; }
        void PlaceAt(CutsceneStagePoint destination);
        ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds);
        ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition);
    }

    /// <summary>
    /// Optional authoritative player-control seam for scenes that explicitly lock and release input.
    /// Existing cutscene callers remain valid through the completed no-op default.
    /// </summary>
    public interface ICutsceneControlLockRuntime
    {
        ICutsceneOperation Acquire();
        ICutsceneOperation Release();
    }

    public sealed class CompletedCutsceneControlLockRuntime : ICutsceneControlLockRuntime
    {
        public static readonly CompletedCutsceneControlLockRuntime Instance =
            new CompletedCutsceneControlLockRuntime();

        private CompletedCutsceneControlLockRuntime() { }
        public ICutsceneOperation Acquire() => CompletedCutsceneOperation.Instance;
        public ICutsceneOperation Release() => CompletedCutsceneOperation.Instance;
    }

    /// <summary>
    /// Client-local presentation seam. Camera, dialogue, and audio never own authoritative gameplay state.
    /// A default speaker id means the dialogue cue itself owns speaker assignment (legacy-content compatible).
    /// </summary>
    public interface ICutscenePresentation
    {
        ICutsceneOperation SetCamera(CutsceneCueId cameraCue);
        ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue);
        ICutsceneOperation PlaySound(CutsceneCueId soundCue);
    }

    public sealed class CutsceneExecutionContext
    {
        public ICutsceneActorController Actors { get; }
        public ICutscenePresentation Presentation { get; }
        public CutsceneStageBinding Stage { get; }
        public ICutsceneControlLockRuntime ControlLock { get; }

        public CutsceneExecutionContext(
            ICutsceneActorController actors,
            ICutscenePresentation presentation,
            CutsceneStageBinding stage,
            ICutsceneControlLockRuntime controlLock = null)
        {
            Actors = actors ?? throw new ArgumentNullException(nameof(actors));
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            ControlLock = controlLock ?? CompletedCutsceneControlLockRuntime.Instance;
        }
    }
}
