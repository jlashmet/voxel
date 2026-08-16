using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned battlements, main roof, dormers, belfry, and heraldic roof detail.</summary>
    public static class CastleKeepRooflineAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 size = CastleKeepCoreAuthoring.Size(in plan);
            int topY = baseY + plan.Floors * plan.FloorHeight;

            authoring.Box(
                new int3(min.x - 5, topY, min.z - 5),
                new int3(size.x + 10, 6, size.z + 10),
                GameMaterialIds.DarkStone);

            for (int i = 0; i < size.x + 10; i += 44)
            {
                authoring.Box(
                    new int3(min.x - 5 + i, topY + 6, min.z - 5),
                    new int3(24, 20, 7),
                    GameMaterialIds.Stone);
                authoring.Box(
                    new int3(min.x - 5 + i, topY + 6, min.z + size.z + 3),
                    new int3(24, 20, 7),
                    GameMaterialIds.Stone);
            }

            authoring.Gable(
                new int3(min.x, topY + 8, min.z),
                new int3(size.x, 70, size.z),
                true,
                GameMaterialIds.Tile);

            AuthorRooflineDetails(authoring, in plan, min, size, topY);
        }

        private static void AuthorRooflineDetails(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int topY)
        {
            int roofFrontZ = min.z - 2;

            for (int side = -1; side <= 1; side += 2)
            {
                int dormerX = plan.Centre.x + side * 52;
                authoring.Box(
                    new int3(dormerX - 12, topY + 25, roofFrontZ),
                    new int3(24, 25, 18),
                    GameMaterialIds.Stone);
                authoring.Arch(
                    new int3(dormerX - 6, topY + 32, roofFrontZ - 1),
                    12,
                    16,
                    4,
                    2,
                    GameMaterialIds.Empty);
                authoring.Box(
                    new int3(dormerX - 3, topY + 35, roofFrontZ),
                    new int3(6, 10, 2),
                    GameMaterialIds.LitWindow);
                authoring.Gable(
                    new int3(dormerX - 15, topY + 49, roofFrontZ - 4),
                    new int3(30, 20, 25),
                    true,
                    GameMaterialIds.Slate);
            }

            int lanternX = plan.Centre.x + size.x / 7;
            int lanternZ = min.z + size.z / 2;
            int lanternY = topY + 63;
            const int half = 24;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                authoring.Box(
                    new int3(lanternX + sx * half - 5, lanternY,
                             lanternZ + sz * half - 5),
                    new int3(10, 48, 10),
                    GameMaterialIds.Stone);

            authoring.Box(
                new int3(lanternX - half - 5, lanternY, lanternZ - half - 5),
                new int3(half * 2 + 10, 48, 8),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(lanternX - half - 5, lanternY, lanternZ + half - 3),
                new int3(half * 2 + 10, 48, 8),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(lanternX - half - 5, lanternY, lanternZ - half + 3),
                new int3(8, 48, half * 2 - 6),
                GameMaterialIds.Stone);
            authoring.Box(
                new int3(lanternX + half - 3, lanternY, lanternZ - half + 3),
                new int3(8, 48, half * 2 - 6),
                GameMaterialIds.Stone);

            authoring.Arch(
                new int3(lanternX - 13, lanternY + 7, lanternZ - half - 6),
                26,
                34,
                10,
                2,
                GameMaterialIds.Empty);
            authoring.Arch(
                new int3(lanternX - 13, lanternY + 7, lanternZ + half - 4),
                26,
                34,
                10,
                2,
                GameMaterialIds.Empty);
            authoring.Arch(
                new int3(lanternX - half - 6, lanternY + 7, lanternZ - 13),
                26,
                34,
                10,
                0,
                GameMaterialIds.Empty);
            authoring.Arch(
                new int3(lanternX + half - 4, lanternY + 7, lanternZ - 13),
                26,
                34,
                10,
                0,
                GameMaterialIds.Empty);

            authoring.Box(
                new int3(lanternX - half - 5, lanternY + 40, lanternZ - half - 5),
                new int3(half * 2 + 10, 9, 10),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(lanternX - half - 5, lanternY + 40, lanternZ + half - 5),
                new int3(half * 2 + 10, 9, 10),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(lanternX - half - 5, lanternY + 40, lanternZ - half + 5),
                new int3(10, 9, half * 2 - 10),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(lanternX + half - 5, lanternY + 40, lanternZ - half + 5),
                new int3(10, 9, half * 2 - 10),
                GameMaterialIds.DarkStone);

            authoring.Box(
                new int3(lanternX - half - 8, lanternY + 49, lanternZ - half - 8),
                new int3(half * 2 + 16, 7, half * 2 + 16),
                GameMaterialIds.DarkStone);

            for (int x = -half - 7; x <= half - 5; x += 18)
            {
                authoring.Box(
                    new int3(lanternX + x, lanternY + 56, lanternZ - half - 7),
                    new int3(11, 15, 8),
                    GameMaterialIds.Stone);
                authoring.Box(
                    new int3(lanternX + x, lanternY + 56, lanternZ + half - 1),
                    new int3(11, 15, 8),
                    GameMaterialIds.Stone);
            }

            for (int z = -half + 8; z <= half - 10; z += 18)
            {
                authoring.Box(
                    new int3(lanternX - half - 7, lanternY + 56, lanternZ + z),
                    new int3(8, 15, 11),
                    GameMaterialIds.Stone);
                authoring.Box(
                    new int3(lanternX + half - 1, lanternY + 56, lanternZ + z),
                    new int3(8, 15, 11),
                    GameMaterialIds.Stone);
            }

            authoring.Box(
                new int3(lanternX - 1, lanternY + 70, lanternZ - 1),
                new int3(3, 30, 3),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(lanternX + 2, lanternY + 86, lanternZ - 1),
                new int3(24, 11, 3),
                GameMaterialIds.Cloth);
        }
    }
}
