using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using VoxelEngine.Structures.Tests.RuntimeSupport;

namespace VoxelEngine.Structures.Tests.PlayMode
{
    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            // This focused fixture does not exercise the SRP debug UI. Disable it while the test
            // owns the frame so the render-pipeline package cannot poll the legacy Input API when
            // the project is running with the Input System backend. The package-level poll is
            // unrelated to structural socket composition and otherwise turns a passing fixture
            // into an unhandled-log failure before the driver can report its result.
            bool previousRuntimeUi = DebugManager.instance.enableRuntimeUI;
            DebugManager.instance.enableRuntimeUI = false;

            var host = new GameObject("Typed Structural Socket Composition Test Host");
            try
            {
                var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

                yield return null;

                Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
                Assert.That(driver.Passed, Is.True, driver.Detail);
            }
            finally
            {
                Object.Destroy(host);
                DebugManager.instance.enableRuntimeUI = previousRuntimeUi;
            }
        }
    }
}
