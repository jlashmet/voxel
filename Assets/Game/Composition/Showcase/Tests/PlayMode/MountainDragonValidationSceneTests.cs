using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase.Tests.RuntimeSupport;

namespace VoxelEngine.Showcase.Tests.PlayMode
{
    public sealed class MountainDragonValidationSceneTests
    {
        [UnityTest]
        public IEnumerator FocusedValidationDriver_UsesProductionRouteAndCenteredHeadroom()
        {
            var host = new GameObject("Mountain Dragon Validation Test Host");
            var driver = host.AddComponent<MountainDragonValidationSceneDriver>();

            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused Mountain Dragon validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);

            Object.Destroy(host);
        }
    }
}
