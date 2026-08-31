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

        private static readonly int3 UpperMarkedProbe = new int3(934, CapturedMayorHouseSurfaceY + 1, 299);
        private static readonly int3 LowerMarkedProbe = new int3(958, CapturedMayorHouseSurfaceY + 1, 306);

        [Test]
        public void SceneIssue20260826132234356FinalFoundationDoesNotOwnMarkedGroundBand()
        {
            SettlementPlan plan = KentridgeDefinition.Build(VoxelShowcaseSeed);
            VoxelWorldGenSettings settings = BuildSettings(plan);
            FeatureCatalogue structures = KentridgeSharedStructureVoxelCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Persistent);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                VoxelShowcaseSeed, settings, Allocator.Persistent);
            var primitives = new NativeList<Primitive>(64, Allocator.Persistent);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Persistent);
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(16384, Allocator.Persistent);

            try
            {
                int roleId = (int)KentridgeRole.MayorHouse;
                FeatureDefinition definition = structures.Definitions[roleId];
                PlacementRule rule = structures.Rules[roleId];
                Assert.AreEqual(roleId, rule.DefinitionId);
                Assert.AreEqual(1, rule.ExplicitCount);

                ExplicitPlacement placement = structures.ExplicitPlacements[rule.ExplicitOffset];
                Assert.AreEqual(
                    CapturedMayorHouseSurfaceY - KentridgeDefinition.Theme.FoundationHeightDm,
                    placement.Position.y,
                    "Generated houses must sink by the compiler's full foundation depth.");

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
                Assert.LessOrEqual(foundationMax.y, CapturedMayorHouseSurfaceY,
                    "The generated foundation still protrudes above the authored plot surface.");
                AssertHorizontalContains(foundationMin, foundationMax, UpperMarkedProbe,
                    "Upper marked camera probe no longer exercises the production MayorHouse foundation owner.");
                AssertHorizontalContains(foundationMin, foundationMax, LowerMarkedProbe,
                    "Lower marked camera probe no longer exercises the production MayorHouse foundation owner.");

                int3 region = new int3(
                    UpperMarkedProbe.x / VoxelGrid.RegionVoxelEdge,
                    UpperMarkedProbe.y / VoxelGrid.RegionVoxelEdge,
                    UpperMarkedProbe.z / VoxelGrid.RegionVoxelEdge);
                Assert.AreEqual(region.x, LowerMarkedProbe.x / VoxelGrid.RegionVoxelEdge);
                Assert.AreEqual(region.y, LowerMarkedProbe.y / VoxelGrid.RegionVoxelEdge);
                Assert.AreEqual(region.z, LowerMarkedProbe.z / VoxelGrid.RegionVoxelEdge);

                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in combined, VoxelShowcaseSeed, region, reads, mutations);
                Assert.Greater(report.VoxelsWritten, 0,
                    "The regression must rasterize the final production Kentridge catalogue into authoritative storage.");

                AssertEmptyAboveSurface(ref table, in pool, UpperMarkedProbe, "upper");
                AssertEmptyAboveSurface(ref table, in pool, LowerMarkedProbe, "lower");
                AssertSolidSupportBelowSurface(ref table, in pool, UpperMarkedProbe, "upper");
                AssertSolidSupportBelowSurface(ref table, in pool, LowerMarkedProbe, "lower");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                anchors.Dispose();
                primitives.Dispose();
                combined.Dispose();
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

        private static void AssertHorizontalContains(int3 min, int3 max, int3 point, string message)
        {
            Assert.IsTrue(
                point.x >= min.x && point.x <= max.x && point.z >= min.z && point.z <= max.z,
                message + $" bounds=({min.x},{min.z})..({max.x},{max.z}) point=({point.x},{point.z})");
        }

        private static void AssertEmptyAboveSurface(ref RegionTable table, in BrickPool pool, int3 point, string label)
        {
            VoxelCell cell = VoxelAccess.GetCell(ref table, in pool, point);
            Assert.IsFalse(cell.IsSolid,
                $"The final combined catalogue still leaves solid geometry in the {label} marked ground band one voxel above the intended surface (material {cell.BaseMaterialId}).");
        }

        private static void AssertSolidSupportBelowSurface(ref RegionTable table, in BrickPool pool, int3 abovePoint, string label)
        {
            int3 belowPoint = new int3(abovePoint.x, CapturedMayorHouseSurfaceY - 1, abovePoint.z);
            VoxelCell cell = VoxelAccess.GetCell(ref table, in pool, belowPoint);
            Assert.IsTrue(cell.IsSolid,
                $"The {label} probe lost solid support below the house; the fix must sink the foundation, not remove occupancy.");
        }

        private static VoxelWorldGenSettings BuildSettings(SettlementPlan plan)
        {
            var materials = new VoxelMaterialMap(
                foundationStone: FoundationMaterial, masonry: 2, darkMasonry: 6,
                timber: 3, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials, plan);
        }
    }
}
