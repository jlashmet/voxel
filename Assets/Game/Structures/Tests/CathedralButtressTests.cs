using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CathedralButtressTests
    {
        [Test]
        public void SimpleUsesOrdinaryButtressesAndGothicUsesFlyingButtresses()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralWorldbuildingConfig simple = CathedralWorldbuildingPresets.Simple(in palette);
            CathedralWorldbuildingConfig gothic = CathedralWorldbuildingPresets.Gothic(in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(simple.IsWellFormed);
                Assert.IsTrue(gothic.IsWellFormed);
                Assert.IsTrue(simple.ButtressesEnabled);
                Assert.IsTrue(gothic.ButtressesEnabled);
                Assert.IsFalse(simple.NaveButtresses.FlyingEnabled);
                Assert.IsTrue(gothic.NaveButtresses.FlyingEnabled);
                Assert.That(gothic.NaveButtresses.Height, Is.GreaterThan(simple.NaveButtresses.Height));
                Assert.That(gothic.NaveButtresses.FlyingSpan, Is.GreaterThan(0));
            });
        }

        [Test]
        public void GothicWorldbuildingAddsSharedFlyingButtressWritesDeterministically()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralWorldbuildingConfig config = CathedralWorldbuildingPresets.Gothic(in palette);
            var a = new RecordingSession();
            var b = new RecordingSession();
            var origin = new int3(80, 40, -120);

            CathedralWorldbuildingAuthoring.Author(a, origin, in config);
            CathedralWorldbuildingAuthoring.Author(b, origin, in config);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.That(a.Boxes, Is.GreaterThan(40));
                Assert.That(a.ThinButtressBridgeBoxes, Is.GreaterThan(0),
                    "Gothic preset did not emit stepped flying-buttress bridge boxes.");
            });
        }

        [TestCase(Facing.South)]
        [TestCase(Facing.East)]
        [TestCase(Facing.North)]
        [TestCase(Facing.West)]
        public void ButtressCompositionRemainsValidForEveryCardinalOrientation(Facing facing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralWorldbuildingConfig config = CathedralWorldbuildingPresets.Gothic(in palette);
            config.Cathedral.Church.EntryFacing = facing;
            var session = new RecordingSession();

            Assert.DoesNotThrow(() =>
                CathedralWorldbuildingAuthoring.Author(
                    session,
                    new int3(240, 32, 240),
                    in config));
            Assert.That(session.ThinButtressBridgeBoxes, Is.GreaterThan(0));
        }

        [Test]
        public void InvalidFlyingButtressConfigIsRejected()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralWorldbuildingConfig config = CathedralWorldbuildingPresets.Gothic(in palette);
            config.NaveButtresses.FlyingThickness = 0;

            Assert.IsFalse(config.IsWellFormed);
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<string> Operations = new List<string>();
            public int Boxes { get; private set; }
            public int ThinButtressBridgeBoxes { get; private set; }
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
            public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material)
            {
                Boxes++;
                if (size.x == 1 || size.z == 1)
                    ThinButtressBridgeBoxes++;
                Operations.Add($"box:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{material}");
            }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) =>
                Operations.Add($"hollow:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{thickness}:{material}");
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) =>
                Operations.Add($"cyl:{cx}:{baseY}:{cz}:{radius}:{height}:{material}");
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Operations.Add($"cone:{cx}:{baseY}:{cz}:{radius}:{height}:{material}");
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) =>
                Operations.Add($"gable:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{alongX}:{material}");
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
