using System.Reflection;
using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseWaterPresentationRegressionTests
    {
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void VoxelShowcaseLoadRestoresWaterAfterDiagnosticDisable()
        {
            RenderingComposition.SetWaterRenderEnabled(false);
            try
            {
                Assert.That(ReadWaterRenderEnabled(), Is.False,
                    "The discriminator must begin from the leaked diagnostic state.");

                InvokeRestoreForScene("VoxelShowcase");

                Assert.That(ReadWaterRenderEnabled(), Is.True,
                    "Loading the production VoxelShowcase must restore its authored water presentation.");
            }
            finally
            {
                RenderingComposition.SetWaterRenderEnabled(true);
            }
        }

        [Test]
        public void OtherSceneLoadDoesNotOverrideExplicitWaterDiagnostic()
        {
            RenderingComposition.SetWaterRenderEnabled(false);
            try
            {
                InvokeRestoreForScene("WorldbuildingGalleryShowcase");
                Assert.That(ReadWaterRenderEnabled(), Is.False,
                    "The fix must not broaden into a global override of explicit water diagnostics.");
            }
            finally
            {
                RenderingComposition.SetWaterRenderEnabled(true);
            }
        }

        [Test]
        public void PortableShowcaseWorldAuthorsIndependentWaterProfilesThroughCanonicalStorage()
        {
            var world = new ShowcaseWorld(
                0xA913u,
                brickPoolCapacity: 8192,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2,
                GameMaterialSimulationDefinitions.Create(),
                maxMixedBrickAllocationBytes: 64L * 1024L * 1024L,
                features: ShowcaseFeatureContent.HouseOnly,
                startup: ShowcaseStartupSource.Generate);

            try
            {
                world.GenerateRegionBlocking(int3.zero);

                int3 still = new int3(8, 400, 8);
                int3 river = new int3(20, 400, 8);
                int3 waterfall = new int3(32, 400, 8);
                Assert.That(world.AuthorVoxelBox(still, new int3(2), GameMaterialIds.Water), Is.EqualTo(8));
                Assert.That(world.AuthorVoxelBox(river, new int3(2), GameMaterialIds.RiverWater), Is.EqualTo(8));
                Assert.That(world.AuthorVoxelBox(waterfall, new int3(2), GameMaterialIds.Cascade), Is.EqualTo(8));

                Assert.That(world.ReadStorage.TryAcquireRegion(int3.zero, out var view), Is.True,
                    "Portable proof world must expose the ordinary resident-region read path.");
                AssertMaterial(view, still, GameMaterialIds.Water);
                AssertMaterial(view, river, GameMaterialIds.RiverWater);
                AssertMaterial(view, waterfall, GameMaterialIds.Cascade);

                var presentation = GameMaterialRenderingDefinitions.Create();
                Assert.That(presentation[GameMaterialIds.Water].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Still));
                Assert.That(presentation[GameMaterialIds.RiverWater].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Flowing));
                Assert.That(presentation[GameMaterialIds.Cascade].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Waterfall));
                Assert.That(presentation[GameMaterialIds.Cascade].Water.Cascade.w, Is.GreaterThan(0f),
                    "Portable waterfall semantics must retain the shared mist/spray cue.");
            }
            finally
            {
                world.StopBackgroundWork();
                world.Dispose();
            }
        }

        private static void AssertMaterial(
            VoxelEngine.Storage.Api.RegionReadView view,
            int3 localVoxel,
            byte expected)
        {
            Assert.That(view.TryReadCell(localVoxel, out var cell), Is.True);
            Assert.That(cell.BaseMaterialId, Is.EqualTo(expected));
        }

        private static void InvokeRestoreForScene(string sceneName)
        {
            var assembly = Assembly.Load("VoxelEngine.Showcase");
            var type = assembly.GetType(
                "VoxelEngine.Showcase.VoxelShowcasePresentationDefaults",
                throwOnError: true);
            var method = type.GetMethod("RestoreForScene", StaticNonPublic);
            Assert.That(method, Is.Not.Null, "Production Showcase presentation reset entrypoint must exist.");
            method.Invoke(null, new object[] { sceneName });
        }

        private static bool ReadWaterRenderEnabled()
        {
            var assembly = Assembly.Load("VoxelEngine.Rendering.Runtime");
            var type = assembly.GetType(
                "VoxelEngine.Rendering.Runtime.VoxelRenderBridge",
                throwOnError: true);
            var field = type.GetField(
                "WaterRenderEnabled",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Renderer diagnostic water switch must remain observable.");
            return (bool)field.GetValue(null);
        }
    }
}
