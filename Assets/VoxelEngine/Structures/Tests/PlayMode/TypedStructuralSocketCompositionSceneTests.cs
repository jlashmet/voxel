using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Structures.Tests.RuntimeSupport;

namespace VoxelEngine.Structures.Tests.PlayMode
{
    public sealed class TypedStructuralSocketCompositionSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket()
        {
            var host = new GameObject("Typed Structural Socket Composition Test Host");
            var driver = host.AddComponent<TypedStructuralSocketCompositionSceneDriver>();

            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused structural validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);

            Object.Destroy(host);
        }
    }
}
