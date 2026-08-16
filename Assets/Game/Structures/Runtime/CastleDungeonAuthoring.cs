using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Game-owned cellar, main dungeon hall, trapdoor circulation, and secret passage authoring.
    /// Side chambers and natural caves are delegated to focused game-content passes.
    /// </summary>
    public static class CastleDungeonAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;

            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var keepMin = new int3(
                plan.Centre.x - hx,
                baseY,
                plan.Centre.z - hz + 60);

            authoring.FillBulk(
                new int3(keepMin.x + 10, cellarY, keepMin.z + 10),
                new int3(hx * 2 - 20, 40, hz * 2 - 20),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(keepMin.x + 8, cellarY - 4, keepMin.z + 8),
                new int3(hx * 2 - 16, 4, hz * 2 - 16),
                GameMaterialIds.DarkStone);

            AuthorArchive(authoring, in plan, keepMin, cellarY, hx, hz);

            int3 trapdoor = CastleLayout.TrapdoorCentre(in plan);
            int tx = trapdoor.x;
            int tz = trapdoor.z;

            authoring.Box(
                new int3(tx - 10, cellarY + 40, tz - 10),
                new int3(20, 8, 20),
                GameMaterialIds.Empty);
            authoring.SpiralStair(
                tx,
                cellarY,
                tz,
                9,
                46,
                GameMaterialIds.Stone);

            authoring.Box(
                new int3(
                    tx - CastleLayout.TrapdoorHalfSize,
                    baseY,
                    tz - CastleLayout.TrapdoorHalfSize),
                new int3(
                    CastleLayout.TrapdoorHalfSize * 2,
                    2,
                    CastleLayout.TrapdoorHalfSize * 2),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(
                    tx - CastleLayout.TrapdoorHalfSize,
                    baseY + 2,
                    tz - CastleLayout.TrapdoorHalfSize),
                new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(
                    tx + CastleLayout.TrapdoorHalfSize - 3,
                    baseY + 2,
                    tz - CastleLayout.TrapdoorHalfSize),
                new int3(3, 2, CastleLayout.TrapdoorHalfSize * 2),
                GameMaterialIds.Gold);

            authoring.Cylinder(
                tx,
                dungeonY,
                tz,
                16,
                cellarY - dungeonY,
                GameMaterialIds.Empty);
            authoring.SpiralStair(
                tx,
                dungeonY,
                tz,
                13,
                cellarY - dungeonY,
                GameMaterialIds.Stone);

            var hallMin = new int3(tx - 130, dungeonY, tz - 90);
            authoring.FillBulk(
                hallMin,
                new int3(260, 46, 180),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(hallMin.x - 6, dungeonY - 5, hallMin.z - 6),
                new int3(272, 5, 192),
                GameMaterialIds.DarkStone);

            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 2; j++)
            {
                int px = hallMin.x + 50 + i * 80;
                int pz = hallMin.z + 55 + j * 70;
                authoring.Cylinder(
                    px,
                    dungeonY,
                    pz,
                    12,
                    46,
                    GameMaterialIds.Stone);
                authoring.Cylinder(
                    px,
                    dungeonY + 42,
                    pz,
                    15,
                    4,
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(px - 2, dungeonY + 23, pz - 14),
                    new int3(4, 8, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(px - 2, dungeonY + 20, pz - 13),
                    new int3(4, 3, 3),
                    GameMaterialIds.Gold);
            }

            authoring.Box(
                new int3(tx - 34, dungeonY, hallMin.z + 18),
                new int3(68, 5, 26),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(tx - 12, dungeonY + 5, hallMin.z + 24),
                new int3(24, 9, 14),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(tx - 4, dungeonY + 14, hallMin.z + 28),
                new int3(8, 12, 6),
                GameMaterialIds.Gold);
            for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
                authoring.Box(
                    new int3(
                        tx + side * 54 - 20,
                        dungeonY + 1,
                        hallMin.z + 76 + row * 28),
                    new int3(40, 5, 8),
                    row == 1 ? GameMaterialIds.DarkStone : GameMaterialIds.Wood);

            CastleDungeonSideChambers.Author(authoring, tx, tz, dungeonY);

            int passZ = hallMin.z - 1;
            for (int i = 0; i < 320; i++)
            {
                int z = passZ - i;
                int y = dungeonY + (int)math.round(math.sin(i * 0.02f) * 8f);
                for (int x = tx - 14; x < tx + 14; x++)
                    authoring.FillColumnBulk(
                        x,
                        y,
                        y + 32,
                        z,
                        GameMaterialIds.Empty);
                authoring.Box(
                    new int3(tx - 16, y - 2, z),
                    new int3(32, 2, 1),
                    GameMaterialIds.DarkStone);
            }

            CastleCaveAuthoring.Author(
                authoring,
                in plan,
                new int3(tx, dungeonY, passZ - 320));
        }

        private static void AuthorArchive(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 keepMin,
            int cellarY,
            int hx,
            int hz)
        {
            for (int z = keepMin.z + 18; z < keepMin.z + hz * 2 - 30; z += 30)
            {
                authoring.Box(
                    new int3(keepMin.x + 14, cellarY, z),
                    new int3(12, 28, 20),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(keepMin.x + hx * 2 - 26, cellarY, z),
                    new int3(12, 28, 20),
                    GameMaterialIds.Wood);

                for (int shelf = 0; shelf < 3; shelf++)
                for (int book = 0; book < 5; book++)
                {
                    int bookZ = z + 2 + book * 3;
                    int bookY = cellarY + 5 + shelf * 8;
                    int bookHeight = 4 + ((book + shelf * 2 + z) & 3);
                    byte bookMaterial = ((book + shelf) & 2) == 0
                        ? GameMaterialIds.Cloth
                        : GameMaterialIds.Gold;
                    authoring.Box(
                        new int3(keepMin.x + 25, bookY, bookZ),
                        new int3(3, bookHeight, 2),
                        bookMaterial);
                    authoring.Box(
                        new int3(keepMin.x + hx * 2 - 28, bookY, bookZ),
                        new int3(3, bookHeight, 2),
                        bookMaterial);
                }
            }

            for (int beamZ = keepMin.z + 18; beamZ < keepMin.z + hz * 2 - 20; beamZ += 38)
                authoring.Box(
                    new int3(keepMin.x + 10, cellarY + 34, beamZ),
                    new int3(hx * 2 - 20, 4, 4),
                    GameMaterialIds.Wood);
            authoring.Box(
                new int3(plan.Centre.x - 12, cellarY, keepMin.z + 18),
                new int3(24, 1, hz * 2 - 42),
                GameMaterialIds.Cloth);

            int archiveDeskX = plan.Centre.x - 55;
            int archiveDeskZ = keepMin.z + hz;
            authoring.Box(
                new int3(archiveDeskX - 18, cellarY + 8, archiveDeskZ - 10),
                new int3(36, 3, 20),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(archiveDeskX - 14, cellarY + 1, archiveDeskZ - 7),
                new int3(5, 7, 5),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(archiveDeskX + 9, cellarY + 1, archiveDeskZ + 2),
                new int3(5, 7, 5),
                GameMaterialIds.Wood);

            for (int folio = 0; folio < 3; folio++)
                authoring.Box(
                    new int3(
                        archiveDeskX - 10 + folio * 8,
                        cellarY + 11 + folio,
                        archiveDeskZ - 4),
                    new int3(7, 1, 10),
                    folio == 1 ? GameMaterialIds.Gold : GameMaterialIds.Cloth);

            authoring.Box(
                new int3(archiveDeskX + 23, cellarY + 4, archiveDeskZ - 5),
                new int3(9, 4, 10),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(archiveDeskX + 29, cellarY + 8, archiveDeskZ - 5),
                new int3(3, 12, 10),
                GameMaterialIds.Wood);

            for (int side = -1; side <= 1; side += 2)
            {
                int lampX = plan.Centre.x + side * 55;
                authoring.Box(
                    new int3(lampX - 2, cellarY + 17, keepMin.z + hz - 2),
                    new int3(4, 8, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(lampX - 3, cellarY + 14, keepMin.z + hz - 1),
                    new int3(6, 3, 3),
                    GameMaterialIds.Gold);
            }

            authoring.Box(
                new int3(keepMin.x + 38, cellarY + 1, keepMin.z + 24),
                new int3(28, 10, 18),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(keepMin.x + 42, cellarY + 11, keepMin.z + 28),
                new int3(20, 4, 10),
                GameMaterialIds.Gold);

            for (int i = 0; i < 4; i++)
            {
                int bx = keepMin.x + hx * 2 - 42 - (i & 1) * 18;
                int bz = keepMin.z + 24 + (i >> 1) * 22;
                authoring.Cylinder(
                    bx,
                    cellarY,
                    bz,
                    6,
                    12,
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(bx - 5, cellarY + 5, bz - 7),
                    new int3(10, 2, 14),
                    GameMaterialIds.Gold);
            }
        }
    }
}
