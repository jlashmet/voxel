using System;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Runtime
{
    /// <summary>
    /// Default presentation façade. Camera, dialogue, and sound remain independent client-local
    /// adapters; cutscene execution sees one ICutscenePresentation without coupling those subsystems.
    /// </summary>
    public sealed class CutscenePresentationRouter : ICutscenePresentation
    {
        private readonly ICutsceneCameraCueRuntime _camera;
        private readonly ICutsceneDialogueCueRuntime _dialogue;
        private readonly ICutsceneSoundCueRuntime _sound;

        public CutscenePresentationRouter(
            ICutsceneCameraCueRuntime camera,
            ICutsceneDialogueCueRuntime dialogue,
            ICutsceneSoundCueRuntime sound)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _sound = sound ?? throw new ArgumentNullException(nameof(sound));
        }

        public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
            RequireOperation(_camera.Execute(cameraCue), "camera", cameraCue);

        public ICutsceneOperation ShowDialogue(
            CutsceneActorId speaker,
            CutsceneCueId dialogueCue) =>
            RequireOperation(_dialogue.Execute(speaker, dialogueCue), "dialogue", dialogueCue);

        public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
            RequireOperation(_sound.Execute(soundCue), "sound", soundCue);

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
