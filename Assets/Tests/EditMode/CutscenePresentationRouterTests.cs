using System;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CutscenePresentationRouterTests
    {
        private sealed class Operation : ICutsceneOperation
        {
            public bool IsComplete => false;
        }

        private sealed class Camera : ICutsceneCameraCueRuntime
        {
            public CutsceneCueId Cue;
            public ICutsceneOperation Result = new Operation();
            public ICutsceneOperation Execute(CutsceneCueId cue) { Cue = cue; return Result; }
        }

        private sealed class Dialogue : ICutsceneDialogueCueRuntime
        {
            public CutsceneActorId Speaker;
            public CutsceneCueId Cue;
            public ICutsceneOperation Result = new Operation();
            public ICutsceneOperation Execute(CutsceneActorId speaker, CutsceneCueId cue)
            {
                Speaker = speaker;
                Cue = cue;
                return Result;
            }
        }

        private sealed class Sound : ICutsceneSoundCueRuntime
        {
            public CutsceneCueId Cue;
            public ICutsceneOperation Result = new Operation();
            public ICutsceneOperation Execute(CutsceneCueId cue) { Cue = cue; return Result; }
        }

        [Test]
        public void RouterPreservesCueAndExplicitDialogueSpeaker()
        {
            var camera = new Camera();
            var dialogue = new Dialogue();
            var sound = new Sound();
            var router = new CutscenePresentationRouter(camera, dialogue, sound);
            var cameraCue = new CutsceneCueId("camera.opening");
            var speaker = new CutsceneActorId("guide");
            var dialogueCue = new CutsceneCueId("guide.line");
            var soundCue = new CutsceneCueId("door.open");

            ICutsceneOperation cameraOperation = router.SetCamera(cameraCue);
            ICutsceneOperation dialogueOperation = router.ShowDialogue(speaker, dialogueCue);
            ICutsceneOperation soundOperation = router.PlaySound(soundCue);

            Assert.That(camera.Cue, Is.EqualTo(cameraCue));
            Assert.That(dialogue.Speaker, Is.EqualTo(speaker));
            Assert.That(dialogue.Cue, Is.EqualTo(dialogueCue));
            Assert.That(sound.Cue, Is.EqualTo(soundCue));
            Assert.That(cameraOperation, Is.SameAs(camera.Result));
            Assert.That(dialogueOperation, Is.SameAs(dialogue.Result));
            Assert.That(soundOperation, Is.SameAs(sound.Result));
        }

        [Test]
        public void RouterPreservesDefaultSpeakerForLegacyDialogueCue()
        {
            var dialogue = new Dialogue();
            var router = new CutscenePresentationRouter(new Camera(), dialogue, new Sound());
            var cue = new CutsceneCueId("legacy.dialogue");

            router.ShowDialogue(default(CutsceneActorId), cue);

            Assert.That(dialogue.Speaker, Is.EqualTo(default(CutsceneActorId)));
            Assert.That(dialogue.Cue, Is.EqualTo(cue));
        }

        [Test]
        public void RouterRejectsMissingPresentationOperation()
        {
            var camera = new Camera { Result = null };
            var router = new CutscenePresentationRouter(camera, new Dialogue(), new Sound());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                router.SetCamera(new CutsceneCueId("missing.camera")));

            Assert.That(error.Message, Does.Contain("missing.camera"));
        }
    }
}
