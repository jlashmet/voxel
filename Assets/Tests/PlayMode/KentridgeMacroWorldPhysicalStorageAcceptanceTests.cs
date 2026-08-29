using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldPhysicalStorageAcceptanceTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PhysicalMacroWorldReachesProductionStorageWithSettlementShellAndRoof()
        {
            // Keep the existing graph/route/geography/water acceptance inside the same final CI
            // target, then discriminate definition-only settlement output from real stored voxels.
            new KentridgeMacroWorldPhysicalProductionAcceptanceTests()
                .PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody();

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            TopDownWorldSettlementPlan settlement = FindGenericSettlementWithBuilding(physical);
            TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[0];
            int scale = settings.VoxelsPerDecimetre;
            int ground = TerrainSampler.HeightAt(
                building.CentreDm.X * scale,
                building.CentreDm.Y * scale,
                Seed);

            // BuildingProgram emits the filled timber volume from +8 dm to HeightDm, and starts
            // the gable prism exactly at HeightDm. Sampling the horizontal centre avoids all roof
            // edge/profile ambiguity and proves both above-ground authored material passes survive
            // the real combined catalogue and rasterisation/storage path.
            var timberVoxel = new int3(
                building.CentreDm.X * scale,
                ground + 10 * scale,
                building.CentreDm.Y * scale);
            var roofVoxel = new int3(
                building.CentreDm.X * scale,
                ground + building.HeightDm * scale,
                building.CentreDm.Y * scale);

            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue combined = default;
            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(8192, Allocator.Temp);
            try
            {
                combined = KentridgeCombinedVoxelCatalogue.Build(Seed, settings, Allocator.Temp);
                Assert.That(combined.IsCreated, Is.True);

                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                int3 timberRegion = timberVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;
                int3 roofRegion = roofVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;

                table.LoadRegion(timberRegion);
                FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                    in combined,
                    Seed,
                    timberRegion,
                    reads,
                    mutations);
                Assert.That(report.BudgetExceeded, Is.False,
                    "The production settlement region may not truncate authored primitives.");
                Assert.That(report.VoxelsWritten, Is.GreaterThan(0));

                if (!roofRegion.Equals(timberRegion))
                {
                    table.LoadRegion(roofRegion);
                    FeatureGenerationReport roofReport = FeatureGeneration.GenerateRegion(
                        in combined,
                        Seed,
                        roofRegion,
                        reads,
                        mutations);
                    Assert.That(roofReport.BudgetExceeded, Is.False,
                        "The production roof region may not truncate authored primitives.");
                    Assert.That(roofReport.VoxelsWritten, Is.GreaterThan(0));
                }

                byte timber = VoxelAccess.GetVoxel(ref table, in pool, timberVoxel);
                byte roof = VoxelAccess.GetVoxel(ref table, in pool, roofVoxel);
                Assert.That(
                    timber,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.Timber)),
                    settlement.Node.Id + " must contain a real above-ground timber building volume in final production storage.");
                Assert.That(
                    roof,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.RoofTile)),
                    settlement.Node.Id + " must contain a real gable roof in final production storage.");

                TestContext.WriteLine(
                    "MACRO_SETTLEMENT_STORAGE " +
                    $"settlement={settlement.Node.Id} timberVoxel={timberVoxel} roofVoxel={roofVoxel} " +
                    $"region={timberRegion} voxelsWritten={report.VoxelsWritten} bricks={pool.AllocatedCount}");
            }
            finally
            {
                if (combined.IsCreated) combined.Dispose();
                table.Dispose();
                pool.Dispose();
            }
        }

        private static TopDownWorldSettlementPlan FindGenericSettlementWithBuilding(
            TopDownWorldPhysicalPlan physical)
        {
            for (var i = 0; i < physical.Settlements.Count; i++)
            {
                TopDownWorldSettlementPlan candidate = physical.Settlements[i];
                if (candidate.RealizationKind == TopDownWorldSettlementRealizationKind.GenericBlockout
                    && candidate.Buildings.Count > 0)
                    return candidate;
            }

            Assert.Fail("The production Kentridge macro plan must contain at least one generic settlement blockout.");
            return null;
        }

        private static VoxelWorldGenSettings Settings()
        {
            return new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
                    foundationStone: 20,
                    masonry: 18,
                    darkMasonry: 6,
                    timber: 2,
                    glass: 4,
                    warmWindow: 15,
                    roofTile: 8,
                    slate: 7,
                    cloth: 9,
                    moss: 14,
                    water: 11,
                    roadSurface: 13));
        }
    }
}
