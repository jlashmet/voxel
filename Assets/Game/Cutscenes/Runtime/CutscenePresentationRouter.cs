using System;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Runtime
{
    /// <summary>
    /// Default presentation façade. Camera, dialogue, sound, transitions, and actor cues remain
    /// independent client-local adapters; cutscene execution sees one presentation boundary without
    /// coupling those subsystems. Optional newer adapters preserve compatibility for existing scenes.
    /// </summary>
    public sealed class CutscenePresentationRouter :
        ICutscenePresentation,
        ICutsceneTransitionPresentation,
        ICutsceneActorCuePresentation
    {
        private readonly ICutsceneCameraCueRuntime _camera;
        private readonly ICutsceneDialogueCueRuntime _dialogue;
        private readonly ICutsceneSoundCueRuntime _sound;
        private readonly ICutsceneTransitionCueRuntime _transition;
        private readonly ICutsceneActorCueRuntime _actorCue;

        public CutscenePresentationRouter(
            ICutsceneCameraCueRuntime camera,
            ICutsceneDialogueCueRuntime dialogue,
            ICutsceneSoundCueRuntime sound,
            ICutsceneTransitionCueRuntime transition = null,
            ICutsceneActorCueRuntime actorCue = null)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _sound = sound ?? throw new ArgumentNullException(nameof(sound));
            _transition = transition;
            _actorCue = actorCue;
        }

        public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
            RequireOperation(_camera.Execute(cameraCue), "camera", cameraCue);

        public ICutsceneOperation ShowDialogue(
            CutsceneActorId speaker,
            CutsceneCueId dialogueCue) =>
            RequireOperation(_dialogue.Execute(speaker, dialogueCue), "dialogue", dialogueCue);

        public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
            RequireOperation(_sound.Execute(soundCue), "sound", soundCue);

        public ICutsceneOperation PlayTransition(CutsceneCueId transitionCue) =>
            _transition == null
                ? CompletedCutsceneOperation.Instance
                : RequireOperation(_transition.Execute(transitionCue), "transition", transitionCue);

        public ICutsceneOperation PlayActorCue(CutsceneActorId actor, CutsceneCueId actorCue) =>
            _actorCue == null
                ? CompletedCutsceneOperation.Instance
                : RequireOperation(_actorCue.Execute(actor, actorCue), "actor", actorCue);

        private static ICutsceneOperation RequireOperation(
            ICutsceneOperation operation,
            string kind,
            CutsceneCueId cue)
        {
            if (operation != null) return operation;
            throw new InvalidOperationException(
                "Cutscene " + kind + " runtime returned no operation for cue '" + cue + "'.");
        }
    }
}
