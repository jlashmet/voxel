using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NewHouseReferenceOpeningOrderTests
    {
        [NUnit.Framework.Test]
        public void AuthorHouse_RestoresUpperOpeningsAfterDestructiveRoofReplacement()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = new(
                plaster: 41, timber: 42, roof: 43, stone: 44, glass: 45,
                door: 46, accent: 47, ground: 48, flowers: 49, foliage: 50, ornament: 51);
            int3 origin = new(17, 9, -23);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int upper = origin.y + config.UpperFloorY;
            int eave = origin.y + config.MainEaveY;
            int centre = origin.x + config.Width / 2;
            int roofClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.y == upper + 13 &&
                op.Size.x == config.Width + 44);

            Assert.That(roofClear, Is.GreaterThanOrEqualTo(0),
                "The regression must observe the destructive roof-clear operation.");
            Assert.That(session.Operations.ExistsAfter(roofClear, op =>
                    op.Kind == OperationKind.Box && op.Material == palette.Glass &&
                    op.Position.x <= centre && op.Position.x + op.Size.x > centre &&
                    op.Position.y >= upper + 7 && op.Position.y < upper + 31),
                Is.True,
                "The middle-storey arched glass opening must be re-authored after roof replacement.");
            Assert.That(session.Operations.ExistsAfter(roofClear, op =>
                    op.Kind == OperationKind.Box && op.Material == palette.Glass &&
                    op.Position.x <= centre && op.Position.x + op.Size.x > centre &&
                    op.Position.y >= eave + 10 && op.Position.y < eave + 31),
                Is.True,
                "The upper-gable arched glass opening must be re-authored after roof replacement.");
            Assert.That(session.Operations.ExistsAfter(roofClear, op =>
                    op.Kind == OperationKind.Box && op.Material == palette.Accent &&
                    op.Position.y >= upper + 8 && op.Position.y < upper + 30),
                Is.True,
                "Reference shutters must survive the destructive roof replacement ordering.");
        }

        private enum OperationKind : byte
        {
            Set, SetStyled, Coat, FillBulk, FillColumnBulk, Box, HollowBox, Cylinder, Disc,
            Cone, HangingCone, Gable, Crenellate, CrenellateRing, Arch, Stairs, SpiralStair,
            Carve, Weather,
        }

        private readonly struct RecordedOperation
        {
            public RecordedOperation(OperationKind kind, int3 position, int3 size, byte material)
            { Kind = kind; Position = position; Size = size; Material = material; }
            public OperationKind Kind { get; }
            public int3 Position { get; }
            public int3 Size { get; }
            public byte Material { get; }
        }

        private sealed class OperationList : List<RecordedOperation>
        {
            public bool ExistsAfter(int index, System.Predicate<RecordedOperation> match)
            {
                for (int i = index + 1; i < Count; i++)
                    if (match(this[i])) return true;
                return false;
            }
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly OperationList Operations = new();
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Operations.Count;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) => Add(OperationKind.Set, new int3(x, y, z), new int3(1), material);
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Add(OperationKind.SetStyled, new int3(x, y, z), new int3(1), material);
            public void Coat(int x, int y, int z, byte coating) => Add(OperationKind.Coat, new int3(x, y, z), new int3(1), coating);
            public void FillBulk(int3 min, int3 size, byte material) => Add(OperationKind.FillBulk, min, size, material);
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
                Add(OperationKind.FillColumnBulk, new int3(x, minY, z), new int3(1, maxYExclusive - minY, 1), material);
            public void Box(int3 min, int3 size, byte material) => Add(OperationKind.Box, min, size, material);
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) =>
                Add(OperationKind.HollowBox, min, size, material);
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) =>
                Add(OperationKind.Cylinder, new int3(cx, baseY, cz), new int3(radius, height, innerRadius), material);
            public void Disc(int cx, int y, int cz, int radius, byte material) =>
                Add(OperationKind.Disc, new int3(cx, y, cz), new int3(radius, 1, radius), material);
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.Cone, new int3(cx, baseY, cz), new int3(radius, height, radius), material);
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.HangingCone, new int3(cx, ceilingY, cz), new int3(radius, height, radius), material);
            public void Gable(int3 min, int3 size, bool alongX, byte material) => Add(OperationKind.Gable, min, size, material);
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) =>
                Add(OperationKind.Crenellate, start, new int3(count, width, height), material);
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) =>
                Add(OperationKind.CrenellateRing, new int3(cx, y, cz), new int3(radius, height, radius), material);
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) =>
                Add(OperationKind.Arch, min, new int3(width, height, depth), material);
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) =>
                Add(OperationKind.Stairs, min, new int3(width, steps * rise, steps * run), material);
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.SpiralStair, new int3(cx, baseY, cz), new int3(radius, height, radius), material);
            public void Carve(int3 min, int3 size) => Add(OperationKind.Carve, min, size, 0);
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) =>
                Add(OperationKind.Weather, min, size, coating);
            private void Add(OperationKind kind, int3 position, int3 size, byte material) =>
                Operations.Add(new RecordedOperation(kind, position, size, material));
        }
    }
}
