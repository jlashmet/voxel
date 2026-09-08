using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using NUnit.Framework;

namespace Game.Cutscenes.Tests
{
    public sealed class TimedCutsceneDialogueRuntimeTests
    {
        [Test]
        public void ExecuteResolvesDialogueAndExpiresAfterConfiguredDuration()
        {
            var runtime = new TimedCutsceneDialogueRuntime(
                (speaker, cue) => speaker.Value + ":" + cue.Value,
                displayDurationMilliseconds: 100);

            runtime.Execute(new CutsceneActorId("dragon"), new CutsceneCueId("hello"));

            Assert.That(runtime.ActiveDialogue, Is.EqualTo("dragon:hello"));
            runtime.Advance(99);
            Assert.That(runtime.ActiveDialogue, Is.EqualTo("dragon:hello"));
            runtime.Advance(1);
            Assert.That(runtime.ActiveDialogue, Is.Null);
        }

        [Test]
        public void EmptyResolvedDialogueFallsBackToSemanticCue()
        {
            var runtime = new TimedCutsceneDialogueRuntime((_, __) => "   ", 1000);

            runtime.Execute(new CutsceneActorId("dragon"), new CutsceneCueId("hello"));

            Assert.That(runtime.ActiveDialogue, Is.EqualTo("hello"));
        }
    }
}
