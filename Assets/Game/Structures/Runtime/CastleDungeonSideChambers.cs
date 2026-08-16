using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned puzzle room and secret treasury branching from the main dungeon hall.</summary>
    public static class CastleDungeonSideChambers
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int tx,
            int trapZ,
            int dungeonY)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            const int corridorHalf = 10;
            const int corridorHeight = 30;

            int puzzleMinX = tx + 176;
            int puzzleMinZ = trapZ - 58;
            authoring.Box(
                new int3(tx + 118, dungeonY + 2, trapZ - corridorHalf),
                new int3(70, corridorHeight, corridorHalf * 2),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(tx + 118, dungeonY, trapZ - corridorHalf),
                new int3(70, 2, corridorHalf * 2),
                GameMaterialIds.DarkStone);
            authoring.FillBulk(
                new int3(puzzleMinX, dungeonY + 2, puzzleMinZ),
                new int3(100, 38, 116),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(puzzleMinX, dungeonY, puzzleMinZ),
                new int3(100, 2, 116),
                GameMaterialIds.DarkStone);

            authoring.Box(
                new int3(puzzleMinX + 8, dungeonY + 1, trapZ - 2),
                new int3(84, 1, 4),
                GameMaterialIds.Slate);
            authoring.Box(
                new int3(puzzleMinX + 48, dungeonY + 1, puzzleMinZ + 8),
                new int3(4, 1, 100),
                GameMaterialIds.Slate);

            for (int ring = 0; ring < 3; ring++)
            {
                int inset = 18 + ring * 10;
                byte inlay = ring == 1 ? GameMaterialIds.Gold : GameMaterialIds.Cloth;
                authoring.Box(
                    new int3(puzzleMinX + inset, dungeonY + 1, puzzleMinZ + 15),
                    new int3(2, 1, 86),
                    inlay);
                authoring.Box(
                    new int3(puzzleMinX + 98 - inset, dungeonY + 1, puzzleMinZ + 15),
                    new int3(2, 1, 86),
                    inlay);
            }

            int puzzleCx = puzzleMinX + 50;
            int puzzleCz = trapZ;
            int2[] runeOffsets =
            {
                new(-26, -30), new(26, -30), new(-26, 30), new(26, 30),
            };
            for (int i = 0; i < runeOffsets.Length; i++)
            {
                int px = puzzleCx + runeOffsets[i].x;
                int pz = puzzleCz + runeOffsets[i].y;
                authoring.Box(
                    new int3(px - 8, dungeonY + 2, pz - 8),
                    new int3(16, 8, 16),
                    GameMaterialIds.Stone);
                authoring.Disc(
                    px,
                    dungeonY + 10,
                    pz,
                    6,
                    GameMaterialIds.DarkStone);
                authoring.Cone(
                    px,
                    dungeonY + 11,
                    pz,
                    3 + (i & 1),
                    8 + i * 2,
                    i % 2 == 0 ? GameMaterialIds.Glass : GameMaterialIds.Gold);
                authoring.Cone(
                    px + (i < 2 ? 5 : -5),
                    dungeonY + 11,
                    pz + 4,
                    2,
                    6 + (i & 1) * 2,
                    GameMaterialIds.Glass);
            }

            authoring.Box(
                new int3(puzzleCx - 14, dungeonY + 2, puzzleCz - 14),
                new int3(28, 3, 28),
                GameMaterialIds.Slate);
            authoring.Disc(
                puzzleCx,
                dungeonY + 5,
                puzzleCz,
                8,
                GameMaterialIds.DarkStone);
            authoring.Cone(
                puzzleCx,
                dungeonY + 6,
                puzzleCz,
                4,
                10,
                GameMaterialIds.Glass);
            authoring.Cone(
                puzzleCx - 6,
                dungeonY + 6,
                puzzleCz + 4,
                2,
                7,
                GameMaterialIds.Gold);

            int shrineX = puzzleMinX + 91;
            authoring.Box(
                new int3(shrineX - 5, dungeonY + 2, puzzleCz - 28),
                new int3(7, 30, 7),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(shrineX - 5, dungeonY + 2, puzzleCz + 21),
                new int3(7, 30, 7),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(shrineX - 6, dungeonY + 28, puzzleCz - 28),
                new int3(8, 6, 56),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(shrineX - 10, dungeonY + 3, puzzleCz - 12),
                new int3(10, 6, 24),
                GameMaterialIds.DarkStone);
            authoring.Cone(
                shrineX - 7,
                dungeonY + 9,
                puzzleCz,
                4,
                16,
                GameMaterialIds.Glass);

            for (int arch = 0; arch < 2; arch++)
            {
                int z = puzzleMinZ + 16 + arch * 84;
                authoring.Cylinder(
                    puzzleMinX + 12,
                    dungeonY + 2,
                    z,
                    7,
                    31,
                    GameMaterialIds.Stone);
                authoring.Cylinder(
                    puzzleMinX + 88,
                    dungeonY + 2,
                    z,
                    7,
                    31,
                    GameMaterialIds.Stone);
            }

            for (int x = puzzleMinX + 15; x < puzzleMinX + 92; x += 25)
                authoring.Box(
                    new int3(x, dungeonY + 32, puzzleMinZ + 5),
                    new int3(4, 4, 106),
                    GameMaterialIds.Wood);

            for (int side = -1; side <= 1; side += 2)
            {
                authoring.Box(
                    new int3(puzzleMinX + 48, dungeonY + 18, trapZ + side * 49 - 2),
                    new int3(4, 8, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(puzzleMinX + 48, dungeonY + 15, trapZ + side * 49 - 1),
                    new int3(6, 3, 3),
                    GameMaterialIds.Gold);
            }

            AuthorTreasury(authoring, tx, trapZ, dungeonY, corridorHalf, corridorHeight);
        }

        private static void AuthorTreasury(
            IStructureAuthoringSession authoring,
            int tx,
            int trapZ,
            int dungeonY,
            int corridorHalf,
            int corridorHeight)
        {
            int treasuryMinX = tx - 276;
            int treasuryMinZ = trapZ - 52;

            authoring.Box(
                new int3(tx - 188, dungeonY + 2, trapZ - corridorHalf),
                new int3(70, corridorHeight, corridorHalf * 2),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(tx - 188, dungeonY, trapZ - corridorHalf),
                new int3(70, 2, corridorHalf * 2),
                GameMaterialIds.DarkStone);
            authoring.FillBulk(
                new int3(treasuryMinX, dungeonY + 2, treasuryMinZ),
                new int3(100, 36, 104),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(treasuryMinX, dungeonY, treasuryMinZ),
                new int3(100, 2, 104),
                GameMaterialIds.DarkStone);

            for (int x = treasuryMinX + 12; x < treasuryMinX + 94; x += 24)
                authoring.Box(
                    new int3(x, dungeonY + 30, treasuryMinZ + 5),
                    new int3(5, 4, 94),
                    GameMaterialIds.Wood);

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = treasuryMinX + 18 + bay * 30;
                int z = trapZ + side * 45;
                authoring.Box(
                    new int3(x - 9, dungeonY + 2, z - 5),
                    new int3(18, 23, 10),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(x - 10, dungeonY + 9, z - 6),
                    new int3(20, 2, 12),
                    GameMaterialIds.Gold);
                authoring.Box(
                    new int3(x - 10, dungeonY + 18, z - 6),
                    new int3(20, 2, 12),
                    GameMaterialIds.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                int x = treasuryMinX + 24 + row * 27;
                int z = trapZ + side * 34;
                authoring.Box(
                    new int3(x - 8, dungeonY + 2, z - 7),
                    new int3(16, 10, 14),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(x - 9, dungeonY + 10, z - 8),
                    new int3(18, 3, 16),
                    GameMaterialIds.Gold);
            }

            authoring.Box(
                new int3(treasuryMinX + 18, dungeonY + 1, trapZ - 8),
                new int3(62, 1, 16),
                GameMaterialIds.Cloth);
            authoring.Box(
                new int3(treasuryMinX + 15, dungeonY + 2, treasuryMinZ + 12),
                new int3(70, 5, 12),
                GameMaterialIds.Gold);

            for (int pile = 0; pile < 5; pile++)
            {
                int px = treasuryMinX + 18 + pile * 16;
                int pz = treasuryMinZ + 21 + (pile & 1) * 7;
                authoring.Cone(
                    px,
                    dungeonY + 7,
                    pz,
                    5,
                    7 + (pile % 3) * 3,
                    GameMaterialIds.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                authoring.Box(
                    new int3(treasuryMinX + 48, dungeonY + 17, trapZ + side * 45 - 2),
                    new int3(4, 8, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(treasuryMinX + 48, dungeonY + 14, trapZ + side * 45 - 1),
                    new int3(6, 3, 3),
                    GameMaterialIds.Gold);
            }
        }
    }
}
