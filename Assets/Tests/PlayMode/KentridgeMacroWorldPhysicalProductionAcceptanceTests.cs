using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldPhysicalProductionAcceptanceTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int StrictRoadRiseVoxelsPerThreeMetres = 6;
        private const long MaximumWaterPrimitiveBoundingCells = 10_000_000L;

        [Test]
        public void PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody()
        {
            // Keep the full graph/settlement/geography regression in the one final CI target.
            new KentridgeMacroWorldPhysicalRealizationTests()
                .MacroGraphRealizesSettlementsTerrainAwareRoadsAndGeographyThroughProductionWorldBuilder();

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            var intent = KentridgeTopDownWorldPhysicalIntent.Build();
            VoxelWorldGenSettings settings = Settings();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalVoxelCatalogue.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                settings);

            int maximumRise = AssertStrictRoadRise(physical, settings.VoxelsPerDecimetre);
            Assert.That(
                physical.TryGetRegion(
                    KentridgeTopDownWorldPhysicalIntent.RossdamLake,
                    out TopDownWorldRegionPlan lake),
                Is.True);
            Assert.That(lake.Spec.Kind, Is.EqualTo(TopDownWorldRegionKind.WaterBody));
            AssertBanditSpurUsesDryAuthoredShoreline(physical, lake);
            AssertOrcVillageSkirtsSouthernRidge(intent, physical);

            long waterPrimitiveBoundingCells = 0L;
            FeatureCatalogue waterCatalogue = default;
            try
            {
                waterCatalogue = TopDownWorldWaterBodyVoxelCatalogue.Build(
                    physical,
                    Seed,
                    settings,
                    Allocator.Temp);
                Assert.That(waterCatalogue.IsCreated, Is.True);
                Assert.That(waterCatalogue.Definitions.Length, Is.EqualTo(1));
                FeatureDefinition definition = waterCatalogue.Definitions[0];
                StringAssert.StartsWith(
                    TopDownWorldWaterBodyVoxelCatalogue.DefinitionPrefix,
                    definition.Name.ToString());
                Assert.That(definition.Footprint.x, Is.EqualTo(900),
                    "The first-pass Rossdam lake must stay substantial without returning to its former streaming-cost footprint.");
                Assert.That(definition.Footprint.z, Is.EqualTo(450),
                    "The first-pass Rossdam lake must stay substantial without returning to its former streaming-cost footprint.");
                Assert.That(
                    TopDownWorldWaterBodyVoxelCatalogue.DepthVoxels(lake, settings.VoxelsPerDecimetre),
                    Is.EqualTo(TopDownWorldWaterBodyVoxelCatalogue.MinimumDepthDm),
                    "The first-pass lake uses the smallest accepted physical depth so carved-water streaming remains bounded.");
                Assert.That(
                    definition.Footprint.y,
                    Is.GreaterThanOrEqualTo(TopDownWorldWaterBodyVoxelCatalogue.MinimumDepthDm));
                waterPrimitiveBoundingCells = AssertWaterProgramHasCarvedDepthAndNonSolidFill(
                    waterCatalogue,
                    definition,
                    settings.Materials.Resolve(MaterialRole.Water));
                Assert.That(
                    waterPrimitiveBoundingCells,
                    Is.LessThanOrEqualTo(MaximumWaterPrimitiveBoundingCells),
                    "The carved/fill lake primitive scan envelope must remain compatible with ordinary streamed feature budgets.");
            }
            finally
            {
                if (waterCatalogue.IsCreated) waterCatalogue.Dispose();
            }

            AssertProductionCompositionContainsMacro(layout, settings);

            TestContext.WriteLine(
                "MACRO_PHYSICAL_ACCEPTANCE " +
                $"routes={physical.Routes.Count} routeTiles={physical.RouteTileCount} " +
                $"settlements={physical.Settlements.Count} buildings={physical.BuildingCount} " +
                $"constrainedRoutes={physical.GeographyConstrainedRouteCount} solveSteps={physical.RouteSolveSteps} " +
                $"maxRoadRiseVoxels={maximumRise} roadStepDm={TopDownWorldPhysicalPlanner.RouteTileStepDm} " +
                $"waterDepthVoxels={TopDownWorldWaterBodyVoxelCatalogue.DepthVoxels(lake, settings.VoxelsPerDecimetre)} " +
                $"waterPrimitiveBoundingCells={waterPrimitiveBoundingCells}");
        }

        private static void AssertBanditSpurUsesDryAuthoredShoreline(
            TopDownWorldPhysicalPlan physical,
            TopDownWorldRegionPlan lake)
        {
            Assert.That(
                physical.TryGetRoute(
                    MountingForceTopDownWorldDefinition.FightingArea1,
                    MountingForceTopDownWorldDefinition.BanditHideout,
                    out TopDownWorldPhysicalRoutePlan bandit),
                Is.True);
            Assert.That(
                bandit.GeographyConstrained,
                Is.True,
                "The verified bandit spur grazes the modern Rossdam lake footprint and must use an explicit dry routing solution.");
            int corridorMargin = bandit.Route.CorridorWidthDm / 2;
            for (var i = 0; i < bandit.Tiles.Count; i++)
            {
                Assert.That(
                    lake.Contains(bandit.Tiles[i], -corridorMargin),
                    Is.False,
                    "The authored Bandit Hideout shoreline route may not put the travel corridor in Rossdam Lake.");
            }
        }

        private static void AssertOrcVillageSkirtsSouthernRidge(
            TopDownWorldPhysicalIntentSpec intent,
            TopDownWorldPhysicalPlan physical)
        {
            TopDownWorldRouteRegionConstraintSpec orcConstraint = null;
            for (var i = 0; i < intent.RouteConstraints.Count; i++)
            {
                TopDownWorldRouteRegionConstraintSpec candidate = intent.RouteConstraints[i];
                if (!string.Equals(
                        candidate.FromId,
                        MountingForceTopDownWorldDefinition.SouthFightingArea,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        candidate.ToId,
                        MountingForceTopDownWorldDefinition.OrcVillage,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        candidate.RegionId,
                        KentridgeTopDownWorldPhysicalIntent.SouthernRidge,
                        StringComparison.Ordinal))
                    continue;
                Assert.That(orcConstraint, Is.Null, "The Orc ridge shoulder needs exactly one semantic solution.");
                orcConstraint = candidate;
            }

            Assert.That(orcConstraint, Is.Not.Null,
                "The verified Orc Village branch grazes the modern Logan ridge and must explicitly say how it remains walkable.");
            Assert.That(orcConstraint.SolutionKind, Is.EqualTo(TopDownWorldRouteRegionSolutionKind.GoAround),
                "The Orc branch should skirt the Logan ridge shoulder rather than invent a second mountain pass.");

            Assert.That(
                physical.TryGetRegion(
                    KentridgeTopDownWorldPhysicalIntent.SouthernRidge,
                    out TopDownWorldRegionPlan ridge),
                Is.True);
            Assert.That(
                physical.TryGetRoute(
                    MountingForceTopDownWorldDefinition.SouthFightingArea,
                    MountingForceTopDownWorldDefinition.OrcVillage,
                    out TopDownWorldPhysicalRoutePlan orc),
                Is.True);
            Assert.That(orc.GeographyConstrained, Is.True);

            int corridorMargin = orc.Route.CorridorWidthDm / 2;
            for (var i = 0; i < orc.Tiles.Count; i++)
            {
                Assert.That(
                    ridge.Contains(orc.Tiles[i], -corridorMargin),
                    Is.False,
                    "The authored Orc Village shoulder route may not put its travel corridor inside Southern Ridge.");
            }
        }

        private static void AssertProductionCompositionContainsMacro(
            TopDownWorldLayout layout,
            VoxelWorldGenSettings settings)
        {
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
                Assert.That(ContainsDefinitionStarting(combined, "macro-road-"), Is.True,
                    "The production Kentridge catalogue must consume the selected macro graph.");
                Assert.That(ContainsDefinitionStarting(combined, "macro-town-building-moordell-"), Is.True);
                Assert.That(ContainsDefinitionStarting(combined, "macro-town-building-rossdam-"), Is.True);
                Assert.That(ContainsDefinitionStarting(combined, "macro-town-building-fairy-village-"), Is.True);
                Assert.That(ContainsDefinitionStarting(combined, "macro-town-building-orc-village-"), Is.True);
                Assert.That(ContainsDefinitionStarting(combined, "macro-region-ridge-"), Is.True);
                Assert.That(
                    ContainsDefinitionStarting(combined, TopDownWorldWaterBodyVoxelCatalogue.DefinitionPrefix),
                    Is.True,
                    "The production composition must include the carved water-body pass, not only region paint.");
            }
            finally
            {
                if (combined.IsCreated) combined.Dispose();
            }
        }

        private static bool ContainsDefinitionStarting(FeatureCatalogue catalogue, string prefix)
        {
            for (var i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static int AssertStrictRoadRise(TopDownWorldPhysicalPlan physical, int scale)
        {
            int maximumRise = 0;
            for (var r = 0; r < physical.Routes.Count; r++)
            {
                TopDownWorldPhysicalRoutePlan route = physical.Routes[r];
                int previous = TerrainSampler.HeightAt(
                    route.Tiles[0].X * scale,
                    route.Tiles[0].Y * scale,
                    Seed);
                for (var p = 1; p < route.Tiles.Count; p++)
                {
                    int current = TerrainSampler.HeightAt(
                        route.Tiles[p].X * scale,
                        route.Tiles[p].Y * scale,
                        Seed);
                    int rise = Math.Abs(current - previous);
                    maximumRise = Math.Max(maximumRise, rise);
                    Assert.That(
                        rise,
                        Is.LessThanOrEqualTo(StrictRoadRiseVoxelsPerThreeMetres),
                        "A production macro road exceeds the strict CharacterMotor-oriented rise budget: " +
                        route.Route.Key + " at tile " + p);
                    previous = current;
                }
            }
            return maximumRise;
        }

        private static long AssertWaterProgramHasCarvedDepthAndNonSolidFill(
            FeatureCatalogue catalogue,
            FeatureDefinition definition,
            byte waterMaterial)
        {
            bool carved = false;
            bool filledWater = false;
            int deepestCarve = 0;
            long primitiveBoundingCells = 0L;
            int end = definition.ProgramOffset + definition.ProgramLength;
            for (int offset = definition.ProgramOffset; offset < end;)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[offset];
                int length = ShapeOps.InstructionLength(op);
                Assert.That(length, Is.GreaterThan(0));
                if (op == ShapeOp.EmitRoundedBox)
                {
                    int offsetY = catalogue.Program[offset + 3];
                    int sizeX = catalogue.Program[offset + 5];
                    int sizeY = catalogue.Program[offset + 6];
                    int sizeZ = catalogue.Program[offset + 7];
                    primitiveBoundingCells += (long)sizeX * sizeY * sizeZ;
                    byte material = (byte)catalogue.Program[offset + 9];
                    var mode = (PrimitiveMode)catalogue.Program[offset + 12];
                    if (mode == PrimitiveMode.Carve)
                    {
                        carved = true;
                        deepestCarve = Math.Max(deepestCarve, sizeY);
                    }
                    if (mode == PrimitiveMode.Fill && material == waterMaterial)
                    {
                        filledWater = true;
                        Assert.That(
                            offsetY,
                            Is.EqualTo(TopDownWorldWaterBodyVoxelCatalogue.MinimumDepthDm),
                            "Non-solid water should occupy the basin surface instead of refilling the carved depth.");
                        Assert.That(
                            sizeY,
                            Is.EqualTo(TopDownWorldWaterBodyVoxelCatalogue.WaterSurfaceThicknessDm),
                            "Streamed water should remain a thin presentation sheet over the full carved basin.");
                    }
                }
                offset += length;
                if (op == ShapeOp.End) break;
            }

            Assert.That(carved, Is.True, "A water region must alter occupancy to form a physical basin.");
            Assert.That(
                deepestCarve,
                Is.GreaterThanOrEqualTo(TopDownWorldWaterBodyVoxelCatalogue.MinimumDepthDm),
                "The lake must have player-readable physical depth rather than a surface repaint.");
            Assert.That(filledWater, Is.True, "The carved basin must use the configured water material.");
            return primitiveBoundingCells;
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
