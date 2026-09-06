using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Behavioural coverage of the production browser lifecycle, not a screenshot substitute.
    /// The module-owned standalone scene supplies actual material/geometry acceptance evidence.
    /// </summary>
    public sealed class PropShowcaseMaterialModeTests
    {
        [UnityTest]
        public IEnumerator EnableAndSwitch_UsesProductionMaterialsInsteadOfNormalCoverage()
        {
            Assert.That(VoxelRenderBridge.Source, Is.Null,
                "This isolated browser fixture must not replace another consumer's live world.");

            Color previousTint = VoxelRenderBridge.SurfaceDebugTint;
            Vector3 previousSun = VoxelRenderBridge.SunDirection;
            Color previousHorizon = VoxelRenderBridge.SkyHorizon;
            Color previousZenith = VoxelRenderBridge.SkyZenith;
            bool previousBuildEnabled = VoxelRenderBridge.SurfaceBuildEnabled;
            bool previousLodEnabled = VoxelRenderBridge.SurfaceLodEnabled;
            double previousSolidBudget = VoxelRenderBridge.SolidBuildBudgetMs;
            double previousWaterBudget = VoxelRenderBridge.WaterBuildBudgetMs;
            bool previousCutaway = VoxelRenderBridge.CutawayEnabled;
            bool previousFlashlight = VoxelRenderBridge.FlashlightEnabled;
            Vector4[] previousLights = VoxelRenderBridge.LocalLights;
            Vector4[] previousLightColours = VoxelRenderBridge.LocalLightColours;
            uint previousSeed = VoxelRenderBridge.TerrainSeed;
            bool previousFarField = VoxelRenderBridge.FarFieldEnabled;

            var cameraObject = new GameObject("PropShowcase material-mode regression");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            // This fixture observes real composition state. It does not render fake proof pixels
            // or allocate the full GPU arena; standalone validation exercises the enabled camera.
            camera.enabled = false;
            PropShowcase browser = cameraObject.AddComponent<PropShowcase>();

            try
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    // Start from diagnostic mode so the real OnEnable must explicitly restore
                    // production shading; a white global default cannot hide a missing reset.
                    VoxelRenderBridge.SurfaceDebugTint = Color.magenta;
                    cameraObject.SetActive(true);
                    Assert.That(browser.EntryCount, Is.GreaterThan(0));
                    Assert.That(browser.SelectedStableId, Is.Not.Empty);
                    Assert.That(VoxelRenderBridge.SurfaceDebugTint, Is.EqualTo(Color.white),
                        "Non-white selects normal coverage and bypasses production materials.");

                    // Exercise public navigation endpoints without duplicating any catalogue ID.
                    Assert.That(browser.Select(browser.EntryCount - 1), Is.True);
                    Assert.That(VoxelRenderBridge.SurfaceDebugTint, Is.EqualTo(Color.white));
                    Assert.That(browser.Select(0), Is.True);
                    Assert.That(VoxelRenderBridge.SurfaceDebugTint, Is.EqualTo(Color.white));

                    cameraObject.SetActive(false);
                    Assert.That(VoxelRenderBridge.Source, Is.Null);
                    Assert.That(browser.OwnedPresentationCount, Is.Zero);
                    // Allow production Destroy calls to retire the previous presentation root.
                    yield return null;
                }
            }
            finally
            {
                cameraObject.SetActive(false);
                Object.Destroy(cameraObject);
                VoxelRenderBridge.SurfaceDebugTint = previousTint;
                VoxelRenderBridge.SunDirection = previousSun;
                VoxelRenderBridge.SkyHorizon = previousHorizon;
                VoxelRenderBridge.SkyZenith = previousZenith;
                VoxelRenderBridge.SurfaceBuildEnabled = previousBuildEnabled;
                VoxelRenderBridge.SurfaceLodEnabled = previousLodEnabled;
                VoxelRenderBridge.SolidBuildBudgetMs = previousSolidBudget;
                VoxelRenderBridge.WaterBuildBudgetMs = previousWaterBudget;
                VoxelRenderBridge.CutawayEnabled = previousCutaway;
                VoxelRenderBridge.FlashlightEnabled = previousFlashlight;
                VoxelRenderBridge.LocalLights = previousLights;
                VoxelRenderBridge.LocalLightColours = previousLightColours;
                VoxelRenderBridge.TerrainSeed = previousSeed;
                VoxelRenderBridge.FarFieldEnabled = previousFarField;
            }
        }

        [UnityTearDown]
        public IEnumerator DrainDeferredDestruction()
        {
            yield return null;
        }
    }
}
