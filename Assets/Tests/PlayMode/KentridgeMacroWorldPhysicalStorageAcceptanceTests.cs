using System;
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
        private const int BuildingFoundationDm = 8;
        private const int BuildingRoofDm = 24;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingTerrainSamplesPerAxis = 5;

        [Test]
        public void PhysicalMacroWorldReachesProductionStorageWithSettlementShellAndRoof()
        {
            // Keep graph/route/geography/water acceptance inside this single final CI target, then
            // discriminate definition-only output from all generic buildings reaching stored voxels.
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

            AssertStandalonePhysicalCatalogueRetainsGenericWater(layout, intent, settings);
            AssertGenericBuildingsAvoidBlockingRegions(physical);

            TopDownWorldLayoutSelection.Select(
                layout,
                KentridgeDefinition.TownCentreDm.X,
                KentridgeDefinition.TownCentreDm.Y,
                MountingForceTopDownWorldDefinition.CellSizeDm);

            FeatureCatalogue combined = default;
            try
            {
                combined = KentridgeCombinedVoxelCatalogue.Build(Seed, settings, Allocator.Temp);
                Assert.That(combined.IsCreated, Is.True);
                Assert.That(
                    CountDefinitionsStarting(combined, "macro-region-water-"),
                    Is.Zero,
                    "Kentridge production composition must not retain the generic rectangular WaterBody painter when the carved basin owns water.");
                Assert.That(
                    CountDefinitionsStarting(combined, TopDownWorldWaterBodyVoxelCatalogue.DefinitionPrefix),
                    Is.EqualTo(1),
                    "Rossdam water must have exactly one dedicated carved-basin owner in production composition.");

                int genericSettlements = 0;
                int verifiedBuildings = 0;
                for (var s = 0; s < physical.Settlements.Count; s++)
                {
                    TopDownWorldSettlementPlan settlement = physical.Settlements[s];
                    if (settlement.RealizationKind != TopDownWorldSettlementRealizationKind.GenericBlockout)
                        continue;

                    genericSettlements++;
                    Assert.That(
                        settlement.Buildings.Count,
                        Is.GreaterThanOrEqualTo(4),
                        settlement.Node.Id + " must retain at least four generic blockout buildings.");
                    for (var b = 0; b < settlement.Buildings.Count; b++)
                    {
                        AssertBuildingGroundingAndStorage(
                            combined,
                            settlement,
                            b,
                            settings);
                        verifiedBuildings++;
                    }
                }

                Assert.That(genericSettlements, Is.EqualTo(4));
                Assert.That(verifiedBuildings, Is.EqualTo(physical.BuildingCount));
                Assert.That(verifiedBuildings, Is.EqualTo(16));
                TestContext.WriteLine(
                    "MACRO_SETTLEMENT_STORAGE " +
                    $"settlements={genericSettlements} buildings={verifiedBuildings} " +
                    $"genericWaterOwners=0 basinWaterOwners=1");
            }
            finally
            {
                if (combined.IsCreated) combined.Dispose();
            }
        }

        private static void AssertStandalonePhysicalCatalogueRetainsGenericWater(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalIntentSpec intent,
            VoxelWorldGenSettings settings)
        {
            FeatureCatalogue standalone = default;
            try
            {
                standalone = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    KentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    settings,
                    Allocator.Temp);
                Assert.That(
                    CountDefinitionsStarting(standalone, "macro-region-water-"),
                    Is.EqualTo(1),
                    "The reusable standalone physical catalogue must preserve its existing generic WaterBody output by default.");
            }
            finally
            {
                if (standalone.IsCreated) standalone.Dispose();
            }
        }

        private static void AssertGenericBuildingsAvoidBlockingRegions(TopDownWorldPhysicalPlan physical)
        {
            for (var s = 0; s < physical.Settlements.Count; s++)
            {
                TopDownWorldSettlementPlan settlement = physical.Settlements[s];
                if (settlement.RealizationKind != TopDownWorldSettlementRealizationKind.GenericBlockout)
                    continue;

                for (var b = 0; b < settlement.Buildings.Count; b++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[b];
                    for (var r = 0; r < physical.Regions.Count; r++)
                    {
                        TopDownWorldRegionPlan region = physical.Regions[r];
                        if (region.Spec.Kind != TopDownWorldRegionKind.WaterBody
                            && region.Spec.Relation != TopDownWorldRegionRelationKind.Separates)
                            continue;

                        int buildingHalfX = building.HalfExtentXDm + BuildingFoundationInsetDm;
                        int buildingHalfZ = building.HalfExtentZDm + BuildingFoundationInsetDm;
                        bool overlapsX = Math.Abs(building.CentreDm.X - region.CentreDm.X)
                            < buildingHalfX + region.HalfExtentXDm;
                        bool overlapsZ = Math.Abs(building.CentreDm.Y - region.CentreDm.Y)
                            < buildingHalfZ + region.HalfExtentZDm;
                        Assert.That(
                            overlapsX && overlapsZ,
                            Is.False,
                            settlement.Node.Id + " building " + b +
                            " overlaps blocking/separative macro region " + region.Spec.Id + ".");
                    }
                }
            }
        }

        private static void AssertBuildingGroundingAndStorage(
            FeatureCatalogue combined,
            TopDownWorldSettlementPlan settlement,
            int buildingIndex,
            VoxelWorldGenSettings settings)
        {
            TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[buildingIndex];
            int scale = settings.VoxelsPerDecimetre;
            SampleTerrainRelief(building, scale, out int minimumGround, out int maximumGround);
            int terrainRelief = maximumGround - minimumGround;
            string definitionName = "macro-town-building-" + settlement.Node.Id + "-" + buildingIndex;
            int definitionIndex = FindDefinition(combined, definitionName);
            Assert.That(definitionIndex, Is.GreaterThanOrEqualTo(0), definitionName + " is missing from production composition.");

            FeatureDefinition definition = combined.Definitions[definitionIndex];
            ExplicitPlacement placement = FindPlacement(combined, definitionIndex);
            Assert.That(
                placement.Position.x,
                Is.EqualTo((building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm) * scale));
            Assert.That(placement.Position.y, Is.EqualTo(minimumGround),
                definitionName + " must begin at the sampled low terrain point so its foundation can absorb relief.");
            Assert.That(
                placement.Position.z,
                Is.EqualTo((building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm) * scale));
            Assert.That(
                definition.Footprint.y,
                Is.EqualTo(terrainRelief + (building.HeightDm + BuildingRoofDm) * scale),
                definitionName + " must include local terrain relief in its vertical envelope.");

            AssertBuildingProgramGrounding(
                combined,
                definition,
                terrainRelief,
                building,
                settings);

            // Wall and roof probes are expressed from the sampled high terrain, not the feature's
            // low-side origin. They therefore prove the shell remains exposed across local relief.
            var timberVoxel = new int3(
                building.CentreDm.X * scale,
                maximumGround + 10 * scale,
                building.CentreDm.Y * scale);
            var roofVoxel = new int3(
                building.CentreDm.X * scale,
                maximumGround + building.HeightDm * scale,
                building.CentreDm.Y * scale);

            var table = new RegionTable(4, Allocator.Temp);
            var pool = new BrickPool(8192, Allocator.Temp);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                int3 timberRegion = timberVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;
                int3 roofRegion = roofVoxel >> VoxelDimensions.RegionVoxelEdgeLog2;

                GenerateRegion(combined, timberRegion, ref table, in reads, in mutations);
                if (!roofRegion.Equals(timberRegion))
                    GenerateRegion(combined, roofRegion, ref table, in reads, in mutations);

                byte timber = VoxelAccess.GetVoxel(ref table, in pool, timberVoxel);
                byte roof = VoxelAccess.GetVoxel(ref table, in pool, roofVoxel);
                Assert.That(
                    timber,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.Timber)),
                    definitionName + " must contain an exposed timber wall volume above the highest sampled local terrain.");
                Assert.That(
                    roof,
                    Is.EqualTo(settings.Materials.Resolve(MaterialRole.RoofTile)),
                    definitionName + " must contain a gable roof above the highest sampled local terrain.");

                TestContext.WriteLine(
                    "MACRO_BUILDING_STORAGE " +
                    $"settlement={settlement.Node.Id} building={buildingIndex} minGround={minimumGround} " +
                    $"maxGround={maximumGround} relief={terrainRelief} timberVoxel={timberVoxel} roofVoxel={roofVoxel} " +
                    $"bricks={pool.AllocatedCount}");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static void GenerateRegion(
            FeatureCatalogue combined,
            int3 region,
            ref RegionTable table,
            in RegionReadSource reads,
            in RegionMutationStore mutations)
        {
            table.LoadRegion(region);
            FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                in combined,
                Seed,
                region,
                reads,
                mutations);
            Assert.That(report.BudgetExceeded, Is.False,
                "A production generic-building region may not truncate authored primitives.");
            Assert.That(report.VoxelsWritten, Is.GreaterThan(0));
        }

        private static void AssertBuildingProgramGrounding(
            FeatureCatalogue combined,
            FeatureDefinition definition,
            int terrainRelief,
            TopDownWorldBuildingBlockoutPlan building,
            VoxelWorldGenSettings settings)
        {
            int scale = settings.VoxelsPerDecimetre;
            int foundationTop = terrainRelief + BuildingFoundationDm * scale;
            int roofBase = terrainRelief + building.HeightDm * scale;
            int offset = definition.ProgramOffset;

            Assert.That((ShapeOp)combined.Program[offset], Is.EqualTo(ShapeOp.EmitBox));
            Assert.That(combined.Program[offset + 3], Is.EqualTo(0));
            Assert.That(combined.Program[offset + 6], Is.EqualTo(foundationTop),
                "Foundation must span sampled terrain relief plus the normal cap above the high point.");
            Assert.That((byte)combined.Program[offset + 8], Is.EqualTo(settings.Materials.Resolve(MaterialRole.FoundationStone)));

            offset += ShapeOps.InstructionLength(ShapeOp.EmitBox);
            for (var wall = 0; wall < 4; wall++)
            {
                Assert.That((ShapeOp)combined.Program[offset], Is.EqualTo(ShapeOp.EmitBox),
                    "Generic building shell must retain all four bounded timber wall boxes.");
                Assert.That(combined.Program[offset + 3], Is.EqualTo(foundationTop),
                    "Timber walls must begin above the sampled terrain high point.");
                Assert.That((byte)combined.Program[offset + 8], Is.EqualTo(settings.Materials.Resolve(MaterialRole.Timber)));
                offset += ShapeOps.InstructionLength(ShapeOp.EmitBox);
            }

            Assert.That((ShapeOp)combined.Program[offset], Is.EqualTo(ShapeOp.EmitPrism));
            Assert.That(combined.Program[offset + 3], Is.EqualTo(roofBase));
            Assert.That((byte)combined.Program[offset + 9], Is.EqualTo(settings.Materials.Resolve(MaterialRole.RoofTile)));
        }

        private static void SampleTerrainRelief(
            TopDownWorldBuildingBlockoutPlan building,
            int scale,
            out int minimumGround,
            out int maximumGround)
        {
            int leftDm = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
            int rightDm = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
            int backDm = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
            int frontDm = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
            minimumGround = int.MaxValue;
            maximumGround = int.MinValue;

            for (var x = 0; x < BuildingTerrainSamplesPerAxis; x++)
            {
                int xDm = leftDm + (rightDm - leftDm) * x / (BuildingTerrainSamplesPerAxis - 1);
                for (var z = 0; z < BuildingTerrainSamplesPerAxis; z++)
                {
                    int zDm = backDm + (frontDm - backDm) * z / (BuildingTerrainSamplesPerAxis - 1);
                    int ground = TerrainSampler.HeightAt(xDm * scale, zDm * scale, Seed);
                    minimumGround = Math.Min(minimumGround, ground);
                    maximumGround = Math.Max(maximumGround, ground);
                }
            }
        }

        private static int FindDefinition(FeatureCatalogue catalogue, string name)
        {
            for (var i = 0; i < catalogue.Definitions.Length; i++)
                if (string.Equals(catalogue.Definitions[i].Name.ToString(), name, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static ExplicitPlacement FindPlacement(FeatureCatalogue catalogue, int definitionIndex)
        {
            for (var i = 0; i < catalogue.Rules.Length; i++)
            {
                PlacementRule rule = catalogue.Rules[i];
                if (rule.DefinitionId != definitionIndex) continue;
                Assert.That(rule.ExplicitCount, Is.EqualTo(1));
                return catalogue.ExplicitPlacements[rule.ExplicitOffset];
            }

            Assert.Fail("Missing explicit placement rule for definition " + definitionIndex + ".");
            return default;
        }

        private static int CountDefinitionsStarting(FeatureCatalogue catalogue, string prefix)
        {
            int count = 0;
            for (var i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                    count++;
            return count;
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
