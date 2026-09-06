using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NewHouseReferenceAuthoringTests
    {
        [NUnit.Framework.Test]
        public void AuthorHouse_IsTranslationInvariant_AndDoesNotOwnReferenceSiteGround()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 firstOrigin = new(40, 16, -70);
            int3 secondOrigin = new(-113, 43, 271);
            int3 delta = secondOrigin - firstOrigin;

            var first = new RecordingSession();
            var second = new RecordingSession();
            NewHouseReferenceResult firstResult = NewHouseReferenceAuthoring.AuthorHouse(
                first, firstOrigin, in config, in palette);
            NewHouseReferenceResult secondResult = NewHouseReferenceAuthoring.AuthorHouse(
                second, secondOrigin, in config, in palette);

            Assert.That(first.Operations.Count, Is.GreaterThan(100));
            Assert.That(second.Operations.Count, Is.EqualTo(first.Operations.Count));
            Assert.That(first.Operations.Exists(op => op.Material == palette.Ground), Is.False,
                "Reusable house geometry must not absorb reference-shot ground/site policy.");

            for (int i = 0; i < first.Operations.Count; i++)
            {
                RecordedOperation a = first.Operations[i];
                RecordedOperation b = second.Operations[i];
                Assert.That(b.Kind, Is.EqualTo(a.Kind));
                Assert.That(b.Material, Is.EqualTo(a.Material));
                Assert.That(b.Size, Is.EqualTo(a.Size));
                Assert.That(b.Position, Is.EqualTo(a.Position + delta),
                    $"Primitive {i} is not translation-invariant.");
            }

            Assert.That(secondResult.Min, Is.EqualTo(firstResult.Min + delta));
            Assert.That(secondResult.MaxExclusive, Is.EqualTo(firstResult.MaxExclusive + delta));
            Assert.That(secondResult.DoorCentreX, Is.EqualTo(firstResult.DoorCentreX + delta.x));
            Assert.That(secondResult.FrontZ, Is.EqualTo(firstResult.FrontZ + delta.z));
            Assert.That(secondResult.RidgeY, Is.EqualTo(firstResult.RidgeY + delta.y));
        }

        [NUnit.Framework.Test]
        public void AuthorHouse_UsesEveryHighValueReferenceMaterialRole()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            var session = new RecordingSession();

            NewHouseReferenceAuthoring.AuthorHouse(session, int3.zero, in config, in palette);

            foreach (byte required in new[]
                     {
                         palette.Plaster, palette.Timber, palette.Roof, palette.Stone,
                         palette.Glass, palette.Door, palette.Accent, palette.Flowers,
                         palette.Foliage,
                     })
            {
                Assert.That(session.Operations.Exists(op => op.Material == required), Is.True,
                    $"Reference material role {required} was not authored by the production house path.");
            }
        }

        [NUnit.Framework.Test]
        public void AuthorReferenceSite_IsSeparateAndContainsApproachAndLandscapePrimitives()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            var session = new RecordingSession();

            NewHouseReferenceAuthoring.AuthorReferenceSite(
                session, new int3(12, 9, 33), in config, in palette);

            Assert.That(session.Operations.Exists(op => op.Material == palette.Ground), Is.True);
            Assert.That(session.Operations.Exists(op => op.Material == palette.Stone), Is.True);
            Assert.That(session.Operations.Exists(op => op.Material == palette.Foliage), Is.True);
            Assert.That(session.Operations.Exists(op => op.Kind == OperationKind.Disc), Is.True,
                "Reference approach should contain the curved stepping-stone rhythm.");
            Assert.That(session.Operations.Exists(op => op.Kind == OperationKind.Cone), Is.True,
                "Reference landscape should contain production structure primitives for shrubs.");
        }

        private static NewHouseReferencePalette Palette() =>
            new(plaster: 41, timber: 42, roof: 43, stone: 44, glass: 45,
                door: 46, accent: 47, ground: 48, flowers: 49, foliage: 50);

        private enum OperationKind : byte
        {
            Set,
            SetStyled,
            Coat,
            FillBulk,
            FillColumnBulk,
            Box,
            HollowBox,
            Cylinder,
            Disc,
            Cone,
            HangingCone,
            Gable,
            Crenellate,
            CrenellateRing,
            Arch,
            Stairs,
            SpiralStair,
            Carve,
            Weather,
        }

        private readonly struct RecordedOperation
        {
            public RecordedOperation(OperationKind kind, int3 position, int3 size, byte material)
            {
                Kind = kind;
                Position = position;
                Size = size;
                Material = material;
            }

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

            public void Set(int x, int y, int z, byte material) =>
                Add(OperationKind.Set, new int3(x, y, z), new int3(1), material);

            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Add(OperationKind.SetStyled, new int3(x, y, z), new int3(1), material);

            public void Coat(int x, int y, int z, byte coating) =>
                Add(OperationKind.Coat, new int3(x, y, z), new int3(1), coating);

            public void FillBulk(int3 min, int3 size, byte material) =>
                Add(OperationKind.FillBulk, min, size, material);

            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
                Add(OperationKind.FillColumnBulk, new int3(x, minY, z),
                    new int3(1, maxYExclusive - minY, 1), material);

            public void Box(int3 min, int3 size, byte material) =>
                Add(OperationKind.Box, min, size, material);

            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) => Add(OperationKind.HollowBox, min, size, material);

            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                int innerRadius = 0) => Add(OperationKind.Cylinder, new int3(cx, baseY, cz),
                    new int3(radius, height, innerRadius), material);

            public void Disc(int cx, int y, int cz, int radius, byte material) =>
                Add(OperationKind.Disc, new int3(cx, y, cz), new int3(radius, 1, radius), material);

            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.Cone, new int3(cx, baseY, cz), new int3(radius, height, radius), material);

            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.HangingCone, new int3(cx, ceilingY, cz),
                    new int3(radius, height, radius), material);

            public void Gable(int3 min, int3 size, bool alongX, byte material) =>
                Add(OperationKind.Gable, min, size, material);

            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) => Add(OperationKind.Crenellate, start,
                    new int3(count, width, height), material);

            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) =>
                Add(OperationKind.CrenellateRing, new int3(cx, y, cz),
                    new int3(radius, height, radius), material);

            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) =>
                Add(OperationKind.Arch, min, new int3(width, height, depth), material);

            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) =>
                Add(OperationKind.Stairs, min, new int3(width, steps * rise, steps * run), material);

            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(OperationKind.SpiralStair, new int3(cx, baseY, cz),
                    new int3(radius, height, radius), material);

            public void Carve(int3 min, int3 size) =>
                Add(OperationKind.Carve, min, size, 0);

            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) =>
                Add(OperationKind.Weather, min, size, coating);

            private void Add(OperationKind kind, int3 position, int3 size, byte material) =>
                Operations.Add(new RecordedOperation(kind, position, size, material));
        }
    }
}
