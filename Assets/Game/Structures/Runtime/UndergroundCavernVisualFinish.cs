using System;
using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Semantic visual-finish pass for large underground destinations. The pass derives every
    /// placement from authored cavern/ruin bounds and facing so it can be reused by caves at other
    /// origins and orientations; no showcase coordinates are encoded here.
    /// </summary>
    public readonly struct UndergroundCavernVisualFinishResult
    {
        public readonly int IrregularLobeCount;
        public readonly int ArchitecturalDetailCount;
        public readonly int StatueDetailCount;
        public readonly int AdditionalFormationCount;
        public readonly long VoxelsWritten;

        public UndergroundCavernVisualFinishResult(
            int irregularLobeCount,
            int architecturalDetailCount,
            int statueDetailCount,
            int additionalFormationCount,
            long voxelsWritten)
        {
            IrregularLobeCount = irregularLobeCount;
            ArchitecturalDetailCount = architecturalDetailCount;
            StatueDetailCount = statueDetailCount;
            AdditionalFormationCount = additionalFormationCount;
            VoxelsWritten = voxelsWritten;
        }

        public bool IsWellFormed =>
            IrregularLobeCount >= 3 && ArchitecturalDetailCount >= 12 &&
            StatueDetailCount >= 20 && AdditionalFormationCount >= 6 && VoxelsWritten > 0;
    }

    public static class UndergroundCavernVisualFinish
    {
        public static UndergroundCavernVisualFinishResult Author(
            IStructureAuthoringSession authoring,
            in UndergroundCavernRuinResult destination,
            Facing facing,
            in CaveMaterialPalette palette,
            uint weatherSeed)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (!destination.IsWellFormed)
                throw new ArgumentException("Visual finish requires a complete cavern/ruin result.", nameof(destination));

            long startWrites = authoring.TotalVoxelsWritten;
            int lobes = AuthorIrregularCavern(authoring, in destination.CavernBounds, facing, in palette);
            int architecture = AuthorAncientFacade(authoring, in destination.RuinBounds, facing, weatherSeed);
            int statueDetails = AuthorMonumentalStatues(authoring, in destination.RuinBounds, facing, weatherSeed);
            int formations = AuthorSilhouetteFormations(authoring, in destination.CavernBounds, facing, palette.Rock);

            return new UndergroundCavernVisualFinishResult(
                lobes,
                architecture,
                statueDetails,
                formations,
                authoring.TotalVoxelsWritten - startWrites);
        }

        private static int AuthorIrregularCavern(
            IStructureAuthoringSession a,
            in DecorationBounds bounds,
            Facing facing,
            in CaveMaterialPalette palette)
        {
            int3 centre = CentreOf(in bounds);
            int floorY = bounds.Min.y;
            int radius = math.max(70, (bounds.MaxExclusive.x - bounds.Min.x - 1) / 2);
            int height = math.max(90, bounds.MaxExclusive.y - bounds.Min.y);
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);

            int sideReach = radius * 2 / 3;
            int rearReach = radius * 3 / 5;
            int[] outerRadii =
            {
                math.max(62, radius * 56 / 100),
                math.max(58, radius * 52 / 100),
                math.max(54, radius * 48 / 100),
            };
            int3[] centres =
            {
                centre + side * sideReach + forward * (radius / 5),
                centre - side * (sideReach - radius / 12) + forward * (radius / 3),
                centre - forward * rearReach + side * (radius / 5),
            };
            int[] baseOffsets = { 4, 10, 7 };
            int[] heightCuts = { 26, 42, 54 };

            // Author all thin host shells before opening any lobe. A later lobe can therefore
            // overlap an earlier one without re-filling the already-carved union.
            for (int i = 0; i < centres.Length; i++)
            {
                int outer = outerRadii[i];
                int inner = math.max(40, outer - 14);
                int baseY = floorY + baseOffsets[i];
                int lobeHeight = math.max(72, height - heightCuts[i]);
                a.Cylinder(centres[i].x, baseY - 3, centres[i].z, outer, lobeHeight + 7,
                    palette.Rock, inner);
                a.Disc(centres[i].x, baseY - 3, centres[i].z, outer, palette.Rock);
                a.Disc(centres[i].x, baseY + lobeHeight + 3, centres[i].z, outer, palette.Rock);
            }

            for (int i = 0; i < centres.Length; i++)
            {
                int outer = outerRadii[i];
                int inner = math.max(40, outer - 14);
                int baseY = floorY + baseOffsets[i];
                int lobeHeight = math.max(72, height - heightCuts[i]);
                a.Cylinder(centres[i].x, baseY, centres[i].z, inner, lobeHeight, palette.Opening);
                a.Disc(centres[i].x, baseY - 1, centres[i].z, inner - 2, palette.Rock);
            }

            // Low shoulders and shelves make the floor/wall junction read as geology rather than
            // a mathematically smooth tank while staying away from the centreline circulation.
            int3 leftShelf = centre + side * (radius * 4 / 5) + forward * (radius / 4);
            int3 rightShelf = centre - side * (radius * 4 / 5) + forward * (radius / 8);
            a.Cylinder(leftShelf.x, floorY, leftShelf.z, 25, 22, palette.Rock);
            a.Cylinder(rightShelf.x, floorY, rightShelf.z, 29, 16, palette.Rock);
            a.Disc(leftShelf.x, floorY + 21, leftShelf.z, 31, palette.Rock);
            a.Disc(rightShelf.x, floorY + 15, rightShelf.z, 35, palette.Rock);
            return centres.Length;
        }

        private static int AuthorAncientFacade(
            IStructureAuthoringSession a,
            in DecorationBounds ruin,
            Facing facing,
            uint weatherSeed)
        {
            int3 centre = CentreOf(in ruin);
            int floorY = ruin.Min.y;
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            bool alongX = math.abs(forward.x) == 1;
            int forwardSize = alongX ? ruin.MaxExclusive.x - ruin.Min.x : ruin.MaxExclusive.z - ruin.Min.z;
            int sideSize = alongX ? ruin.MaxExclusive.z - ruin.Min.z : ruin.MaxExclusive.x - ruin.Min.x;
            int height = ruin.MaxExclusive.y - ruin.Min.y;
            int3 front = centre - forward * (forwardSize / 2 - 3);
            int detailCount = 0;

            // Layered stepped foundation gives the ruin visible weight and contact with the cave.
            OrientedBox(a, centre - forward * 2, facing, forwardSize + 16, sideSize + 24,
                floorY - 3, 5, GameMaterialIds.DarkStone); detailCount++;
            OrientedBox(a, centre - forward * 3, facing, forwardSize + 9, sideSize + 16,
                floorY + 2, 4, GameMaterialIds.MasonryMedium); detailCount++;
            OrientedBox(a, centre - forward * (forwardSize / 2 + 12), facing, 30, 54,
                floorY + 1, 3, GameMaterialIds.MasonrySmall); detailCount++;

            // Re-face the original box with a darker, articulated temple frontage, then cut a
            // genuinely arched opening through both the new facing and the old backing wall.
            OrientedBox(a, front + forward * 2, facing, 9, sideSize - 10,
                floorY + 5, height - 8, GameMaterialIds.Stone); detailCount++;
            AuthorArch(a, front, centre, facing, floorY + 4, 54, 58, 13, GameMaterialIds.MasonryMedium);
            detailCount++;
            AuthorArch(a, front - forward * 1, centre, facing, floorY + 5, 36, 48, 16, GameMaterialIds.Empty);
            detailCount++;
            CarveDoorThroat(a, front, centre, facing, floorY + 5, 28, 32, 20);

            // Massive side pylons, stepped broken pediment, and front columns produce a readable
            // silhouette at cavern scale instead of a single rectangular wall sheet.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int3 pylon = front + side * (sideSize / 2 - 11) * sign + forward * 2;
                OrientedBox(a, pylon, facing, 18, 20, floorY + 4, height + 9, GameMaterialIds.DarkStone);
                detailCount++;
                OrientedBox(a, pylon - forward * 3, facing, 24, 26, floorY + 1, 5, GameMaterialIds.MasonryLarge);
                detailCount++;

                int3 column = front + side * (sideSize / 3) * sign - forward * 8;
                a.Cylinder(column.x, floorY + 5, column.z, 5, math.max(42, height - 8), GameMaterialIds.MasonryMedium);
                a.Cylinder(column.x, floorY + 4, column.z, 8, 4, GameMaterialIds.MasonryLarge);
                detailCount += 2;
            }

            OrientedBox(a, front + forward * 1, facing, 10, sideSize - 24,
                floorY + height - 5, 9, GameMaterialIds.DarkStone); detailCount++;
            OrientedBox(a, front + forward * 1, facing, 11, sideSize * 2 / 3,
                floorY + height + 4, 7, GameMaterialIds.Stone); detailCount++;
            OrientedBox(a, front + forward * 1, facing, 12, sideSize / 3,
                floorY + height + 11, 6, GameMaterialIds.MasonryMedium); detailCount++;

            // Deterministic collapse removes upper corners and one roof shoulder. The cuts are
            // asymmetric but preserve the central entrance and normal gameplay circulation.
            int3 damageA = front + side * (sideSize / 2 - 17) + forward * 1;
            int3 damageB = front - side * (sideSize / 2 - 20) + forward * 1;
            OrientedCarve(a, damageA, facing, 18, 24, floorY + height - 18, 27);
            OrientedCarve(a, damageB, facing, 18, 18, floorY + height - 27, 34);

            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                int3 rubble = front - forward * (19 + math.abs(i) * 3) + side * (i * 11);
                int size = 6 + (math.abs(i) & 1) * 3;
                a.Box(new int3(rubble.x - size / 2, floorY + 3, rubble.z - size / 2),
                    new int3(size, 4 + math.abs(i), size + 2), GameMaterialIds.MasonrySmall);
                detailCount++;
            }

            a.Weather(
                new int3(ruin.Min.x - 12, floorY, ruin.Min.z - 12),
                new int3(
                    ruin.MaxExclusive.x - ruin.Min.x + 24,
                    height + 22,
                    ruin.MaxExclusive.z - ruin.Min.z + 24),
                Coatings.Moss,
                weatherSeed ^ 0xA11C1E57u,
                34);
            return detailCount;
        }

        private static int AuthorMonumentalStatues(
            IStructureAuthoringSession a,
            in DecorationBounds ruin,
            Facing facing,
            uint weatherSeed)
        {
            int3 centre = CentreOf(in ruin);
            int floorY = ruin.Min.y;
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            bool alongX = math.abs(forward.x) == 1;
            int forwardSize = alongX ? ruin.MaxExclusive.x - ruin.Min.x : ruin.MaxExclusive.z - ruin.Min.z;
            int sideSize = alongX ? ruin.MaxExclusive.z - ruin.Min.z : ruin.MaxExclusive.x - ruin.Min.x;
            int3 front = centre - forward * (forwardSize / 2 + 18);
            int details = 0;

            for (int sign = -1; sign <= 1; sign += 2)
            {
                int3 p = front + side * (sideSize / 2 - 12) * sign;

                OrientedBox(a, p, facing, 30, 30, floorY, 5, GameMaterialIds.MasonryLarge); details++;
                OrientedBox(a, p, facing, 24, 24, floorY + 5, 5, GameMaterialIds.DarkStone); details++;

                // Separate feet and legs, pelvis, tapered torso, shoulder bar and articulated arms
                // make the figure readable from the ruin approach at normal gameplay scale.
                OrientedBox(a, p - side * 6, facing, 10, 7, floorY + 10, 7, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p + side * 6, facing, 10, 7, floorY + 10, 7, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p - side * 6, facing, 9, 7, floorY + 17, 27, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p + side * 6, facing, 9, 7, floorY + 17, 27, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p, facing, 11, 19, floorY + 42, 9, GameMaterialIds.Stone); details++;
                OrientedBox(a, p, facing, 13, 21, floorY + 51, 22, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p, facing, 14, 29, floorY + 69, 7, GameMaterialIds.Stone); details++;
                OrientedBox(a, p - side * 13, facing, 8, 7, floorY + 57, 25, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p + side * 13, facing, 8, 7, floorY + 57, 25, GameMaterialIds.DarkStone); details++;

                a.Cylinder(p.x, floorY + 76, p.z, 4, 6, GameMaterialIds.DarkStone); details++;
                a.Cylinder(p.x, floorY + 82, p.z, 8, 16, GameMaterialIds.DarkStone); details++;
                OrientedBox(a, p - forward * 7, facing, 4, 8, floorY + 87, 6, GameMaterialIds.Stone); details++;
                OrientedBox(a, p, facing, 12, 18, floorY + 98, 4, GameMaterialIds.MasonryMedium); details++;

                // Different deterministic chips avoid cloned pristine figures while keeping both
                // monuments visibly complete enough to read as humanoid statues.
                int3 chip = sign < 0
                    ? p + side * 12 + forward * 1
                    : p - side * 4 - forward * 5;
                OrientedCarve(a, chip, facing, 8, 8, sign < 0 ? floorY + 69 : floorY + 91, 9);

                a.Weather(
                    new int3(p.x - 18, floorY, p.z - 18),
                    new int3(37, 104, 37),
                    Coatings.Moss,
                    weatherSeed ^ (uint)(0x57A70010 + sign),
                    24);
            }

            return details;
        }

        private static int AuthorSilhouetteFormations(
            IStructureAuthoringSession a,
            in DecorationBounds cavern,
            Facing facing,
            byte rock)
        {
            int3 centre = CentreOf(in cavern);
            int floorY = cavern.Min.y;
            int height = cavern.MaxExclusive.y - cavern.Min.y;
            int radius = math.max(70, (cavern.MaxExclusive.x - cavern.Min.x - 1) / 2);
            int3 forward = FacingVector(facing);
            int3 side = new int3(-forward.z, 0, forward.x);
            int ceilingY = floorY + height - 3;

            int3[] hanging =
            {
                centre + side * (radius * 3 / 4) + forward * (radius / 5),
                centre - side * (radius * 2 / 3) + forward * (radius / 3),
                centre + side * (radius / 2) - forward * (radius / 2),
                centre - side * (radius / 3) - forward * (radius * 3 / 5),
            };
            for (int i = 0; i < hanging.Length; i++)
                a.HangingCone(hanging[i].x, ceilingY - i * 5, hanging[i].z,
                    10 + i * 2, 34 + i * 8, rock);

            int3 left = centre + side * (radius * 4 / 5) - forward * (radius / 6);
            int3 right = centre - side * (radius * 4 / 5) + forward * (radius / 7);
            a.Cone(left.x, floorY, left.z, 14, 42, rock);
            a.Cone(right.x, floorY, right.z, 18, 52, rock);
            return hanging.Length + 2;
        }

        private static void AuthorArch(
            IStructureAuthoringSession a,
            int3 front,
            int3 centre,
            Facing facing,
            int baseY,
            int width,
            int height,
            int depth,
            byte material)
        {
            int3 forward = FacingVector(facing);
            if (math.abs(forward.x) == 1)
            {
                int minX = front.x - depth / 2;
                a.Arch(new int3(minX, baseY, centre.z - width / 2),
                    width, height, depth, 0, material);
            }
            else
            {
                int minZ = front.z - depth / 2;
                a.Arch(new int3(centre.x - width / 2, baseY, minZ),
                    width, height, depth, 2, material);
            }
        }

        private static void CarveDoorThroat(
            IStructureAuthoringSession a,
            int3 front,
            int3 centre,
            Facing facing,
            int baseY,
            int width,
            int height,
            int depth)
        {
            int3 forward = FacingVector(facing);
            if (math.abs(forward.x) == 1)
                a.Carve(new int3(front.x - depth / 2, baseY, centre.z - width / 2),
                    new int3(depth, height, width));
            else
                a.Carve(new int3(centre.x - width / 2, baseY, front.z - depth / 2),
                    new int3(width, height, depth));
        }

        private static void OrientedBox(
            IStructureAuthoringSession a,
            int3 centre,
            Facing facing,
            int forwardSize,
            int sideSize,
            int baseY,
            int height,
            byte material)
        {
            int3 forward = FacingVector(facing);
            int sizeX = math.abs(forward.x) == 1 ? forwardSize : sideSize;
            int sizeZ = math.abs(forward.x) == 1 ? sideSize : forwardSize;
            a.Box(new int3(centre.x - sizeX / 2, baseY, centre.z - sizeZ / 2),
                new int3(sizeX, height, sizeZ), material);
        }

        private static void OrientedCarve(
            IStructureAuthoringSession a,
            int3 centre,
            Facing facing,
            int forwardSize,
            int sideSize,
            int baseY,
            int height)
        {
            int3 forward = FacingVector(facing);
            int sizeX = math.abs(forward.x) == 1 ? forwardSize : sideSize;
            int sizeZ = math.abs(forward.x) == 1 ? sideSize : forwardSize;
            a.Carve(new int3(centre.x - sizeX / 2, baseY, centre.z - sizeZ / 2),
                new int3(sizeX, height, sizeZ));
        }

        private static int3 CentreOf(in DecorationBounds bounds) => new int3(
            (bounds.Min.x + bounds.MaxExclusive.x) / 2,
            bounds.Min.y,
            (bounds.Min.z + bounds.MaxExclusive.z) / 2);

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.East: return new int3(1, 0, 0);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.West: return new int3(-1, 0, 0);
                default: return new int3(0, 0, 1);
            }
        }
    }
}
