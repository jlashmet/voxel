using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class NewHouseReferenceRefinementTests
    {
        [NUnit.Framework.Test]
        public void AuthorHouse_RefinementIsTranslationInvariant_AndKeepsSitePolicySeparate()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 firstOrigin = new(23, 11, -37);
            int3 secondOrigin = new(-91, 42, 205);
            int3 delta = secondOrigin - firstOrigin;
            var first = new RecordingSession();
            var second = new RecordingSession();

            NewHouseReferenceResult firstResult = NewHouseReferenceRefinement.AuthorHouse(
                first, firstOrigin, in config, in palette);
            NewHouseReferenceResult secondResult = NewHouseReferenceRefinement.AuthorHouse(
                second, secondOrigin, in config, in palette);

            Assert.That(first.Operations.Count, Is.GreaterThan(180));
            Assert.That(second.Operations.Count, Is.EqualTo(first.Operations.Count));
            Assert.That(first.Operations.Exists(op => op.Material == palette.Ground), Is.False,
                "Reusable refinement must not absorb reference-site ground policy.");

            RecordedOperation roofReplacement = first.Operations.Find(op =>
                op.Kind == OperationKind.Carve &&
                op.Size.x >= config.Width + 40 &&
                op.Size.z >= config.Depth + 40);
            Assert.That(roofReplacement.Kind, Is.EqualTo(OperationKind.Carve),
                "The refinement must remove the complete conflicting upper roof composition before rebuilding it.");
            Assert.That(first.Operations.Exists(op =>
                    op.Kind == OperationKind.Carve && op.Size.x == 20 && op.Size.y >= 20),
                Is.True,
                "The obsolete low side-wing roof geometry must be removed rather than left under the reference roof.");

            for (int i = 0; i < first.Operations.Count; i++)
            {
                RecordedOperation a = first.Operations[i];
                RecordedOperation b = second.Operations[i];
                Assert.That(b.Kind, Is.EqualTo(a.Kind));
                Assert.That(b.Material, Is.EqualTo(a.Material));
                Assert.That(b.Size, Is.EqualTo(a.Size));
                Assert.That(b.Position, Is.EqualTo(a.Position + delta),
                    $"Refinement primitive {i} is not translation-invariant.");
            }

            Assert.That(secondResult.Min, Is.EqualTo(firstResult.Min + delta));
            Assert.That(secondResult.MaxExclusive, Is.EqualTo(firstResult.MaxExclusive + delta));
        }

        [NUnit.Framework.Test]
        public void AuthorHouse_AuditShellInfillOccursAfterRoofClear_AndRearWindowsCarveAfterInfill()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(17, 9, -23);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int upper = origin.y + config.UpperFloorY;
            int ridge = origin.y + config.MainRidgeY;
            int rear = origin.z + config.Depth + 1;

            int roofClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(origin.x - 22, upper + 13, origin.z - 18)) &&
                op.Size.Equals(new int3(config.Width + 44, ridge - upper + 32, config.Depth + 42)));
            int leftShell = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(origin.x + 1, upper, origin.z + 8)) &&
                op.Size.Equals(new int3(3, 25, config.Depth - 7)));
            int rightShell = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(origin.x + config.Width - 4, upper, origin.z + 8)) &&
                op.Size.Equals(new int3(3, 25, config.Depth - 7)));
            int rearShell = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(origin.x + 1, upper, rear - 2)) &&
                op.Size.Equals(new int3(config.Width - 2, 25, 3)));
            int centreRearWindowCarve = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(origin.x + config.Width / 2 - 6, upper + 7, rear - 4)) &&
                op.Size.Equals(new int3(13, 20, 8)));

            Assert.That(roofClear, Is.GreaterThanOrEqualTo(0), "Expected destructive roof-clear operation.");
            Assert.That(leftShell, Is.GreaterThan(roofClear), "Left upper shell must be restored after roof clear.");
            Assert.That(rightShell, Is.GreaterThan(roofClear), "Right upper shell must be restored after roof clear.");
            Assert.That(rearShell, Is.GreaterThan(roofClear), "Rear upper shell must be restored after roof clear.");
            Assert.That(centreRearWindowCarve, Is.GreaterThan(rearShell),
                "Intentional rear openings must be carved after opaque shell infill, not leave wall-sized holes.");
        }

        [NUnit.Framework.Test]
        public void AuthorHouse_FinishPassRunsAfterAuditShell_AndRebuildsCompactReferenceDetails()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(31, 7, -41);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int centre = origin.x + config.Width / 2;
            int upper = origin.y + config.UpperFloorY;
            int eave = origin.y + config.MainEaveY;
            int ridge = origin.y + config.MainRidgeY;
            int rear = origin.z + config.Depth + 1;
            int portraitEave = upper + 31;
            int clearRise = math.max(38, ridge - portraitEave);
            int portraitRise = math.min(clearRise, math.max(38, config.Width / 2 - 2));
            int portraitRidge = portraitEave + portraitRise;

            int rearShell = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(origin.x + 1, upper, rear - 2)) &&
                op.Size.Equals(new int3(config.Width - 2, 25, 3)));
            int smallerHighWindow = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 5, eave + 12, origin.z - 6)) &&
                op.Size.Equals(new int3(11, 1, 9)));
            int rearGableClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(origin.x + 6, upper + 29, rear - 4)) &&
                op.Size.Equals(new int3(config.Width - 12, ridge - (upper + 29) + 8, 8)));
            int crestClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 8, portraitRidge + 1, origin.z)) &&
                op.Size.Equals(new int3(17, ridge - portraitRidge + 18, 16)));
            int compactFinial = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Cone && op.Material == palette.Ornament &&
                op.Position.Equals(new int3(centre, portraitRidge + 8, origin.z + 7)) &&
                op.Size.Equals(new int3(2, 6, 2)));
            int sweptLeftTip = session.Operations.FindIndex(math.max(0, rearShell + 1), op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.x == centre - 36 && op.Position.z == origin.z - config.RoofOverhang - 2);

            Assert.That(rearShell, Is.GreaterThanOrEqualTo(0));
            Assert.That(smallerHighWindow, Is.GreaterThan(rearShell),
                "Reference finish must run after structural shell repair and carve through the front repair depth so the smaller high window is visible.");
            Assert.That(rearGableClear, Is.GreaterThan(rearShell),
                "The final pass must remove the obsolete full-height rear triangle without erasing the lower rear wall/window shell.");
            Assert.That(crestClear, Is.GreaterThan(smallerHighWindow),
                "All stale crest layers above the lowered portrait apex must be cleared after the gable face is refined.");
            Assert.That(compactFinial, Is.GreaterThan(crestClear),
                "One compact tapered finial must be anchored to the lowered portrait apex.");
            Assert.That(sweptLeftTip, Is.GreaterThan(rearShell),
                "The portrait roof must retain a controlled outward/downward swept eave tip in the final post-audit pass.");
        }

        [NUnit.Framework.Test]
        public void AuthorHouse_SweptPortraitProfileIsConcave_AndRestoresOpeningsAfterRebuild()
        {
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            NewHouseReferencePalette palette = Palette();
            int3 origin = new(-13, 12, 27);
            var session = new RecordingSession();

            NewHouseReferenceRefinement.AuthorHouse(session, origin, in config, in palette);

            int centre = origin.x + config.Width / 2;
            int upper = origin.y + config.UpperFloorY;
            int eave = origin.y + config.MainEaveY;
            int ridge = origin.y + config.MainRidgeY;
            int portraitEave = upper + 31;
            int clearRise = math.max(38, ridge - portraitEave);
            int rise = math.min(clearRise, math.max(38, config.Width / 2 - 2));
            int roofZ = origin.z - config.RoofOverhang - 2;
            int front = origin.z - 2;
            int rear = origin.z + config.Depth + 1;

            int rearShell = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.Equals(new int3(origin.x + 1, upper, rear - 2)) &&
                op.Size.Equals(new int3(config.Width - 2, 25, 3)));
            int profileClear = session.Operations.FindIndex(op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 34, portraitEave, roofZ - 1)) &&
                op.Size.Equals(new int3(69, clearRise + 8, config.RoofOverhang + 20)));
            int profileSearchStart = math.max(0, profileClear + 1);
            int baseLeftEdge = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.y == portraitEave && op.Position.z == roofZ &&
                op.Size.Equals(new int3(4, 2, 20)) && op.Position.x < centre);
            int midY = portraitEave + rise / 2;
            int midLeftEdge = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.y == midY && op.Position.z == roofZ &&
                op.Size.Equals(new int3(4, 2, 20)) && op.Position.x < centre);
            int midRightEdge = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.y == midY && op.Position.z == roofZ &&
                op.Size.Equals(new int3(4, 2, 20)) && op.Position.x > centre);
            int apexLeftEdge = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.y == portraitEave + rise && op.Position.z == roofZ &&
                op.Size.Equals(new int3(4, 2, 20)) && op.Position.x < centre);
            int obsoleteHighRoof = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Roof &&
                op.Position.z == roofZ && op.Position.x >= centre - 36 && op.Position.x <= centre + 36 &&
                op.Position.y > portraitEave + rise + 1 && op.Position.y <= portraitEave + clearRise + 1);
            int midPlaster = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Box && op.Material == palette.Plaster &&
                op.Position.y == midY && op.Position.z == front - 1 &&
                op.Size.z == 6 && op.Position.x < centre &&
                op.Position.x + op.Size.x > centre);
            int smallerHighWindow = session.Operations.FindIndex(profileSearchStart, op =>
                op.Kind == OperationKind.Carve &&
                op.Position.Equals(new int3(centre - 5, eave + 12, origin.z - 6)) &&
                op.Size.Equals(new int3(11, 1, 9)));

            Assert.That(rearShell, Is.GreaterThanOrEqualTo(0));
            Assert.That(profileClear, Is.GreaterThan(rearShell),
                "The swept silhouette replacement must run after the structural audit shell is restored.");
            RecordedOperation clear = session.Operations[profileClear];
            Assert.That(clear.Position.z + clear.Size.z, Is.LessThan(rear - 2),
                "The destructive silhouette clear must remain shallow/front-only and never reach the rear audit shell.");
            Assert.That(rise, Is.LessThan(clearRise),
                "The final portrait roof must no longer rebuild all the way to the obsolete global ridge.");
            Assert.That(baseLeftEdge, Is.GreaterThan(profileClear));
            Assert.That(midLeftEdge, Is.GreaterThan(baseLeftEdge));
            Assert.That(midRightEdge, Is.GreaterThan(profileClear));
            Assert.That(apexLeftEdge, Is.GreaterThan(midLeftEdge),
                "The lowered width-driven portrait apex must still be explicitly authored.");
            Assert.That(obsoleteHighRoof, Is.EqualTo(-1),
                "No final portrait-roof edge may survive in the cleared needle-spire range above the lowered apex.");
            Assert.That(midPlaster, Is.GreaterThan(profileClear));

            RecordedOperation baseEdge = session.Operations[baseLeftEdge];
            RecordedOperation midEdge = session.Operations[midLeftEdge];
            RecordedOperation rightEdge = session.Operations[midRightEdge];
            RecordedOperation plaster = session.Operations[midPlaster];
            int baseReach = centre - baseEdge.Position.x;
            int midReach = centre - midEdge.Position.x;
            Assert.That(baseReach, Is.GreaterThan(midReach * 2),
                "The portrait roof must narrow nonlinearly through the mid-gable; a straight A-frame profile is not acceptable.");
            Assert.That(plaster.Position.x, Is.EqualTo(midEdge.Position.x + midEdge.Size.x),
                "The left roof inner edge must meet the plaster shell without a sky-visible seam.");
            Assert.That(plaster.Position.x + plaster.Size.x, Is.EqualTo(rightEdge.Position.x),
                "The right roof inner edge must meet the plaster shell without a sky-visible seam.");
            Assert.That(smallerHighWindow, Is.GreaterThan(midLeftEdge),
                "Final upper openings must be carved after the swept shell rebuild so the silhouette correction cannot erase them.");
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
