using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeGeneratedEntranceAlignmentTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PubPhysicalDoorAndAnchorsHonorArchitectureDoorOffset()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            BuildingPlot pubPlot = FindPub(plan);
            StructureIntent intent = KentridgeDefinition.StructureIntent(pubPlot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);

            Assert.AreEqual(-12, form.DoorOffsetDm,
                "This regression must exercise Kentridge's deliberately off-centre Pub entrance.");

            int scale = settings.VoxelsPerDecimetre;
            int doorWidth = 13 * scale;
            int wallThickness = plan.Theme.WallThicknessDm * scale;
            int foundation = plan.Theme.FoundationHeightDm * scale;
            int doorHeight = plan.Theme.DoorHeightDm * scale;
            Int3 envelopeDm = SettlementFootprints.For(plan, pubPlot.Archetype);
            int width = form.WidthDm * scale;
            int x0 = (envelopeDm.X * scale - width) / 2;
            int z0 = 10 * scale;
            int sideClearance = 7 * scale;
            int localDoorX = width / 2 - doorWidth / 2 + form.DoorOffsetDm * scale;
            localDoorX = System.Math.Max(
                sideClearance,
                System.Math.Min(width - doorWidth - sideClearance, localDoorX));
            int expectedDoorX = x0 + localDoorX;
            int expectedDoorCenterX = expectedDoorX + doorWidth / 2;

            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            try
            {
                FeatureDefinition pub = catalogue.Definitions[(int)KentridgeRole.Pub];
                AnchorSpec publishedDoor = catalogue.Anchors[pub.AnchorOffset];
                Assert.AreEqual("door", publishedDoor.Name.ToString());
                Assert.AreEqual(expectedDoorCenterX, publishedDoor.LocalPosition.x,
                    "The published Pub door anchor must consume the architecture-owned door offset.");
                Assert.AreEqual(foundation, publishedDoor.LocalPosition.y);
                Assert.AreEqual(z0, publishedDoor.LocalPosition.z);

                bool foundPhysicalDoor = false;
                bool foundProgramDoorAnchor = false;
                int pc = pub.ProgramOffset;
                int end = pub.ProgramOffset + pub.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    int length = ShapeOps.InstructionLength(op);
                    Assert.GreaterOrEqual(length, 2);

                    if (op == ShapeOp.EmitBox
                        && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Carve
                        && catalogue.Program[pc + 3] == foundation
                        && catalogue.Program[pc + 4] == z0
                        && catalogue.Program[pc + 5] == doorWidth
                        && catalogue.Program[pc + 6] == doorHeight
                        && catalogue.Program[pc + 7] == wallThickness)
                    {
                        foundPhysicalDoor = true;
                        Assert.AreEqual(expectedDoorX, catalogue.Program[pc + 2],
                            "The physical front-wall carve must begin at the architecture-owned Pub door offset.");
                    }
                    else if (op == ShapeOp.SetAnchor
                             && catalogue.Program[pc + 2] == 0)
                    {
                        foundProgramDoorAnchor = true;
                        Assert.AreEqual(expectedDoorCenterX, catalogue.Program[pc + 3],
                            "The shared house bytecode door anchor must follow the physical explicit opening.");
                        Assert.AreEqual(foundation, catalogue.Program[pc + 4]);
                        Assert.AreEqual(z0, catalogue.Program[pc + 5]);
                    }

                    pc += length;
                    if (op == ShapeOp.End) break;
                }

                Assert.IsTrue(foundPhysicalDoor,
                    "Generated Pub program must contain its physical front-door carve.");
                Assert.IsTrue(foundProgramDoorAnchor,
                    "Generated Pub program must contain its main-door SetAnchor instruction.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static BuildingPlot FindPub(SettlementPlan plan)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == (int)KentridgeRole.Pub)
                    return plan.Plots[i];

            Assert.Fail("Kentridge settlement must contain its stable Pub role.");
            return default;
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
