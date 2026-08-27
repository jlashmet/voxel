using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeInteriorScaleTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int MinimumInteriorSpanDm = 64;

        [Test]
        public void ProductionBuildingsMeetExpandedRoomAndCeilingMinimums()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            Assert.AreEqual(40, plan.Theme.FloorHeightDm,
                "Kentridge generated storeys should provide the raised 4.0 m floor-to-floor height.");

            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                int roomBearingStructures = 0;
                for (int roleId = 0; roleId < catalogue.Definitions.Length; roleId++)
                {
                    KentridgeRole role = (KentridgeRole)roleId;
                    if (role == KentridgeRole.Well)
                        continue;

                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[roleId];
                    primitives.Clear();
                    anchors.Clear();

                    ParameterSet parameters = FeatureGeneration.ResolveParameters(
                        in catalogue,
                        in definition,
                        in placement,
                        roleId,
                        placement.Position,
                        Seed);
                    EvaluationResult evaluation = ShapeProgram.Evaluate(
                        in catalogue,
                        roleId,
                        in parameters,
                        placement.Position,
                        placement.Orientation,
                        Seed,
                        FeatureGeneration.InstanceSeed(Seed, roleId, placement.Position),
                        primitives,
                        anchors);
                    Assert.AreEqual(EvaluationResult.Ok, evaluation,
                        role + " did not evaluate through the production shared-structure catalogue.");

                    long largestCarveVolume = -1;
                    int3 largestCarveSize = default;
                    int3 footprintMaxExclusive = placement.Position + definition.Footprint;

                    for (int i = 0; i < primitives.Length; i++)
                    {
                        Primitive primitive = primitives[i];
                        primitive.Bounds(out int3 min, out int3 max);
                        Assert.That(min.x, Is.GreaterThanOrEqualTo(placement.Position.x),
                            role + " escaped its reserved footprint on -X.");
                        Assert.That(max.x, Is.LessThan(footprintMaxExclusive.x),
                            role + " escaped its reserved footprint on +X.");
                        Assert.That(min.z, Is.GreaterThanOrEqualTo(placement.Position.z),
                            role + " escaped its reserved footprint on -Z.");
                        Assert.That(max.z, Is.LessThan(footprintMaxExclusive.z),
                            role + " escaped its reserved footprint on +Z.");

                        if (primitive.Shape != PrimitiveShape.Box
                            || primitive.Mode != PrimitiveMode.Carve)
                            continue;

                        int3 size = primitive.B - primitive.A + new int3(1, 1, 1);
                        long volume = (long)size.x * size.y * size.z;
                        if (volume > largestCarveVolume)
                        {
                            largestCarveVolume = volume;
                            largestCarveSize = size;
                        }
                    }

                    Assert.That(largestCarveVolume, Is.GreaterThan(0),
                        role + " did not emit a room-sized interior carve.");
                    int horizontalSpan = math.min(largestCarveSize.x, largestCarveSize.z);
                    Assert.That(horizontalSpan, Is.GreaterThanOrEqualTo(MinimumInteriorSpanDm),
                        role + " interior short span is still too cramped: " + horizontalSpan + " dm.");
                    Assert.That(largestCarveSize.y, Is.GreaterThanOrEqualTo(40),
                        role + " interior vertical carve is below the raised room-height target.");

                    roomBearingStructures++;
                }

                Assert.AreEqual(16, roomBearingStructures,
                    "Every Kentridge building except the open well should satisfy the room-scale contract.");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void ProductionCatalogue_LandmarkEntrancesCarryHeroVoussoirSeams()
        {
            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                int heroEntranceDefinitions = 0;
                for (int definitionId = 0; definitionId < catalogue.Definitions.Length; definitionId++)
                {
                    FeatureDefinition definition = catalogue.Definitions[definitionId];
                    int pc = definition.ProgramOffset;
                    int end = pc + definition.ProgramLength;
                    int masonrySeams = 0;
                    bool hasArchedClearance = false;

                    while (pc < end)
                    {
                        ShapeOp op = (ShapeOp)catalogue.Program[pc];
                        if (op == ShapeOp.End) break;

                        int instructionLength = ShapeOps.InstructionLength(op);
                        Assert.That(instructionLength, Is.GreaterThan(0),
                            "Production Kentridge emitted an unknown shape opcode in " +
                            definition.Name + ".");

                        if (op == ShapeOp.EmitCapsule
                            && (PrimitiveMode)catalogue.Program[pc + 12]
                                == PrimitiveMode.SurfaceDetail
                            && catalogue.Program[pc + 10] == SurfaceStyles.MasonryJoint)
                        {
                            masonrySeams++;
                        }

                        if (op == ShapeOp.EmitPrism
                            && (PrismProfile)catalogue.Program[pc + 8] == PrismProfile.Arch
                            && (PrimitiveMode)catalogue.Program[pc + 12] == PrimitiveMode.Carve)
                        {
                            hasArchedClearance = true;
                        }

                        pc += instructionLength;
                    }

                    if (hasArchedClearance && masonrySeams >= 12)
                    {
                        Assert.That(masonrySeams, Is.EqualTo(12),
                            "A landmark entrance should carry the 13-piece hero arch rhythm as " +
                            "twelve radial joints without multiplying that treatment onto glazing.");
                        heroEntranceDefinitions++;
                    }
                }

                Assert.That(heroEntranceDefinitions, Is.GreaterThanOrEqualTo(3),
                    "Warehouse, mansion, and church production programs should each carry the " +
                    "lookdev-derived voussoir treatment while retaining their arched clearance.");
            }
            finally
            {
                catalogue.Dispose();
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
