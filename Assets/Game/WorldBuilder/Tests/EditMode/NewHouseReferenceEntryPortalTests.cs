using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    [NUnit.Framework.TestFixture]
    public sealed class NewHouseReferenceEntryPortalTests
    {
        [NUnit.Framework.Test]
        public void AuthorHouse_FinalEntryPortalProjectsStoneRingAroundRecessedArchedDoor()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(13, 8, -27);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int centre = origin.x + config.Width / 2;
            int first = origin.y + config.FirstFloorY;
            int upper = origin.y + config.UpperFloorY;
            int front = origin.z;
            int portalY = first + 3;
            int springY = portalY + 16;

            int middleClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 21, upper + 4, origin.z - 8)) &&
                op.Size.Equals(new int3(43, 31, 10)));
            int entryClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 16, first + 1, front - 7)) &&
                op.Size.Equals(new int3(33, 31, 11)));
            int entryRefill = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Stone &&
                op.Position.Equals(new int3(centre - 16, first + 1, front - 3)) &&
                op.Size.Equals(new int3(33, 31, 6)));
            int recessedDoor = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Door &&
                op.Position.Equals(new int3(centre - 8, portalY, front + 1)) &&
                op.Size.Equals(new int3(17, 1, 2)));
            int leftJamb = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Stone &&
                op.Position.Equals(new int3(centre - 14, portalY - 2, front - 8)) &&
                op.Size.Equals(new int3(7, 19, 5)));
            int rightJamb = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Stone &&
                op.Position.Equals(new int3(centre + 8, portalY - 2, front - 8)) &&
                op.Size.Equals(new int3(7, 19, 5)));
            int crown = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Stone &&
                op.Position.Equals(new int3(centre, springY + 13, front - 8)) &&
                op.Size.Equals(new int3(1, 1, 5)));

            Assert.That(middleClear, Is.GreaterThanOrEqualTo(0));
            Assert.That(entryClear, Is.GreaterThan(middleClear),
                "The lower portal correction must run late, after the compact middle-facade pass.");
            Assert.That(entryRefill, Is.GreaterThan(entryClear));
            Assert.That(recessedDoor, Is.GreaterThan(entryRefill),
                "The arched timber door must be restored after the destructive lower-entry refill.");
            Assert.That(leftJamb, Is.GreaterThan(recessedDoor));
            Assert.That(rightJamb, Is.GreaterThan(recessedDoor));
            Assert.That(crown, Is.GreaterThan(leftJamb),
                "The final portal must include a projecting round stone crown, not only flat jambs.");

            RecordedOperation clear = session.Operations[entryClear];
            int leftSideWindowCentre = centre - 29;
            int rightSideWindowCentre = centre + 29;
            Assert.That(clear.Position.x, Is.GreaterThan(leftSideWindowCentre + 4),
                "Entry repair must not touch the left lower side window.");
            Assert.That(clear.Position.x + clear.Size.x, Is.LessThan(rightSideWindowCentre - 4),
                "Entry repair must not touch the right lower side window.");
        }

        private static NewHouseReferencePalette Palette() =>
            new(plaster: 41, timber: 42, roof: 43, stone: 44, glass: 45,
                door: 46, accent: 47, ground: 48, flowers: 49, foliage: 50, ornament: 51);

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

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<RecordedOperation> Operations = new();
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
