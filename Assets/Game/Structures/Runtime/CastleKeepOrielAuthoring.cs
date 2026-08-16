using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned two-storey timber oriel attached to the rear keep wall.</summary>
    public static class CastleKeepOrielAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 keepMin,
            int3 keepSize,
            int baseY)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            const int width = 44;
            const int depth = 22;
            int minX = plan.Centre.x + 18;
            int wallZ = keepMin.z + keepSize.z;
            int firstFloorY = baseY + plan.FloorHeight * 2;

            for (int x = 3; x < width - 2; x += 12)
                authoring.Box(
                    new int3(minX + x, firstFloorY - 13, wallZ + 2),
                    new int3(5, 13, 14),
                    GameMaterialIds.DarkStone);

            for (int storey = 0; storey < 2; storey++)
            {
                int y = firstFloorY + storey * plan.FloorHeight;
                authoring.Box(
                    new int3(minX, y, wallZ - 2),
                    new int3(width, 4, depth),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(minX, y + 4, wallZ + depth - 5),
                    new int3(width, plan.FloorHeight - 7, 4),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(minX, y + 4, wallZ),
                    new int3(4, plan.FloorHeight - 7, depth - 3),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(minX + width - 4, y + 4, wallZ),
                    new int3(4, plan.FloorHeight - 7, depth - 3),
                    GameMaterialIds.Wood);

                for (int bay = 0; bay < 3; bay++)
                {
                    int bayX = minX + 5 + bay * 13;
                    authoring.Box(
                        new int3(bayX, y + 9, wallZ + depth - 4),
                        new int3(9, plan.FloorHeight - 18, 3),
                        GameMaterialIds.LitWindow);
                }

                authoring.Box(
                    new int3(minX + 8, y + 4, wallZ - 8),
                    new int3(width - 16, 25, 12),
                    GameMaterialIds.Empty);
                authoring.Box(
                    new int3(minX + 4, y + 4, wallZ + 4),
                    new int3(width - 8, plan.FloorHeight - 8, depth - 9),
                    GameMaterialIds.Empty);
            }

            int roofY = firstFloorY + plan.FloorHeight * 2;
            authoring.Gable(
                new int3(minX - 4, roofY, wallZ - 4),
                new int3(width + 8, 24, depth + 8),
                true,
                GameMaterialIds.Tile);
            authoring.Box(
                new int3(minX - 3, firstFloorY + plan.FloorHeight - 1, wallZ - 1),
                new int3(width + 6, 3, depth + 1),
                GameMaterialIds.DarkStone);
        }
    }
}
