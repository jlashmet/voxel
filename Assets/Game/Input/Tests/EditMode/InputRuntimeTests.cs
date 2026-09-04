using Game.Input.Api;
using Game.Input.Runtime;
using NUnit.Framework;

namespace Game.Input.Tests
{
    public sealed class InputRuntimeTests
    {
        [Test]
        public void NestedContextLeaseRestoresPriorContext()
        {
            var contexts = new InputContextService();
            Assert.That(contexts.ActiveContext, Is.EqualTo(InputContextId.Exploration));
            var ui = contexts.Push(InputContextId.Ui);
            var disabled = contexts.Push(InputContextId.Disabled);
            Assert.That(contexts.ActiveContext, Is.EqualTo(InputContextId.Disabled));
            disabled.Dispose();
            Assert.That(contexts.ActiveContext, Is.EqualTo(InputContextId.Ui));
            ui.Dispose();
            Assert.That(contexts.ActiveContext, Is.EqualTo(InputContextId.Exploration));
        }

        [Test]
        public void InputSystemBindingOverrideRoundTripsThroughOwningAdapter()
        {
            var contexts = new InputContextService();
            using (var reader = new UnityPlayerInputReader(contexts))
            {
                var binding = new InputBindingOverride("Confirm", 0, "<Keyboard>/f");
                Assert.That(reader.TryApplyOverride(binding, out string error), Is.True, error);
                Assert.That(reader.SnapshotOverrides(), Has.Count.EqualTo(1));
                Assert.That(reader.SnapshotOverrides()[0], Is.EqualTo(binding));
                reader.ClearOverrides();
                Assert.That(reader.SnapshotOverrides(), Is.Empty);
            }
        }

        [Test]
        public void UiContextSuppressesGameplaySnapshotWithoutLegacyPolling()
        {
            var contexts = new InputContextService();
            using (var reader = new UnityPlayerInputReader(contexts))
            using (contexts.Push(InputContextId.Ui))
            {
                PlayerInputSnapshot snapshot = reader.Read(new LocalPlayerId(0));
                Assert.That(snapshot.MoveX, Is.Zero);
                Assert.That(snapshot.MoveY, Is.Zero);
                Assert.That(snapshot.PrimaryPressed, Is.False);
                Assert.That(snapshot.ConfirmPressed, Is.False);
            }
        }
    }
}
