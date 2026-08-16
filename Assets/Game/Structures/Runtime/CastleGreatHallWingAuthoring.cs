using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned occupied great-hall/solar wing attached to the east side of the keep.</summary>
    public static class CastleGreatHallWingAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 keepMin = CastleKeepCoreAuthoring.Minimum(in plan);
            int3 keepSize = CastleKeepCoreAuthoring.Size(in plan);

            int wingHeight = plan.FloorHeight * 2;
            int wingWidth = math.max(96, keepSize.x * 2 / 5);
            int wingDepth = math.max(80, keepSize.z - 72);
            var wingMin = new int3(
                keepMin.x + keepSize.x - 4,
                baseY,
                keepMin.z + 24);

            authoring.Box(
                new int3(wingMin.x - 4, baseY - 12, wingMin.z - 4),
                new int3(wingWidth + 8, 16, wingDepth + 8),
                GameMaterialIds.DarkStone);
            authoring.HollowBox(
                wingMin,
                new int3(wingWidth, wingHeight, wingDepth),
                6,
                GameMaterialIds.Stone,
                false,
                false);
            authoring.FillBulk(
                new int3(wingMin.x + 6, baseY + 1, wingMin.z + 6),
                new int3(wingWidth - 12, wingHeight - 1, wingDepth - 12),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(wingMin.x + 6, baseY + plan.FloorHeight, wingMin.z + 6),
                new int3(wingWidth - 12, 3, wingDepth - 12),
                GameMaterialIds.Wood);

            int hallCentreZ = wingMin.z + wingDepth / 2;
            for (int side = -1; side <= 1; side += 2)
            {
                int tableZ = hallCentreZ + side * 25;
                authoring.Box(
                    new int3(wingMin.x + 22, baseY + 7, tableZ - 5),
                    new int3(wingWidth - 46, 4, 10),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(wingMin.x + 27, baseY + 2, tableZ - 3),
                    new int3(4, 6, 6),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(wingMin.x + wingWidth - 31, baseY + 2, tableZ - 3),
                    new int3(4, 6, 6),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(wingMin.x + 20, baseY + 2, tableZ + side * 9 - 2),
                    new int3(wingWidth - 42, 4, 4),
                    GameMaterialIds.Wood);
            }

            authoring.Box(
                new int3(wingMin.x + wingWidth - 20, baseY + 2, hallCentreZ - 17),
                new int3(8, 4, 34),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(wingMin.x + wingWidth - 17, baseY + 6, hallCentreZ - 8),
                new int3(5, 14, 16),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + wingWidth - 16, baseY + 12, hallCentreZ - 6),
                new int3(4, 8, 12),
                GameMaterialIds.Cloth);

            int upperY = baseY + plan.FloorHeight;
            for (int z = wingMin.z + 12; z < wingMin.z + wingDepth - 18; z += 28)
            {
                authoring.Box(
                    new int3(wingMin.x + wingWidth - 18, upperY + 3, z),
                    new int3(10, 28, 18),
                    GameMaterialIds.Wood);
                for (int shelf = 0; shelf < 3; shelf++)
                    authoring.Box(
                        new int3(
                            wingMin.x + wingWidth - 19,
                            upperY + 9 + shelf * 8,
                            z - 1),
                        new int3(12, 2, 20),
                        shelf == 1 ? GameMaterialIds.Gold : GameMaterialIds.Wood);
            }

            authoring.Box(
                new int3(wingMin.x + 28, upperY + 8, hallCentreZ - 12),
                new int3(34, 4, 24),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + 32, upperY + 3, hallCentreZ - 8),
                new int3(5, 6, 5),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + 53, upperY + 3, hallCentreZ + 3),
                new int3(5, 6, 5),
                GameMaterialIds.Wood);

            for (int floor = 0; floor < 2; floor++)
            for (int side = -1; side <= 1; side += 2)
            {
                int lampY = baseY + floor * plan.FloorHeight + 17;
                int lampZ = hallCentreZ + side * (wingDepth / 2 - 13);
                authoring.Box(
                    new int3(wingMin.x + wingWidth / 2 - 2, lampY, lampZ - 2),
                    new int3(4, 7, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(wingMin.x + wingWidth / 2 - 3, lampY - 3, lampZ - 1),
                    new int3(6, 3, 3),
                    GameMaterialIds.Gold);
            }

            for (int i = 0; i < 2; i++)
            {
                int z = wingMin.z + 14 + i * (wingDepth - 28);
                authoring.Arch(
                    new int3(wingMin.x + wingWidth - 7, baseY + 12, z),
                    16,
                    28,
                    8,
                    0,
                    GameMaterialIds.Empty);
                authoring.Box(
                    new int3(wingMin.x + wingWidth - 5, baseY + 16, z + 3),
                    new int3(2, 18, 10),
                    GameMaterialIds.LitWindow);
            }

            authoring.Arch(
                new int3(wingMin.x - 8, baseY + 2, wingMin.z + wingDepth / 2 - 10),
                20,
                32,
                16,
                0,
                GameMaterialIds.Empty);
            authoring.Arch(
                new int3(
                    wingMin.x - 8,
                    baseY + plan.FloorHeight + 2,
                    wingMin.z + wingDepth / 2 - 10),
                20,
                30,
                16,
                0,
                GameMaterialIds.Empty);

            int connectorZ = wingMin.z + wingDepth / 2;
            for (int floor = 0; floor < 2; floor++)
            {
                int floorY = baseY + floor * plan.FloorHeight;
                authoring.Box(
                    new int3(keepMin.x + keepSize.x - 12, floorY, connectorZ - 7),
                    new int3(24, floor == 0 ? 2 : 3, 14),
                    floor == 0 ? GameMaterialIds.Stone : GameMaterialIds.Wood);

                int footY = floorY + (floor == 0 ? 2 : 3);
                authoring.Box(
                    new int3(keepMin.x + keepSize.x - 12, footY, connectorZ - 7),
                    new int3(24, 24, 14),
                    GameMaterialIds.Empty);
            }

            authoring.Gable(
                new int3(wingMin.x - 4, baseY + wingHeight, wingMin.z - 4),
                new int3(wingWidth + 8, 34, wingDepth + 8),
                true,
                GameMaterialIds.Tile);

            int balconyY = baseY + plan.FloorHeight + 4;
            int balconyZ = wingMin.z + wingDepth / 2 - 25;
            authoring.Box(
                new int3(wingMin.x + wingWidth - 2, balconyY, balconyZ),
                new int3(18, 4, 50),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ),
                new int3(3, 18, 3),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + wingWidth + 12, balconyY + 4, balconyZ + 47),
                new int3(3, 18, 3),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(wingMin.x + wingWidth + 12, balconyY + 18, balconyZ),
                new int3(3, 3, 50),
                GameMaterialIds.Wood);
        }
    }
}
