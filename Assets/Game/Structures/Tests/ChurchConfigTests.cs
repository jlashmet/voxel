using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class ChurchConfigTests
    {
        [Test]
        public void ChapelAndParishPresetsAreValidAndMateriallyDifferent()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ChurchConfig chapel = ChurchPresets.Chapel(in palette);
            ChurchConfig parish = ChurchPresets.ParishChurch(in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(chapel.IsWellFormed);
                Assert.IsTrue(parish.IsWellFormed);
                Assert.IsFalse(chapel.AislesEnabled);
                Assert.IsFalse(chapel.ClerestoryEnabled);
                Assert.AreEqual(ChurchBellTowerPlacement.None, chapel.BellTowerPlacement);
                Assert.IsTrue(parish.AislesEnabled);
                Assert.IsTrue(parish.ClerestoryEnabled);
                Assert.AreEqual(ChurchBellTowerPlacement.FrontLeft, parish.BellTowerPlacement);
                Assert.IsTrue(parish.SpireEnabled);
                Assert.AreNotEqual(chapel.NaveWidth, parish.NaveWidth);
                Assert.AreNotEqual(chapel.NaveLength, parish.NaveLength);
                Assert.AreEqual(chapel.OverallWidth, chapel.Footprint.Primary.Size.x);
                Assert.AreEqual(chapel.OverallLength, chapel.Footprint.Primary.Size.y);
                Assert.AreEqual(parish.OverallWidth, parish.Footprint.Primary.Size.x);
                Assert.AreEqual(parish.OverallLength, parish.Footprint.Primary.Size.y);
            });
        }

        [TestCase(Facing.South)]
        [TestCase(Facing.East)]
        [TestCase(Facing.North)]
        [TestCase(Facing.West)]
        public void ChapelMainEntranceReachesSanctuaryAndApseForEveryCardinalFacing(Facing facing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ChurchConfig chapel = ChurchPresets.Chapel(in palette);
            chapel.EntryFacing = facing;
            Assert.IsTrue(chapel.IsWellFormed);

            const int navY = 41;
            var origin = new int3(160, navY - 1, -240);
            var session = new SliceSession(navY);
            ChurchAuthoring.Author(session, origin, in chapel);

            int frontZ = chapel.Footprint.Primary.Min.y;
            int wall = chapel.WallThickness;
            int apseCentreZ = frontZ + chapel.NaveLength + chapel.SanctuaryLength;
            int2 startLocal = new int2(0, frontZ + wall + 1);
            int2 targetLocal = new int2(0, apseCentreZ + chapel.ApseRadius / 2);
            int2 start = WorldXZ(origin, StructureCardinalTransform.Point(startLocal, facing));
            int2 target = WorldXZ(origin, StructureCardinalTransform.Point(targetLocal, facing));

            // The portal must actually clear every layer of the south wall at walking height.
            for (int z = frontZ - 1; z <= frontZ + wall; z++)
            {
                int2 local = StructureCardinalTransform.Point(new int2(0, z), facing);
                Assert.IsFalse(session.Solid.Contains(WorldXZ(origin, local)),
                    $"Main portal remained blocked at local z={z}, facing={facing}.");
            }

            StructureFootprintRect worldFootprint = StructureCardinalTransform.Rect(
                in chapel.Footprint.Primary, facing);
            Assert.IsTrue(IsReachable(
                session.Solid,
                start,
                target,
                new int2(origin.x + worldFootprint.Min.x, origin.z + worldFootprint.Min.y),
                worldFootprint.Size),
                $"Church interior was not connected from entrance to apse for facing {facing}.");
        }

        [Test]
        public void ParishGeometryIsDeterministic()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ChurchConfig parish = ChurchPresets.ParishChurch(in palette);
            var a = new RecordingSession();
            var b = new RecordingSession();
            var origin = new int3(100, 32, 200);

            ChurchAuthoring.Author(a, origin, in parish);
            ChurchAuthoring.Author(b, origin, in parish);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.That(a.Operations.Count, Is.GreaterThan(20));
                Assert.That(a.HollowBoxes, Is.GreaterThanOrEqualTo(4));
                Assert.That(a.Cylinders, Is.GreaterThan(0));
                Assert.That(a.Cones, Is.GreaterThanOrEqualTo(2));
            });
        }

        [Test]
        public void ValidationRejectsImpossibleChurchComposition()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            ChurchConfig parish = ChurchPresets.ParishChurch(in palette);

            ChurchConfig nonCardinal = parish;
            nonCardinal.EntryFacing = Facing.Up;

            ChurchConfig badApse = parish;
            badApse.ApseRadius = badApse.WallThickness;

            ChurchConfig lowClerestory = parish;
            lowClerestory.ClerestoryWindow.BottomOffset = lowClerestory.AisleHeight;

            ChurchConfig badTowerPortal = parish;
            badTowerPortal.MainPortal.Width = badTowerPortal.BellTower.Width;

            Assert.Multiple(() =>
            {
                Assert.IsFalse(nonCardinal.IsWellFormed);
                Assert.IsFalse(badApse.IsWellFormed);
                Assert.IsFalse(lowClerestory.IsWellFormed);
                Assert.IsFalse(badTowerPortal.IsWellFormed);
            });
        }

        private static int2 WorldXZ(int3 origin, int2 local) =>
            new int2(origin.x + local.x, origin.z + local.y);

        private static bool IsReachable(
            HashSet<int2> solid,
            int2 start,
            int2 target,
            int2 min,
            int2 size)
        {
            int2 max = min + size;
            if (solid.Contains(start) || solid.Contains(target)) return false;

            var visited = new HashSet<int2> { start };
            var queue = new Queue<int2>();
            queue.Enqueue(start);
            int2[] steps =
            {
                new int2(1, 0), new int2(-1, 0),
                new int2(0, 1), new int2(0, -1),
            };

            while (queue.Count > 0)
            {
                int2 current = queue.Dequeue();
                if (current.Equals(target)) return true;
                foreach (int2 step in steps)
                {
                    int2 next = current + step;
                    if (next.x < min.x || next.x >= max.x ||
                        next.y < min.y || next.y >= max.y ||
                        solid.Contains(next) || !visited.Add(next))
                        continue;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private sealed class SliceSession : IStructureAuthoringSession
        {
            private readonly int _sliceY;
            public readonly HashSet<int2> Solid = new HashSet<int2>();

            public SliceSession(int sliceY) => _sliceY = sliceY;

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Solid.Count;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => y == _sliceY && Solid.Contains(new int2(x, z));

            public void Set(int x, int y, int z, byte material)
            {
                if (y == _sliceY) Apply(new int2(x, z), material);
            }

            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Set(x, y, z, material);
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);

            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
            {
                if (_sliceY >= minY && _sliceY < maxYExclusive)
                    Apply(new int2(x, z), material);
            }

            public void Box(int3 min, int3 size, byte material)
            {
                if (_sliceY < min.y || _sliceY >= min.y + size.y) return;
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                    Apply(new int2(x, z), material);
            }

            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling)
            {
                if (_sliceY < min.y || _sliceY >= min.y + size.y) return;
                bool floorSlice = floor && _sliceY < min.y + thickness;
                bool ceilingSlice = ceiling && _sliceY >= min.y + size.y - thickness;
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                {
                    bool wall = x < min.x + thickness || x >= min.x + size.x - thickness ||
                                z < min.z + thickness || z >= min.z + size.z - thickness;
                    if (wall || floorSlice || ceilingSlice)
                        Apply(new int2(x, z), material);
                }
            }

            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0)
            {
                if (_sliceY < baseY || _sliceY >= baseY + height) return;
                int outerSquared = radius * radius;
                int innerSquared = innerRadius * innerRadius;
                for (int z = cz - radius; z <= cz + radius; z++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dz = z - cz;
                    int d2 = dx * dx + dz * dz;
                    if (d2 <= outerSquared && (innerRadius <= 0 || d2 >= innerSquared))
                        Apply(new int2(x, z), material);
                }
            }

            public void Disc(int cx, int y, int cz, int radius, byte material)
            {
                if (y != _sliceY) return;
                int r2 = radius * radius;
                for (int z = cz - radius; z <= cz + radius; z++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dz = z - cz;
                    if (dx * dx + dz * dz <= r2) Apply(new int2(x, z), material);
                }
            }

            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) => Box(min, size, GameMaterialIds.Empty);
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }

            private void Apply(int2 cell, byte material)
            {
                if (material == GameMaterialIds.Empty) Solid.Remove(cell);
                else Solid.Add(cell);
            }
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<string> Operations = new List<string>();
            public int HollowBoxes { get; private set; }
            public int Cylinders { get; private set; }
            public int Cones { get; private set; }
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

            public void Box(int3 min, int3 size, byte material) =>
                Operations.Add($"box:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{material}");

            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling)
            {
                HollowBoxes++;
                Operations.Add($"hollow:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{thickness}:{material}:{floor}:{ceiling}");
            }

            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0)
            {
                Cylinders++;
                Operations.Add($"cyl:{cx}:{baseY}:{cz}:{radius}:{height}:{innerRadius}:{material}");
            }

            public void Disc(int cx, int y, int cz, int radius, byte material) =>
                Operations.Add($"disc:{cx}:{y}:{cz}:{radius}:{material}");

            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material)
            {
                Cones++;
                Operations.Add($"cone:{cx}:{baseY}:{cz}:{radius}:{height}:{material}");
            }

            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) =>
                Operations.Add($"gable:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{alongX}:{material}");
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
