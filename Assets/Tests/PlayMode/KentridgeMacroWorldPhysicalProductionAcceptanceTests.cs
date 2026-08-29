using System;
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
            Assert.That(lake.Spec.Kind, Is.EqualTo(Game.WorldBuilder.Api.TopDownWorldRegionKind.WaterBody));

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
                Assert.That(definition.Footprint.x, Is.GreaterThanOrEqualTo(900));
                Assert.That(definition.Footprint.z, Is.GreaterThanOrEqualTo(450));
                Assert.That(
                    definition.Footprint.y,
                    Is.GreaterThanOrEqualTo(TopDownWorldWaterBodyVoxelCatalogue.MinimumDepthDm));
                AssertWaterProgramHasCarvedDepthAndNonSolidFill(
                    waterCatalogue,
                    definition,
                    settings.Materials.Resolve(MaterialRole.Water));
            }
            finally
            {
                if (waterCatalogue.IsCreated) waterCatalogue.Dispose();
            }

            TestContext.WriteLine(
                "MACRO_PHYSICAL_ACCEPTANCE " +
                $"maxRoadRiseVoxels={maximumRise} roadStepDm={TopDownWorldPhysicalPlanner.RouteTileStepDm} " +
                $"waterDepthVoxels={TopDownWorldWaterBodyVoxelCatalogue.DepthVoxels(lake, settings.VoxelsPerDecimetre)}");
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

        private static void AssertWaterProgramHasCarvedDepthAndNonSolidFill(
            FeatureCatalogue catalogue,
            FeatureDefinition definition,
            byte waterMaterial)
        {
            bool carved = false;
            bool filledWater = false;
            int deepestCarve = 0;
            int end = definition.ProgramOffset + definition.ProgramLength;
            for (int offset = definition.ProgramOffset; offset < end;)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[offset];
                int length = ShapeOps.InstructionLength(op);
                Assert.That(length, Is.GreaterThan(0));
                if (op == ShapeOp.EmitRoundedBox)
                {
                    int sizeY = catalogue.Program[offset + 6];
                    byte material = (byte)catalogue.Program[offset + 9];
                    var mode = (PrimitiveMode)catalogue.Program[offset + 12];
                    if (mode == PrimitiveMode.Carve)
                    {
                        carved = true;
                        deepestCarve = Math.Max(deepestCarve, sizeY);
                    }
                    if (mode == PrimitiveMode.Fill && material == waterMaterial)
                        filledWater = true;
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
