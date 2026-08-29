using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CutsceneDialoguePresentationRuntimeTests
    {
        [Test]
        public void TimedDialogueRoutesThroughSharedPresentationAndExpires()
        {
            var cue = new CutsceneCueId("test.dialogue");
            var dialogue = new TimedCutsceneDialogueRuntime(
                (speaker, requested) => requested.Equals(cue) ? "Shared dialogue" : requested.Value,
                displayMilliseconds: 1000);
            var presentation = new CutscenePresentationRouter(
                ImmediateCutsceneCueRuntime.Instance,
                dialogue,
                ImmediateCutsceneCueRuntime.Instance);

            ICutsceneOperation operation = presentation.ShowDialogue(default, cue);

            Assert.That(operation.IsComplete, Is.True);
            Assert.That(dialogue.ActiveDialogue, Is.EqualTo("Shared dialogue"));

            dialogue.Advance(999);
            Assert.That(dialogue.ActiveDialogue, Is.EqualTo("Shared dialogue"));

            dialogue.Advance(1);
            Assert.That(dialogue.ActiveDialogue, Is.Null);
        }
    }
}
