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
    public sealed class CastleMoatConfigTests
    {
        [Test]
        public void CompatibilityPresetIsDisabledButReadyToEnable()
        {
            CastlePlan plan = ValidSmallPlan();
            CastleMoatConfig moat = CastleMoatPresets.Compatibility(in plan);

            Assert.Multiple(() =>
            {
                Assert.IsFalse(moat.Enabled);
                Assert.IsTrue(moat.IsWellFormed);
                Assert.AreEqual(
                    new int2(
                        plan.BaileyHalfX + plan.WallThickness + 12,
                        plan.BaileyHalfZ + plan.WallThickness + 12),
                    moat.InnerHalfExtents);
                Assert.AreEqual(34, moat.Width);
                Assert.AreEqual(28, moat.Depth);
                Assert.AreEqual(12, moat.WaterDepth);
                Assert.AreEqual(StructureMaterialRole.Underground, moat.BedMaterialRole);
            });
        }

        [Test]
        public void EnabledMoatWritesBedOnlyInsideConfiguredBoundedRing()
        {
            CastlePlan plan = ValidSmallPlan();
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            components.Moat.Enabled = true;
            components.Moat.InnerHalfExtents = new int2(8, 6);
            components.Moat.Width = 4;
            components.Moat.Depth = 5;
            components.Moat.WaterDepth = 2;
            components.Moat.BedMaterialRole = StructureMaterialRole.Accent;
            Assert.IsTrue(components.IsWellFormed);

            int top = plan.Centre.y + plan.PlateauHeight;
            var authoring = new RecordingAuthoringSession(top);
            var state = new CastleSiteAuthoringState();

            for (int step = 0; step < 1024 && !state.IsComplete; step++)
                CastleSiteAuthoring.Step(
                    authoring,
                    in plan,
                    in components,
                    19u,
                    ref state);

            Assert.IsTrue(state.IsComplete);
            Assert.That(authoring.GoldColumns.Count, Is.GreaterThan(0));

            int2 inner = components.Moat.InnerHalfExtents;
            int2 outer = components.Moat.OuterHalfExtents;
            int expectedBottom = top - components.Moat.Depth;
            foreach (ColumnWrite write in authoring.GoldColumns)
            {
                int localX = write.X - plan.Centre.x;
                int localZ = write.Z - plan.Centre.z;
                int absX = math.abs(localX);
                int absZ = math.abs(localZ);

                Assert.Multiple(() =>
                {
                    Assert.That(absX, Is.LessThanOrEqualTo(outer.x));
                    Assert.That(absZ, Is.LessThanOrEqualTo(outer.y));
                    Assert.IsFalse(absX <= inner.x && absZ <= inner.y);
                    Assert.AreEqual(expectedBottom, write.MinY);
                    Assert.AreEqual(expectedBottom + 1, write.MaxYExclusive);
                });
            }
        }

        [Test]
        public void ValidationRejectsUnboundedOrImpossibleMoatDimensions()
        {
            CastlePlan plan = ValidSmallPlan();
            CastleMoatConfig valid = CastleMoatPresets.Compatibility(in plan);
            valid.Enabled = true;

            CastleMoatConfig zeroWidth = valid;
            zeroWidth.Width = 0;

            CastleMoatConfig waterTooDeep = valid;
            waterTooDeep.WaterDepth = waterTooDeep.Depth + 1;

            CastleMoatConfig overflow = valid;
            overflow.InnerHalfExtents = new int2(int.MaxValue, 10);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(valid.IsWellFormed);
                Assert.IsFalse(zeroWidth.IsWellFormed);
                Assert.IsFalse(waterTooDeep.IsWellFormed);
                Assert.IsFalse(overflow.IsWellFormed);
            });
        }

        private static CastlePlan ValidSmallPlan() => new CastlePlan
        {
            Centre = new int3(0, 40, 0),
            PlateauRadius = 2,
            PlateauHeight = 4,
            CliffDrop = 1,
            BaileyHalfX = 70,
            BaileyHalfZ = 70,
            WallHeight = 48,
            WallThickness = 4,
            TowerRadius = 8,
            TowerHeight = 24,
            GateTowerRadius = 8,
            GateTowerHeight = 24,
            KeepHalfX = 50,
            KeepHalfZ = 50,
            KeepHeight = 92,
            FloorHeight = 46,
            Floors = 2,
            Seed = 19u,
        };

        private readonly struct ColumnWrite
        {
            public readonly int X;
            public readonly int MinY;
            public readonly int MaxYExclusive;
            public readonly int Z;

            public ColumnWrite(int x, int minY, int maxYExclusive, int z)
            {
                X = x;
                MinY = minY;
                MaxYExclusive = maxYExclusive;
                Z = z;
            }
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            private readonly int _solidTop;

            public RecordingAuthoringSession(int solidTop)
            {
                _solidTop = solidTop;
            }

            public readonly List<ColumnWrite> GoldColumns = new();
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => y <= _solidTop;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(
                int x, int minY, int maxYExclusive, int z, byte material)
            {
                if (material == GameMaterialIds.Gold)
                    GoldColumns.Add(new ColumnWrite(x, minY, maxYExclusive, z));
            }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(
                int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(
                int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(
                int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(
                int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(
                int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(
                int3 min, int width, int height, int depth,
                int depthAxis, byte material) { }
            public void Stairs(
                int3 min, int width, int steps, int rise, int run,
                int axis, byte material) { }
            public void SpiralStair(
                int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(
                int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
