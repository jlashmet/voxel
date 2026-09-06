using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class ForgeHearthFacingAuthoringTests
    {
        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, 1)]
        [TestCase(0, -1)]
        public void ProductionAuthoringKeepsSemanticFrontOpenAndBackedByMasonry(int facingX, int facingZ)
        {
            var bounds = new DecorationBounds
            {
                Min = new int3(0, 0, 0),
                MaxExclusive = new int3(18, 12, 14),
            };
            int3 facing = new int3(facingX, 0, facingZ);
            DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(DecorationContentKind.ForgeHearth);
            var placement = new DecorationPlacement
            {
                Id = new GeneratedPropId(0x4845415254480001ul),
                SceneId = 0x48454152u,
                SlotId = 1u,
                Family = recipe.ProxyFamily,
                Backend = DecorationRenderBackend.VoxelStamp,
                Interaction = recipe.Interaction,
                Bounds = bounds,
                Facing = facing,
                Variant = DecorationContentVariants.Encode(DecorationContentKind.ForgeHearth, 17u),
            };
            DecorationContext context = Context();
            var authoring = new RecordingAuthoringSession();

            Assert.That(DecorationContentAuthoringEmitter.TryAuthorGeometry(
                authoring, new[] { placement }, in context), Is.True);

            int y = bounds.Min.y + math.max(3, bounds.Size.y / 6) + 3;
            int cx = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int cz = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            int3 front = facingX > 0 ? new int3(bounds.MaxExclusive.x - 1, y, cz) :
                facingX < 0 ? new int3(bounds.Min.x, y, cz) :
                facingZ > 0 ? new int3(cx, y, bounds.MaxExclusive.z - 1) :
                new int3(cx, y, bounds.Min.z);
            int3 rear = facingX > 0 ? new int3(bounds.Min.x, y, cz) :
                facingX < 0 ? new int3(bounds.MaxExclusive.x - 1, y, cz) :
                facingZ > 0 ? new int3(cx, y, bounds.Min.z) :
                new int3(cx, y, bounds.MaxExclusive.z - 1);

            Assert.Multiple(() =>
            {
                Assert.That(authoring.IsOccupied(front), Is.False,
                    $"Forge Hearth semantic front {facing} must expose the firebox aperture.");
                Assert.That(authoring.IsOccupied(rear), Is.True,
                    $"Forge Hearth side opposite semantic front {facing} must retain rear masonry.");
            });
        }

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 0x48454131u,
            StructureId = 0x48454132u,
            SpaceId = 0x48454133u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 23u),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };

        private readonly struct RecordedBox
        {
            public readonly int3 Min;
            public readonly int3 MaxExclusive;

            public RecordedBox(int3 min, int3 size)
            {
                Min = min;
                MaxExclusive = min + size;
            }

            public bool Contains(int3 point) =>
                point.x >= Min.x && point.x < MaxExclusive.x &&
                point.y >= Min.y && point.y < MaxExclusive.y &&
                point.z >= Min.z && point.z < MaxExclusive.z;
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            private readonly List<RecordedBox> boxes = new List<RecordedBox>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => IsOccupied(new int3(x, y, z));

            public bool IsOccupied(int3 point)
            {
                for (int i = 0; i < boxes.Count; i++)
                    if (boxes[i].Contains(point))
                        return true;
                return false;
            }

            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) => Box(min, size, material);
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
                Box(new int3(x, minY, z), new int3(1, maxYExclusive - minY, 1), material);
            public void Box(int3 min, int3 size, byte material) => boxes.Add(new RecordedBox(min, size));
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
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
