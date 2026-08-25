using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
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
            BuildingPlot pubPlot = FindRole(plan, KentridgeRole.Pub);
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
                int3 transform = int3.zero;
                var transformStack = new Stack<int3>();
                int pc = pub.ProgramOffset;
                int end = pub.ProgramOffset + pub.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    int length = ShapeOps.InstructionLength(op);
                    Assert.GreaterOrEqual(length, 2);

                    if (op == ShapeOp.PushTransform)
                    {
                        transformStack.Push(transform);
                        transform += new int3(
                            catalogue.Program[pc + 2],
                            catalogue.Program[pc + 3],
                            catalogue.Program[pc + 4]);
                    }
                    else if (op == ShapeOp.PopTransform)
                    {
                        Assert.IsNotEmpty(transformStack,
                            "Translated Pub program popped an empty transform stack.");
                        transform = transformStack.Pop();
                    }
                    else if (op == ShapeOp.EmitBox
                             && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Carve
                             && catalogue.Program[pc + 3] + transform.y == foundation
                             && catalogue.Program[pc + 4] + transform.z == z0
                             && catalogue.Program[pc + 5] == doorWidth
                             && catalogue.Program[pc + 6] == doorHeight
                             && catalogue.Program[pc + 7] == wallThickness)
                    {
                        foundPhysicalDoor = true;
                        Assert.AreEqual(expectedDoorX, catalogue.Program[pc + 2] + transform.x,
                            "The physical front-wall carve must begin at the architecture-owned Pub door offset.");
                    }
                    else if (op == ShapeOp.SetAnchor
                             && catalogue.Program[pc + 2] == 0)
                    {
                        foundProgramDoorAnchor = true;
                        Assert.AreEqual(expectedDoorCenterX, catalogue.Program[pc + 3] + transform.x,
                            "The shared house bytecode door anchor must follow the physical explicit opening.");
                        Assert.AreEqual(foundation, catalogue.Program[pc + 4] + transform.y);
                        Assert.AreEqual(z0, catalogue.Program[pc + 5] + transform.z);
                    }

                    pc += length;
                    if (op == ShapeOp.End) break;
                }

                Assert.IsEmpty(transformStack,
                    "Generated Pub program must balance its local transform stack.");
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

        [Test]
        public void MedrareHouseKeepsBothFrontageWindowsClearOfEntranceCanopy()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            BuildingPlot medrarePlot = FindRole(plan, KentridgeRole.MedrareHouse);
            StructureIntent intent = KentridgeDefinition.StructureIntent(medrarePlot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);

            Assert.AreEqual(FrontageRhythm.Asymmetric, form.FrontageRhythm,
                "The captured regression depends on Medrare House's asymmetric two-bay frontage.");
            Assert.AreEqual(-8, form.DoorOffsetDm,
                "The captured regression depends on Medrare House's left-shifted entrance.");

            int scale = settings.VoxelsPerDecimetre;
            int foundation = plan.Theme.FoundationHeightDm * scale;
            int windowY = foundation + plan.Theme.WindowBaseDm * scale;
            int windowWidth = 11 * scale;
            int windowHeight = plan.Theme.WindowHeightDm * scale;
            int windowDepth = (plan.Theme.WallThicknessDm + 1) * scale;
            int z0 = 10 * scale;
            int minimumFacadeGap = 3 * scale;

            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            try
            {
                FeatureDefinition medrare = catalogue.Definitions[(int)KentridgeRole.MedrareHouse];
                var frontWindows = new List<HorizontalSpan>();
                HorizontalSpan entranceCanopy = default;
                bool foundEntranceCanopy = false;
                int3 transform = int3.zero;
                var transformStack = new Stack<int3>();

                int pc = medrare.ProgramOffset;
                int end = medrare.ProgramOffset + medrare.ProgramLength;
                while (pc < end)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    int length = ShapeOps.InstructionLength(op);
                    Assert.GreaterOrEqual(length, 2);

                    if (op == ShapeOp.PushTransform)
                    {
                        transformStack.Push(transform);
                        transform += new int3(
                            catalogue.Program[pc + 2],
                            catalogue.Program[pc + 3],
                            catalogue.Program[pc + 4]);
                    }
                    else if (op == ShapeOp.PopTransform)
                    {
                        Assert.IsNotEmpty(transformStack,
                            "Translated Medrare House program popped an empty transform stack.");
                        transform = transformStack.Pop();
                    }
                    else if (op == ShapeOp.EmitBox
                             && (PrimitiveMode)catalogue.Program[pc + 11] == PrimitiveMode.Fill)
                    {
                        int x = catalogue.Program[pc + 2] + transform.x;
                        int y = catalogue.Program[pc + 3] + transform.y;
                        int z = catalogue.Program[pc + 4] + transform.z;
                        int width = catalogue.Program[pc + 5];
                        int height = catalogue.Program[pc + 6];
                        int depth = catalogue.Program[pc + 7];
                        byte material = (byte)catalogue.Program[pc + 8];

                        if ((material == 4 || material == 15)
                            && y == windowY
                            && z == z0
                            && width == windowWidth
                            && height == windowHeight
                            && depth == windowDepth)
                        {
                            frontWindows.Add(new HorizontalSpan(x, x + width));
                        }
                        else if (y == foundation + 25 * scale
                                 && z == z0 - 5 * scale
                                 && width == 32 * scale
                                 && height == 3 * scale
                                 && depth == 8 * scale)
                        {
                            entranceCanopy = new HorizontalSpan(x, x + width);
                            foundEntranceCanopy = true;
                        }
                    }

                    pc += length;
                    if (op == ShapeOp.End) break;
                }

                Assert.IsEmpty(transformStack,
                    "Generated Medrare House program must balance its local transform stack.");
                Assert.IsTrue(foundEntranceCanopy,
                    "Medrare House should exercise the default generated-house entrance canopy.");
                Assert.AreEqual(2, frontWindows.Count,
                    "An asymmetric two-bay frontage should retain both first-storey windows; " +
                    "entrance collision handling should reflow a bay rather than silently delete it.");

                for (int i = 0; i < frontWindows.Count; i++)
                {
                    int gap = frontWindows[i].GapTo(entranceCanopy);
                    Assert.GreaterOrEqual(gap, minimumFacadeGap,
                        "First-storey glazing must keep at least 3 dm of visible wall from the " +
                        "complete entrance treatment so the door and window do not read as merged.");
                }
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private readonly struct HorizontalSpan
        {
            public readonly int Min;
            public readonly int Max;

            public HorizontalSpan(int min, int max)
            {
                Min = min;
                Max = max;
            }

            public int GapTo(HorizontalSpan other)
            {
                if (Max <= other.Min) return other.Min - Max;
                if (other.Max <= Min) return Min - other.Max;
                return 0;
            }
        }

        private static BuildingPlot FindRole(SettlementPlan plan, KentridgeRole role)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == (int)role)
                    return plan.Plots[i];

            Assert.Fail("Kentridge settlement must contain its stable " + role + " role.");
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