using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Structures.Tests.RuntimeSupport;

namespace VoxelEngine.Structures.Tests.PlayMode
{
    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [Test]
        public void FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            var host = new GameObject("Typed Structural Socket Composition Test Host");
            try
            {
                var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

                // The driver exposes its deterministic production composition validation directly.
                // Running it synchronously keeps this behavioral test scoped to Structures instead of
                // yielding an unrelated rendered frame whose URP debug updater depends on legacy Input.
                driver.RunValidation();

                Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
                Assert.That(driver.Passed, Is.True, driver.Detail);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
