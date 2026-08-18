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
    public sealed class CathedralConfigTests
    {
        [Test]
        public void SimpleAndGothicPresetsAreValidAndComposeChurchSemantics()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig simple = CathedralPresets.Simple(in palette);
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(simple.IsWellFormed);
                Assert.IsTrue(gothic.IsWellFormed);
                Assert.IsTrue(simple.Church.IsWellFormed);
                Assert.IsTrue(gothic.Church.IsWellFormed);
                Assert.That(simple.TranseptWidth, Is.GreaterThan(simple.NaveAssemblyWidth));
                Assert.That(gothic.TranseptWidth, Is.GreaterThan(gothic.NaveAssemblyWidth));
                Assert.AreEqual(0, simple.ExtraAisleCountPerSide);
                Assert.AreEqual(1, gothic.ExtraAisleCountPerSide);
                Assert.IsFalse(simple.SideChapelsEnabled);
                Assert.IsTrue(gothic.SideChapelsEnabled);
                Assert.IsTrue(simple.WestFrontTowersEnabled);
                Assert.IsTrue(gothic.WestTowerSpiresEnabled);
                Assert.IsFalse(simple.CrossingTowerEnabled);
                Assert.IsTrue(gothic.CrossingTowerEnabled);
                Assert.IsTrue(gothic.CrossingSpireEnabled);
                Assert.IsTrue(simple.RoseWindowEnabled);
                Assert.IsTrue(gothic.CryptEnabled);
                Assert.AreEqual(simple.OverallWidth, simple.Footprint.Primary.Size.x);
                Assert.AreEqual(gothic.OverallWidth, gothic.Footprint.Primary.Size.x);
                Assert.AreEqual(gothic.Church.SanctuaryLength, 70);
                Assert.AreEqual(gothic.Church.ApseRadius, 38);
            });
        }

        [TestCase(Facing.South, Facing.North)]
        [TestCase(Facing.East, Facing.West)]
        [TestCase(Facing.North, Facing.South)]
        [TestCase(Facing.West, Facing.East)]
        public void UndergroundAnchorsRotateWithCathedral(
            Facing entryFacing,
            Facing expectedCaveFacing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);
            gothic.Church.EntryFacing = entryFacing;
            var origin = new int3(500, 80, -700);

            int2 expectedCryptXZ = StructureCardinalTransform.Point(
                new int2(
                    gothic.CryptAnchor.LocalPosition.x,
                    gothic.CryptAnchor.LocalPosition.z),
                entryFacing);
            int2 expectedCaveXZ = StructureCardinalTransform.Point(
                new int2(
                    gothic.CaveAnchor.LocalPosition.x,
                    gothic.CaveAnchor.LocalPosition.z),
                entryFacing);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(gothic.IsWellFormed);
                Assert.AreEqual(
                    new int3(
                        origin.x + expectedCryptXZ.x,
                        origin.y + gothic.CryptAnchor.LocalPosition.y,
                        origin.z + expectedCryptXZ.y),
                    gothic.ResolveCryptAnchor(origin));
                Assert.AreEqual(
                    new int3(
                        origin.x + expectedCaveXZ.x,
                        origin.y + gothic.CaveAnchor.LocalPosition.y,
                        origin.z + expectedCaveXZ.y),
                    gothic.ResolveCaveAnchor(origin));
                Assert.AreEqual(expectedCaveFacing, gothic.ResolveCaveFacing());
            });
        }

        [Test]
        public void GothicCryptCarveUsesRotatedLocalFootprint()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);
            gothic.Church.EntryFacing = Facing.East;
            var origin = new int3(120, 60, 240);
            var session = new RecordingSession();

            CathedralAuthoring.Author(session, origin, in gothic);

            int2 centreLocal = new int2(
                gothic.CryptAnchor.LocalPosition.x,
                gothic.CryptAnchor.LocalPosition.z);
            var cryptLocal = new StructureFootprintRect(
                centreLocal - new int2(gothic.CryptWidth / 2, gothic.CryptDepth / 2),
                new int2(gothic.CryptWidth, gothic.CryptDepth));
            StructureFootprintRect rotated = StructureCardinalTransform.Rect(
                in cryptLocal, gothic.Church.EntryFacing);
            int expectedBottomY = origin.y - gothic.CryptTopOffset - gothic.CryptHeight;

            Assert.IsTrue(session.ContainsEmptyBox(
                new int3(
                    origin.x + rotated.Min.x,
                    expectedBottomY,
                    origin.z + rotated.Min.y),
                new int3(rotated.Size.x, gothic.CryptHeight, rotated.Size.y)),
                "Crypt carve did not use the cardinally rotated local footprint.");
        }

        [TestCase(Facing.South)]
        [TestCase(Facing.East)]
        public void GothicMainEntranceReachesApseAndSideChapel(Facing facing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);
            gothic.Church.EntryFacing = facing;
            Assert.IsTrue(gothic.IsWellFormed);

            const int navY = 91;
            var origin = new int3(240, navY - 1, -360);
            var session = new SliceSession(navY);
            CathedralAuthoring.Author(session, origin, in gothic);

            ChurchConfig church = gothic.Church;
            int frontZ = church.Footprint.Primary.Min.y;
            int2 startLocal = new int2(0, frontZ + church.WallThickness + 1);
            int apseCentreZ = frontZ + church.NaveLength + church.SanctuaryLength;
            int2 apseLocal = new int2(0, apseCentreZ + church.ApseRadius / 2);

            int sanctuaryCentreZ = frontZ + church.NaveLength + church.SanctuaryLength / 2;
            int chapelGroupLength = gothic.SideChapelWidth +
                (gothic.SideChapelCountPerSide - 1) * gothic.SideChapelSpacing;
            int firstChapelCentreZ = sanctuaryCentreZ - chapelGroupLength / 2 +
                gothic.SideChapelWidth / 2;
            int2 chapelLocal = new int2(
                -church.SanctuaryWidth / 2 - gothic.SideChapelDepth / 2,
                firstChapelCentreZ);

            StructureFootprintRect worldFootprint = StructureCardinalTransform.Rect(
                in gothic.Footprint.Primary, facing);
            int2 min = new int2(
                origin.x + worldFootprint.Min.x,
                origin.z + worldFootprint.Min.y);
            int2 start = WorldXZ(origin, StructureCardinalTransform.Point(startLocal, facing));
            int2 apse = WorldXZ(origin, StructureCardinalTransform.Point(apseLocal, facing));
            int2 chapel = WorldXZ(origin, StructureCardinalTransform.Point(chapelLocal, facing));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(IsReachable(session.Solid, start, apse, min, worldFootprint.Size),
                    $"Gothic cathedral apse was unreachable for {facing}.");
                Assert.IsTrue(IsReachable(session.Solid, start, chapel, min, worldFootprint.Size),
                    $"Gothic cathedral side chapel was unreachable for {facing}.");
            });
        }

        [Test]
        public void GothicAuthoringIsDeterministicAndBoundedInOperationCount()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);
            var a = new RecordingSession();
            var b = new RecordingSession();
            var origin = new int3(0, 40, 0);

            CathedralAuthoring.Author(a, origin, in gothic);
            CathedralAuthoring.Author(b, origin, in gothic);

            Assert.Multiple(() =>
            {
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.That(a.Operations.Count, Is.GreaterThan(50));
                Assert.That(a.Operations.Count, Is.LessThan(2000),
                    "Cathedral preset exceeded its intended bounded authoring complexity.");
                Assert.That(a.HollowBoxes, Is.GreaterThan(8));
                Assert.That(a.Cones, Is.GreaterThanOrEqualTo(3));
            });
        }

        [Test]
        public void ValidationRejectsImpossibleCathedralCompositions()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CathedralConfig gothic = CathedralPresets.Gothic(in palette);

            CathedralConfig narrowTransept = gothic;
            narrowTransept.TranseptWidth = narrowTransept.NaveAssemblyWidth;

            CathedralConfig badChapels = gothic;
            badChapels.SideChapelSpacing = badChapels.SideChapelWidth - 1;

            CathedralConfig highTowerWindow = gothic;
            highTowerWindow.CrossingTower.Opening.BottomOffset = highTowerWindow.CrossingTower.Height;

            CathedralConfig wrongCryptKind = gothic;
            wrongCryptKind.CryptAnchor.Kind = StructureAttachmentKind.Basement;

            Assert.Multiple(() =>
            {
                Assert.IsFalse(narrowTransept.IsWellFormed);
                Assert.IsFalse(badChapels.IsWellFormed);
                Assert.IsFalse(highTowerWindow.IsWellFormed);
                Assert.IsFalse(wrongCryptKind.IsWellFormed);
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

        private readonly struct BoxWrite
        {
            public readonly int3 Min;
            public readonly int3 Size;
            public readonly byte Material;
            public BoxWrite(int3 min, int3 size, byte material)
            {
                Min = min;
                Size = size;
                Material = material;
            }
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<string> Operations = new List<string>();
            public readonly List<BoxWrite> Boxes = new List<BoxWrite>();
            public int HollowBoxes { get; private set; }
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
            public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material)
            {
                Boxes.Add(new BoxWrite(min, size, material));
                Operations.Add($"box:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{material}");
            }
            public bool ContainsEmptyBox(int3 min, int3 size)
            {
                foreach (BoxWrite box in Boxes)
                    if (box.Material == GameMaterialIds.Empty && box.Min.Equals(min) && box.Size.Equals(size))
                        return true;
                return false;
            }
            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling)
            {
                HollowBoxes++;
                Operations.Add($"hollow:{min.x}:{min.y}:{min.z}:{size.x}:{size.y}:{size.z}:{thickness}:{material}:{floor}:{ceiling}");
            }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) =>
                Operations.Add($"cyl:{cx}:{baseY}:{cz}:{radius}:{height}:{innerRadius}:{material}");
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
                if (_sliceY >= minY && _sliceY < maxYExclusive) Apply(new int2(x, z), material);
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
                    if (wall || floorSlice || ceilingSlice) Apply(new int2(x, z), material);
                }
            }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0)
            {
                if (_sliceY < baseY || _sliceY >= baseY + height) return;
                int outer2 = radius * radius;
                int inner2 = innerRadius * innerRadius;
                for (int z = cz - radius; z <= cz + radius; z++)
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int dx = x - cx;
                    int dz = z - cz;
                    int d2 = dx * dx + dz * dz;
                    if (d2 <= outer2 && (innerRadius <= 0 || d2 >= inner2))
                        Apply(new int2(x, z), material);
                }
            }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
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
    }
}
