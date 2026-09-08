using System;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Runtime
{
    /// <summary>
    /// Reusable client-local dialogue cue runtime. It resolves authored cue ids to display text,
    /// retains the active line for a bounded presentation window, and never owns gameplay state.
    /// </summary>
    public sealed class TimedCutsceneDialogueRuntime : ICutsceneDialogueCueRuntime, IActiveCutsceneDialogue
    {
        private readonly Func<CutsceneActorId, CutsceneCueId, string> _resolveDialogue;
        private readonly int _displayDurationMilliseconds;
        private int _remainingMilliseconds;

        public string ActiveDialogue { get; private set; }

        public TimedCutsceneDialogueRuntime(
            Func<CutsceneActorId, CutsceneCueId, string> resolveDialogue,
            int displayDurationMilliseconds = 5000)
        {
            _resolveDialogue = resolveDialogue ?? throw new ArgumentNullException(nameof(resolveDialogue));
            if (displayDurationMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(displayDurationMilliseconds));
            _displayDurationMilliseconds = displayDurationMilliseconds;
        }

        public ICutsceneOperation Execute(CutsceneActorId speaker, CutsceneCueId cue)
        {
            string resolved = _resolveDialogue(speaker, cue);
            ActiveDialogue = string.IsNullOrWhiteSpace(resolved) ? cue.Value : resolved;
            _remainingMilliseconds = _displayDurationMilliseconds;
            if (_remainingMilliseconds == 0) ActiveDialogue = null;
            return CompletedCutsceneOperation.Instance;
        }

        public void Advance(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (_remainingMilliseconds <= 0) return;

            _remainingMilliseconds -= elapsedMilliseconds;
            if (_remainingMilliseconds <= 0)
            {
                _remainingMilliseconds = 0;
                ActiveDialogue = null;
            }
        }
    }

    /// <summary>
    /// Shared no-op adapter for presentation channels a cutscene does not use. This keeps scene
    /// composition from reimplementing camera/audio completion shims for dialogue-only sequences.
    /// </summary>
    public sealed class ImmediateCutsceneCueRuntime : ICutsceneCameraCueRuntime, ICutsceneSoundCueRuntime
    {
        public static readonly ImmediateCutsceneCueRuntime Instance = new ImmediateCutsceneCueRuntime();
        private ImmediateCutsceneCueRuntime() { }

        public ICutsceneOperation Execute(CutsceneCueId cue) => CompletedCutsceneOperation.Instance;
    }
}
