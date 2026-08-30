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
    public sealed class CastleLowerRiverWaterRepairTests
    {
        private const uint ShowcaseSeed = 0x5EED1234u;

        [Test]
        public void ShowcaseMarkedReceivingBankBecomesWaterAndStopsBeforeOuterShore()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(256, 220, 376), ShowcaseSeed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int riverY = top - CastleLayout.LowerRiverDepth;
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int channelZ = CastleLayout.LowerRiverZAt(in plan, streamX);

            var authoring = new RecordingAuthoringSession();
            int[] markedBankOffsets = { 39, 50, 55, 67, 79 };
            for (int i = 0; i < markedBankOffsets.Length; i++)
            {
                int z = channelZ + markedBankOffsets[i];
                for (int y = riverY + 1; y <= riverY + 18; y++)
                    authoring.Seed(streamX, y, z, GameMaterialIds.Dirt);
                authoring.Seed(streamX, riverY + 19, z, GameMaterialIds.Grass);
            }

            int outerShoreZ = channelZ + 85;
            authoring.Seed(streamX, riverY + 8, outerShoreZ, GameMaterialIds.Grass);

            int cascadeZ = channelZ + 67;
            authoring.Seed(streamX, riverY + 12, cascadeZ, GameMaterialIds.Cascade);

            CastleLowerRiverWaterRepair.Repair(authoring, in plan);

            Assert.Multiple(() =>
            {
                for (int i = 0; i < markedBankOffsets.Length; i++)
                {
                    int z = channelZ + markedBankOffsets[i];
                    Assert.That(
                        authoring.Get(streamX, riverY, z),
                        Is.EqualTo(GameMaterialIds.Water),
                        $"marked receiving-bank offset +{markedBankOffsets[i]} must expose water at the river level");
                    Assert.That(
                        authoring.Get(streamX, riverY + 19, z),
                        Is.EqualTo(GameMaterialIds.Empty),
                        $"marked receiving-bank offset +{markedBankOffsets[i]} must not retain the captured grass cap");
                }

                Assert.That(
                    authoring.Get(streamX, riverY + 8, outerShoreZ),
                    Is.EqualTo(GameMaterialIds.Grass),
                    "the dry outer shore beyond the 80-voxel receiving-water limit must remain untouched");
                Assert.That(
                    authoring.Get(streamX, riverY + 12, cascadeZ),
                    Is.EqualTo(GameMaterialIds.Cascade),
                    "the compatibility repair must preserve authored waterfall cascade voxels");
                Assert.That(authoring.TotalVoxelsWritten, Is.LessThan(1_500_000));
            });
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            private readonly Dictionary<(int X, int Y, int Z), byte> _materials = new();
            private long _writes;

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _writes;

            public void Seed(int x, int y, int z, byte material) =>
                _materials[(x, y, z)] = material;

            public byte Get(int x, int y, int z) =>
                _materials.TryGetValue((x, y, z), out byte material)
                    ? material
                    : GameMaterialIds.Empty;

            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public bool IsSolid(int x, int y, int z) => Get(x, y, z) != GameMaterialIds.Empty;

            public void Set(int x, int y, int z, byte material)
            {
                var key = (x, y, z);
                byte previous = Get(x, y, z);
                if (previous == material) return;
                if (material == GameMaterialIds.Empty)
                    _materials.Remove(key);
                else
                    _materials[key] = material;
                _writes++;
            }

            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => Set(x, y, z, material);

            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);

            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
            {
                for (int y = minY; y < maxYExclusive; y++)
                    Set(x, y, z, material);
            }

            public void Box(int3 min, int3 size, byte material)
            {
                int3 max = min + size;
                for (int z = min.z; z < max.z; z++)
                for (int y = min.y; y < max.y; y++)
                for (int x = min.x; x < max.x; x++)
                    Set(x, y, z, material);
            }

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
            public void Carve(int3 min, int3 size) => Box(min, size, GameMaterialIds.Empty);
            public void Weather(
                int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
