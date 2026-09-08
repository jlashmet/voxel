using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NewHouseReferenceCompactFacadeTests
    {
        [NUnit.Framework.Test]
        public void AuthorHouse_CompactMiddleFacadeRebuildOccursAfterPortraitPass_AndRestoresReferenceScaleOpening()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(19, 6, -31);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int centre = origin.x + config.Width / 2;
            int upper = origin.y + config.UpperFloorY;
            int ridge = origin.y + config.MainRidgeY;
            int portraitEave = upper + 31;
            int roofZ = origin.z - config.RoofOverhang - 2;
            int front = origin.z - 2;
            int rear = origin.z + config.Depth + 1;

            int portraitClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 34, portraitEave, roofZ - 1)));
            int middleClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 21, upper + 4, front - 6)) &&
                op.Size.Equals(new int3(43, 31, 10)));
            int middleRefill = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(centre - 20, upper + 4, front - 4)) &&
                op.Size.Equals(new int3(41, 31, 7)));
            int compactArch = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 6, upper + 8, front - 4)) &&
                op.Size.Equals(new int3(13, 1, 9)));
            int leftShutter = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Accent &&
                op.Position.Equals(new int3(centre - 12, upper + 9, front - 5)) &&
                op.Size.Equals(new int3(4, 17, 2)));
            int rightShutter = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Accent &&
                op.Position.Equals(new int3(centre + 9, upper + 9, front - 5)) &&
                op.Size.Equals(new int3(4, 17, 2)));
            int oversizedBottomFrame = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 19, upper + 4, front - 5)) &&
                op.Size.Equals(new int3(39, 2, 2)));
            int oversizedTopFrame = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 17, upper + 31, front - 5)) &&
                op.Size.Equals(new int3(35, 2, 2)));
            int oversizedLeftPost = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 19, upper + 6, front - 5)) &&
                op.Size.Equals(new int3(2, 25, 2)));
            int oversizedRightPost = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre + 17, upper + 6, front - 5)) &&
                op.Size.Equals(new int3(2, 25, 2)));

            Assert.That(portraitClear, Is.GreaterThanOrEqualTo(0));
            Assert.That(middleClear, Is.GreaterThan(portraitClear),
                "The compact middle facade correction must run after the portrait silhouette pass.");
            Assert.That(session.Operations[middleClear].Position.z + session.Operations[middleClear].Size.z,
                Is.LessThan(rear - 2),
                "The middle facade repair must remain shallow and never reach the rear audit shell.");
            Assert.That(middleRefill, Is.GreaterThan(middleClear),
                "The oversized opening/cross region must be restored with production plaster before the compact opening is carved.");
            Assert.That(compactArch, Is.GreaterThan(middleRefill),
                "The final 13-voxel reference-scale arch must be carved after the destructive facade refill.");
            Assert.That(leftShutter, Is.GreaterThan(compactArch));
            Assert.That(rightShutter, Is.GreaterThan(compactArch));
            Assert.That(oversizedBottomFrame, Is.EqualTo(-1),
                "The final compact middle opening must not be enclosed by the obsolete room-sized bottom timber cage.");
            Assert.That(oversizedTopFrame, Is.EqualTo(-1),
                "The final compact middle opening must not be enclosed by the obsolete room-sized top timber cage.");
            Assert.That(oversizedLeftPost, Is.EqualTo(-1),
                "The final compact middle opening must not retain the obsolete 25-voxel left timber jamb.");
            Assert.That(oversizedRightPost, Is.EqualTo(-1),
                "The final compact middle opening must not retain the obsolete 25-voxel right timber jamb.");
            Assert.That(config.MainRidgeY + origin.y, Is.EqualTo(ridge));
        }

        [NUnit.Framework.Test]
        public void AuthorHouse_HighGableArchKeepsMullionContained_WithoutCrossingStructuralBelt()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(-27, 13, 35);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int centre = origin.x + config.Width / 2;
            int upper = origin.y + config.UpperFloorY;
            int eave = origin.y + config.MainEaveY;
            int portraitEave = upper + 31;
            int front = origin.z - 2;

            int compactArch = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 5, eave + 12, front - 4)) &&
                op.Size.Equals(new int3(11, 1, 9)));
            int insetGlass = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Glass &&
                op.Position.Equals(new int3(centre - 4, eave + 12, front + 1)) &&
                op.Size.Equals(new int3(9, 1, 2)));
            int fullWidthGlass = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Glass &&
                op.Position.Equals(new int3(centre - 5, eave + 12, front + 1)) &&
                op.Size.Equals(new int3(11, 1, 2)));
            int leftInnerFrame = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 5, eave + 12, front - 4)) &&
                op.Size.Equals(new int3(1, 1, 2)));
            int rightInnerFrame = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre + 5, eave + 12, front - 4)) &&
                op.Size.Equals(new int3(1, 1, 2)));
            int containedHorizontalMullion = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 4, eave + 17, front - 4)) &&
                op.Size.Equals(new int3(9, 1, 2)));
            int flowerBox = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 12, portraitEave + 7, front - 2)) &&
                op.Size.Equals(new int3(24, 3, 4)));
            int lowerStructuralBelt = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 20, portraitEave + 4, front - 4)) &&
                op.Size.Equals(new int3(41, 2, 2)));
            int crossingBelt = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Timber &&
                op.Position.Equals(new int3(centre - 14, portraitEave + 27, front - 4)) &&
                op.Size.Equals(new int3(29, 2, 2)));

            Assert.That(compactArch, Is.GreaterThanOrEqualTo(0),
                "The final high-gable reference opening must remain an 11-voxel arched panel.");
            Assert.That(insetGlass, Is.GreaterThan(compactArch),
                "The final high-gable pane must be inset one voxel inside the timber arch frame.");
            Assert.That(fullWidthGlass, Is.EqualTo(-1),
                "The late reference pane must not remain a flat full-width glass slab.");
            Assert.That(leftInnerFrame, Is.GreaterThan(compactArch));
            Assert.That(rightInnerFrame, Is.GreaterThan(compactArch));
            Assert.That(containedHorizontalMullion, Is.GreaterThan(compactArch),
                "The high-gable window must retain its compact internal mullion after the arch is carved.");
            Assert.That(flowerBox, Is.GreaterThan(compactArch),
                "The dense high-gable flower box must survive the opening correction.");
            Assert.That(lowerStructuralBelt, Is.GreaterThan(flowerBox),
                "The structural belt below the high opening remains part of the reference gable composition.");
            Assert.That(crossingBelt, Is.EqualTo(-1),
                "No full-width structural timber belt may cross the final high-gable glazing.");
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
