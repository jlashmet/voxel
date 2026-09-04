using System.Reflection;
using Game.SessionOrchestration.Runtime;
using Game.WorldBuilder.Validation;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderSecretDiscoveryValidationHarnessTests
    {
        [Test]
        public void LocalValidationUsesCanonicalSessionOrchestrationContracts()
        {
            var validationType = typeof(WorldBuilderSecretDiscoveryValidation);

            Assert.That(typeof(ISessionRuntimeGraphFactory).IsAssignableFrom(validationType), Is.True,
                "The local SecretDiscovery scene must compose through the canonical session graph factory contract.");
            Assert.That(typeof(ISessionRuntimeGraph).IsAssignableFrom(validationType), Is.True,
                "The local SecretDiscovery scene must participate in the canonical runtime graph lifecycle.");

            FieldInfo orchestrator = validationType.GetField(
                "_sessionOrchestrator",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(orchestrator, Is.Not.Null,
                "The validation scene must retain an explicit canonical GameSessionOrchestrator owner.");
            Assert.That(orchestrator.FieldType, Is.EqualTo(typeof(GameSessionOrchestrator)));
        }
    }
}
