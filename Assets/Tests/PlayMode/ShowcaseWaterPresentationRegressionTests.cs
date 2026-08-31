using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Showcase;

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

                MaterialPresentationDefinition[] presentation = GameMaterialRenderingDefinitions.Create();
                VoxelMaterialPresentationInstaller.Apply(presentation);
                Assert.That(presentation[GameMaterialIds.Water].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Still));
                Assert.That(presentation[GameMaterialIds.RiverWater].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Flowing));
                Assert.That(presentation[GameMaterialIds.Cascade].Water.Profile,
                    Is.EqualTo(WaterPresentationProfile.Waterfall));

                var renderingWorld = new RenderingWorldBinding(
                    world.ReadStorage,
                    world.Palette,
                    world.SurfaceRules,
                    world.CoatingRules,
                    world.ProfileBlocks);
                RenderingComposition.ConfigureWorld(
                    in renderingWorld,
                    world.Changes,
                    world.Seed,
                    farFieldEnabled: false);

                Assert.That(RenderingComposition.TryGetWorld(out var bound, out uint boundSeed), Is.True,
                    "Portable proof must reach the ordinary production RenderingComposition binding.");
                Assert.That(bound.Storage, Is.SameAs(world.ReadStorage));
                Assert.That(boundSeed, Is.EqualTo(world.Seed));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[GameMaterialIds.Water].x,
                    Is.EqualTo((float)WaterPresentationProfile.Still));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[GameMaterialIds.RiverWater].x,
                    Is.EqualTo((float)WaterPresentationProfile.Flowing));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[GameMaterialIds.Cascade].x,
                    Is.EqualTo((float)WaterPresentationProfile.Waterfall));
                Assert.That(VoxelPresentationCatalogue.WaterCascade[GameMaterialIds.Cascade].w,
                    Is.GreaterThan(0f),
                    "Portable waterfall semantics must retain the shared mist/spray cue after installation.");
            }
            finally
            {
                RenderingComposition.ClearWorld();
                world.StopBackgroundWork();
                world.Dispose();
            }
        }

        [Test]
        public void SolidClassificationUsesInstalledPresentationMaskForOpaqueWaterId()
        {
            const byte opaqueWaterId = 23;
            const byte opaqueSolidId = 24;
            var water = new WaterPresentationDefinition(
                WaterPresentationProfile.Waterfall,
                shallow: new float4(0.2f, 0.7f, 0.9f, 0.7f),
                deep: new float4(0.02f, 0.15f, 0.3f, 4f),
                flowDirection: new float2(0f, 1f),
                flowSpeed: 1f,
                waveScale: 1f,
                normalStrength: 1f,
                refractionStrength: 0.1f,
                smoothness: 0.8f,
                surfaceFoam: 0.5f,
                contactFoam: 0.5f,
                foamScale: 1f,
                foamSpeed: 1f,
                turbulence: 0.6f,
                edgeFoam: 0.7f,
                impactFoam: 0.8f,
                mist: 0.9f);

            try
            {
                VoxelMaterialPresentationInstaller.Apply(new[]
                {
                    new MaterialPresentationDefinition(
                        opaqueWaterId, new float4(0.1f, 0.4f, 0.7f, 1f), water: water),
                    new MaterialPresentationDefinition(
                        opaqueSolidId, new float4(0.5f, 0.5f, 0.5f, 1f)),
                });

                Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(opaqueWaterId), Is.True);
                Assert.That(SolidMaterialClassification.IsSolid(opaqueWaterId), Is.False,
                    "Solid extraction must derive water exclusion from installed presentation, not game IDs.");
                Assert.That(TransvoxelDensityJob.IsSolidSample(opaqueWaterId), Is.False,
                    "Burst density classification must consume the same presentation-driven water mask.");
                Assert.That(SolidMaterialClassification.IsSolid(opaqueSolidId), Is.True);
                Assert.That(TransvoxelDensityJob.IsSolidSample(opaqueSolidId), Is.True);
            }
            finally
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [UnityTest]
        public IEnumerator ExactCascadeCurtainImpactsBesideReceivingWaterAndSurvivesProductionCache()
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
            var cache = new CpuWaterSurfaceChunkCache();
            var cameraObject = new GameObject("Cascade storage-cache discriminator camera");
            Camera camera = cameraObject.AddComponent<Camera>();

            try
            {
                world.GenerateRegionBlocking(int3.zero);
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());

                int fallBaseY = MaxSurfaceHeight(world, 304, 424, 194, 276) + 5;
                int3 receivingMin = new int3(324, fallBaseY + 6, 184);
                int3 receivingSize = new int3(80, 2, 33);
                Assert.That(
                    world.AuthorVoxelBox(receivingMin, receivingSize, GameMaterialIds.RiverWater),
                    Is.EqualTo(receivingSize.x * receivingSize.y * receivingSize.z));

                int3 curtainMin = new int3(333, fallBaseY + 9, 199);
                int3 curtainSize = new int3(22, 59, 3);
                Assert.That(
                    world.AuthorVoxelBox(curtainMin, curtainSize, GameMaterialIds.Cascade),
                    Is.EqualTo(curtainSize.x * curtainSize.y * curtainSize.z),
                    "The discriminator must author an exact primary Cascade band used by WaterRenderingShowcase.");

                Assert.That(world.ReadStorage.TryAcquireRegion(int3.zero, out var view), Is.True);
                AssertMaterial(view, curtainMin, GameMaterialIds.Cascade);
                AssertMaterial(view, curtainMin + curtainSize - 1, GameMaterialIds.Cascade);
                AssertMaterial(view, curtainMin - new int3(0, 2, 0), GameMaterialIds.RiverWater);
                Assert.That(view.TryReadCell(curtainMin - new int3(0, 1, 0), out var impactGap), Is.True);
                Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(impactGap.BaseMaterialId), Is.False,
                    "The exact authored band must leave only one non-water voxel before the receiving river, localizing canonical impact spray to the pool surface instead of up the cliff.");

                List<int3> curtainBricks = BricksCovering(curtainMin, curtainSize);
                cache.InvalidateSurfaceBricks(world.ReadStorage, curtainBricks);
                Assert.That(cache.DirtyCount, Is.GreaterThan(0),
                    "Canonical storage discovery must admit the authored Cascade curtain into the water cache.");

                Vector3 curtainCentre = (Vector3)((float3)curtainMin + (float3)curtainSize * 0.5f)
                                      * ShowcaseWorld.VoxelSize;
                camera.transform.position = curtainCentre + new Vector3(0f, 0f, -20f);
                camera.transform.LookAt(curtainCentre);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 350f;

                const int maxFrames = 120;
                for (int frame = 0; frame < maxFrames && cache.CompletedBuildCount == 0; frame++)
                {
                    cache.Prepare(world.ReadStorage, camera, ShowcaseWorld.VoxelSize, budgetMs: 5.0);
                    cache.TryPublishPending(int.MaxValue, out _);
                    if (cache.CompletedBuildCount == 0)
                        yield return null;
                }

                Assert.That(cache.CompletedBuildCount, Is.GreaterThan(0),
                    "The exact authored Cascade curtain must complete production water-cache extraction.");
                Assert.That(cache.ResidentCount, Is.GreaterThan(0),
                    "The exact authored Cascade curtain must publish a resident production water-cache entry.");
                Assert.That(cache.UploadedGeometryBytes, Is.GreaterThan(0),
                    "The production water cache must encode and upload non-empty Cascade geometry from canonical storage.");

                IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> visible =
                    cache.CollectVisible(camera, ShowcaseWorld.VoxelSize);
                Assert.That(visible.Count, Is.GreaterThan(0),
                    "The published Cascade geometry must survive through the cache visibility boundary.");
                Assert.That(visible[0].IndexCount, Is.GreaterThan(0),
                    "The visible production entry must contain real indexed water geometry, not only admission metadata.");
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(cameraObject);
                world.StopBackgroundWork();
                world.Dispose();
            }
        }

        private static List<int3> BricksCovering(int3 min, int3 size)
        {
            const int blockEdgeLog2 = 3;
            int3 maxInclusive = min + size - 1;
            int3 minBrick = new int3(
                min.x >> blockEdgeLog2,
                min.y >> blockEdgeLog2,
                min.z >> blockEdgeLog2);
            int3 maxBrick = new int3(
                maxInclusive.x >> blockEdgeLog2,
                maxInclusive.y >> blockEdgeLog2,
                maxInclusive.z >> blockEdgeLog2);
            var result = new List<int3>();
            for (int z = minBrick.z; z <= maxBrick.z; z++)
            for (int y = minBrick.y; y <= maxBrick.y; y++)
            for (int x = minBrick.x; x <= maxBrick.x; x++)
                result.Add(new int3(x, y, z));
            return result;
        }

        private static int MaxSurfaceHeight(
            ShowcaseWorld world,
            int minX,
            int maxX,
            int minZ,
            int maxZ)
        {
            int max = 0;
            for (int z = minZ; z <= maxZ; z += 8)
            for (int x = minX; x <= maxX; x += 8)
                max = math.max(max, world.SurfaceHeight(x, z));
            return max;
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
