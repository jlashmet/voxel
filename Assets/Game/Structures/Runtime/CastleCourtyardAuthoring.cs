using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned courtyard paving, well, and outbuilding authoring.</summary>
    public static class CastleCourtyardAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(plan.Seed ^ 0xC0DEu);

            for (int z = -plan.BaileyHalfZ + 40; z < plan.BaileyHalfZ - 40; z++)
            for (int x = -plan.BaileyHalfX + 40; x < plan.BaileyHalfX - 40; x++)
            {
                byte material = rng.NextInt(0, 100) < 82
                    ? GameMaterialIds.Stone
                    : GameMaterialIds.Dirt;
                authoring.FillColumnBulk(
                    plan.Centre.x + x, baseY, baseY + 1,
                    plan.Centre.z + z, material);
            }

            int wellX = plan.Centre.x - plan.BaileyHalfX / 2;
            int wellZ = plan.Centre.z + plan.BaileyHalfZ / 3;
            authoring.Cylinder(wellX, baseY + 1, wellZ,
                16, 12, GameMaterialIds.DarkStone, 11);
            authoring.Cylinder(wellX, baseY - 60, wellZ,
                11, 60, GameMaterialIds.Empty);
            authoring.Cylinder(wellX, baseY - 60, wellZ,
                10, 14, GameMaterialIds.Water);

            for (int i = 0; i < 3; i++)
            {
                int bx = plan.Centre.x - plan.BaileyHalfX + 60 + i * 150;
                int bz = plan.Centre.z + plan.BaileyHalfZ - 130;
                int width = rng.NextInt(70, 100);
                int depth = rng.NextInt(60, 84);
                int height = rng.NextInt(56, 76);

                authoring.HollowBox(
                    new int3(bx, baseY, bz),
                    new int3(width, height, depth),
                    5,
                    GameMaterialIds.Stone,
                    false,
                    false);
                authoring.Box(
                    new int3(bx + width / 2 - 9, baseY, bz),
                    new int3(18, 30, 5),
                    GameMaterialIds.Empty);
                authoring.Gable(
                    new int3(bx - 4, baseY + height, bz - 4),
                    new int3(width + 8, 30, depth + 8),
                    true,
                    GameMaterialIds.Tile);
            }
        }
    }
}
