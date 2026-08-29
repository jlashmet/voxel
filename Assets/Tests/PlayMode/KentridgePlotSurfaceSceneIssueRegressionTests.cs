using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgePlotSurfaceSceneIssueRegressionTests
    {
        private const uint VoxelShowcaseSeed = 1592594996u;
        private const int CapturedMayorHouseSurfaceY = 221;
        private const byte FoundationMaterial = 1;

        [Test]
        public void SceneIssue20260826132234356GeneratedFoundationEndsBelowCapturedGroundSurface()
        {
            FeatureCatalogue structures = KentridgeSharedStructureVoxelCatalogue.Build(
                VoxelShowcaseSeed, BuildSettings(), Allocator.Persistent);
            var primitives = new NativeList<Primitive>(64, Allocator.Persistent);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Persistent);
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                int roleId = (int)KentridgeRole.MayorHouse;
                FeatureDefinition definition = structures.Definitions[roleId];
                PlacementRule rule = structures.Rules[roleId];
                Assert.AreEqual(roleId, rule.DefinitionId);
                Assert.AreEqual(1, rule.ExplicitCount);

                ExplicitPlacement placement = structures.ExplicitPlacements[rule.ExplicitOffset];
                Assert.AreEqual(CapturedMayorHouseSurfaceY - KentridgeDefinition.Theme.FoundationHeightDm,
                    placement.Position.y,
                    "Generated houses must sink by the compiler's full foundation depth; the old 5dm sink exposed a 2dm rectangular cap in both marked dirt/grass regions.");

                ParameterSet parameters = FeatureGeneration.ResolveParameters(
                    in structures, in definition, in placement,
                    roleId, placement.Position, VoxelShowcaseSeed);
                ulong instanceSeed = FeatureGeneration.InstanceSeed(
                    VoxelShowcaseSeed, roleId, placement.Position);
                EvaluationResult evaluation = ShapeProgram.Evaluate(
                    in structures, roleId, in parameters,
                    placement.Position, placement.Orientation,
                    VoxelShowcaseSeed, instanceSeed, primitives, anchors);
                Assert.AreEqual(EvaluationResult.Ok, evaluation);

                Primitive foundation = FindFoundation(primitives);
                foundation.Bounds(out int3 foundationMin, out int3 foundationMax);
                Assert.AreEqual(CapturedMayorHouseSurfaceY - 1, foundationMax.y,
                    "The generated foundation's last occupied voxel must remain below the authored plot surface.");

                int3 sample = new int3(
                    foundationMin.x + (foundationMax.x - foundationMin.x) / 2,
                    CapturedMayorHouseSurfaceY,
                    foundationMin.z + (foundationMax.z - foundationMin.z) / 2);
                int3 region = new int3(
                    sample.x / VoxelGrid.RegionVoxelEdge,
                    sample.y / VoxelGrid.RegionVoxelEdge,
                    sample.z / VoxelGrid.RegionVoxelEdge);

                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in structures, VoxelShowcaseSeed, region, reads, mutations);
                Assert.Greater(report.VoxelsWritten, 0,
                    "The regression must rasterize the production MayorHouse into authoritative storage.");

                VoxelCell surface = VoxelAccess.GetCell(ref table, in pool, sample);
                Assert.AreNotEqual(FoundationMaterial, surface.BaseMaterialId,
                    "A Foundation voxel still owns the captured ground-surface band.");

                int3 belowPoint = new int3(sample.x, CapturedMayorHouseSurfaceY - 1, sample.z);
                VoxelCell below = VoxelAccess.GetCell(ref table, in pool, belowPoint);
                Assert.AreEqual(FoundationMaterial, below.BaseMaterialId,
                    "The fix must sink the generated foundation, not remove structural support beneath the house.");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                anchors.Dispose();
                primitives.Dispose();
                structures.Dispose();
            }
        }

        private static Primitive FindFoundation(NativeList<Primitive> primitives)
        {
            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Mode == PrimitiveMode.Fill && primitive.Material == FoundationMaterial)
                    return primitive;
            }

            Assert.Fail("MayorHouse emitted no production Foundation primitive.");
            return default;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: FoundationMaterial, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
