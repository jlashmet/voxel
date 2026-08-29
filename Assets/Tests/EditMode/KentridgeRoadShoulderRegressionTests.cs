using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeRoadShoulderRegressionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void WorldBuilderRoadsUseOneContinuousInfluenceInsteadOfRepeatedShoulderBands()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            FeatureCatalogue roads = KentridgeDirectedTownSurfaceCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                Assert.AreEqual(plan.Routes.Count, network.Routes.Count);
                Assert.AreEqual(plan.Routes.Count, roads.Definitions.Length,
                    "Each semantic route should lower through one shared bounded road definition.");

                for (int i = 0; i < roads.Definitions.Length; i++)
                {
                    FeatureDefinition definition = roads.Definitions[i];
                    StringAssert.StartsWith("world-road-", definition.Name.ToString());

                    int roadSurfaceFills = 0;
                    int grassyTransitionFills = 0;
                    int carveOps = 0;
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;
                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)roads.Program[pc];
                        if (op == ShapeOp.EmitBox)
                        {
                            byte material = (byte)roads.Program[pc + 8];
                            PrimitiveMode mode = (PrimitiveMode)roads.Program[pc + 11];
                            if (mode == PrimitiveMode.Carve) carveOps++;
                            else if (mode == PrimitiveMode.Fill && material == 13) roadSurfaceFills++;
                            else if (mode == PrimitiveMode.Fill && material == 14) grassyTransitionFills++;
                        }

                        pc += ShapeOps.InstructionLength(op);
                        if (op == ShapeOp.End) break;
                    }

                    Assert.AreEqual(1, carveOps,
                        definition.Name + " should cut one shared grade corridor.");
                    Assert.AreEqual(1, roadSurfaceFills,
                        definition.Name + " should fill one Dirt carriageway core.");
                    Assert.AreEqual(1, grassyTransitionFills,
                        definition.Name + " should use one natural-terrain transition footprint, not repeated bands.");
                    Assert.That(definition.MaxPrimitives, Is.LessThanOrEqualTo(4),
                        definition.Name + " must keep the shared road primitive budget bounded.");
                }

                WorldRoadNetworkRoute route = network.Routes[0];
                ResolvedWorldRoadPoint point = route.Road.Points[0];
                int previousCoverage = 32;
                for (int offset = 0; offset <= route.GradeRadiusDm; offset++)
                {
                    Assert.IsTrue(network.TrySample(point.Xdm + offset, point.Zdm, out WorldRoadNetworkSample sample),
                        "Influence should continuously cover every decimetre through the graded shoulder.");
                    Assert.LessOrEqual(sample.Influence.Coverage31, previousCoverage,
                        "Road influence must recover monotonically toward natural terrain.");
                    previousCoverage = sample.Influence.Coverage31;
                }

                Assert.IsTrue(network.TrySampleClearance(
                    point.Xdm + route.ClearanceRadiusDm, point.Zdm, out WorldRoadNetworkSample clearance));
                Assert.Greater(clearance.ClearanceCoverage31, 0,
                    "Vegetation/placement clearance must extend through the full authored road corridor.");
            }
            finally
            {
                roads.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
