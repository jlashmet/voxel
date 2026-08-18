using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class ShedConfigTests
    {
        [Test]
        public void PresetsAreValidAndMateriallyDifferent()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ShedConfig storage = ShedPresets.Storage(in palette);
            ShedConfig workshop = ShedPresets.Workshop(in palette);
            ShedConfig leanTo = ShedPresets.LeanTo(in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(storage.IsWellFormed);
                Assert.IsTrue(workshop.IsWellFormed);
                Assert.IsTrue(leanTo.IsWellFormed);

                Assert.AreEqual(RoofStyle.Gable, storage.Roof.Style);
                Assert.AreEqual(RoofStyle.Flat, workshop.Roof.Style);
                Assert.AreEqual(RoofStyle.Shed, leanTo.Roof.Style);

                Assert.IsFalse(storage.WindowsEnabled);
                Assert.IsTrue(workshop.WindowsEnabled);
                Assert.IsTrue(leanTo.WindowsEnabled);
                Assert.AreEqual(48, storage.Width);
                Assert.AreEqual(64, workshop.Width);
                Assert.AreEqual(52, leanTo.Width);
                Assert.AreNotEqual(storage.Depth, workshop.Depth);
                Assert.AreNotEqual(workshop.Height, leanTo.Height);
            });
        }

        [Test]
        public void DetailedControlsCanBeOverriddenWithoutChangingBuilderType()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ShedConfig config = ShedPresets.Workshop(in palette);

            config.Footprint.Primary = new StructureFootprintRect(
                new int2(-36, -28),
                new int2(72, 56));
            config.Walls.Length = 72;
            config.Walls.Height = 44;
            config.Walls.Thickness = 6;
            config.Depth = 56;
            config.DoorCount = 2;
            config.Door.Width = 14;
            config.DoorSpacing = 20;
            config.DoorGroupOffset = 0;
            config.WindowCount = 3;
            config.Window.Width = 10;
            config.WindowSpacing = 14;
            config.WindowGroupOffset = 0;
            config.Roof.Style = RoofStyle.Shed;
            config.Roof.RidgeAxis = RoofAxis.Z;
            config.Roof.PitchRise = 12;
            config.Roof.PitchRun = 24;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);
                Assert.AreEqual(72, config.Width);
                Assert.AreEqual(56, config.Depth);
                Assert.AreEqual(44, config.Height);
                Assert.AreEqual(6, config.WallThickness);
                Assert.AreEqual(2, config.DoorCount);
                Assert.AreEqual(3, config.WindowCount);
                Assert.AreEqual(RoofStyle.Shed, config.Roof.Style);
                Assert.AreEqual(RoofAxis.Z, config.Roof.RidgeAxis);
            });
        }

        [Test]
        public void ValidationRejectsUnsupportedRoofAndOpeningsThatDoNotFitFacade()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ShedConfig valid = ShedPresets.Storage(in palette);

            ShedConfig hip = valid;
            hip.Roof.Style = RoofStyle.Hip;

            ShedConfig oversizedDoor = valid;
            oversizedDoor.Door.Width = oversizedDoor.Width;

            ShedConfig repeatedDoorOverlap = valid;
            repeatedDoorOverlap.DoorCount = 2;
            repeatedDoorOverlap.DoorSpacing = valid.Door.Width - 1;

            ShedConfig tooTallWindow = ShedPresets.Workshop(in palette);
            tooTallWindow.Window.Height = tooTallWindow.Height;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(valid.IsWellFormed);
                Assert.IsFalse(hip.IsWellFormed);
                Assert.IsFalse(oversizedDoor.IsWellFormed);
                Assert.IsFalse(repeatedDoorOverlap.IsWellFormed);
                Assert.IsFalse(tooTallWindow.IsWellFormed);
            });
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PresetGeometryIsDeterministic(int preset)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ShedConfig config = preset == 0
                ? ShedPresets.Storage(in palette)
                : preset == 1
                    ? ShedPresets.Workshop(in palette)
                    : ShedPresets.LeanTo(in palette);
            var a = new RecordingSession();
            var b = new RecordingSession();
            var origin = new int3(120, 36, -240);

            ShedAuthoring.Author(a, origin, in config);
            ShedAuthoring.Author(b, origin, in config);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.That(a.Operations.Count, Is.GreaterThan(4));
                Assert.AreEqual(1, a.HollowBoxes);

                if (config.Roof.Style == RoofStyle.Gable)
                    Assert.AreEqual(1, a.Gables);
                else
                    Assert.AreEqual(0, a.Gables);

                if (config.Roof.Style == RoofStyle.Shed)
                    Assert.That(a.Boxes, Is.GreaterThan(config.Depth));
            });
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<string> Operations = new List<string>();
            public int Boxes { get; private set; }
            public int HollowBoxes { get; private set; }
            public int Gables { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Operations.Count;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }

            public void Box(int3 min, int3 size, byte material)
            {
                Boxes++;
                Operations.Add($"box:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{material}");
            }

            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling)
            {
                HollowBoxes++;
                Operations.Add($"hollow:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{thickness}:{material}:{floor}:{ceiling}");
            }

            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }

            public void Gable(int3 min, int3 size, bool alongX, byte material)
            {
                Gables++;
                Operations.Add($"gable:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{alongX}:{material}");
            }

            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
