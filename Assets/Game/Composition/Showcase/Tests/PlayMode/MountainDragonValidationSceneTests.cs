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
            var materialGuard = host.AddComponent<MountainDragonValidationMaterialGuard>();

            yield return null;
            yield return null;

            Assert.That(driver.Complete, Is.True, "Focused Mountain Dragon validation did not complete.");
            Assert.That(driver.Passed, Is.True, driver.Detail);
            Assert.That(materialGuard.Applied, Is.True, materialGuard.Detail);
            Assert.That(materialGuard.ShaderSupported, Is.True, materialGuard.Detail);
            Assert.That(materialGuard.RendererCount, Is.GreaterThan(0), materialGuard.Detail);

            Renderer[] renderers = host.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(materialGuard.RendererCount));
            for (int i = 0; i < renderers.Length; i++)
            {
                Assert.That(renderers[i].sharedMaterial, Is.Not.Null, $"Renderer {i} has no validation material.");
                Assert.That(renderers[i].sharedMaterial.shader, Is.Not.Null, $"Renderer {i} has no validation shader.");
                Assert.That(renderers[i].sharedMaterial.shader.isSupported, Is.True, $"Renderer {i} shader is unsupported.");
                Assert.That(renderers[i].sharedMaterial.shader.name, Is.EqualTo(MountainDragonValidationMaterialGuard.ExpectedShaderName));
            }

            Object.Destroy(host);
        }
    }
}
