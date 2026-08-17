using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned defensive tower vocabulary built from generic authoring primitives.</summary>
    public static class CastleTowerAuthoring
    {
        public static void AuthorCornerTowers(
            IStructureAuthoringSession authoring,
            in CastlePlan plan)
        {
            CastleComponentConfig config = CastleCompatibilityComponents.Resolve(in plan);
            AuthorCornerTowers(authoring, in plan, in config.CornerTowers);
        }

        public static void AuthorCornerTowers(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in TowerConfig towers)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!towers.IsWellFormed || towers.Shape != StructureTowerShape.Round)
                throw new System.ArgumentException("Castle tower configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.BaileyHalfX;
            int hz = plan.BaileyHalfZ;
            int3[] corners =
            {
                new(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z - hz),
                new(plan.Centre.x - hx, baseY, plan.Centre.z + hz),
                new(plan.Centre.x + hx, baseY, plan.Centre.z + hz),
            };

            int count = math.min(towers.Count, corners.Length);
            for (int i = 0; i < count; i++)
            {
                int heightVariation = i == 0 ? 58 : i == 1 ? 8 : i == 2 ? 30 : 14;
                int towerHeight = towers.Height + heightVariation;
                AuthorTower(authoring, in plan, corners[i], towers.Radius,
                    towerHeight, roof: i >= 2);
                if (i < 2 && towers.OpeningsEnabled)
                    AuthorFrontWindows(authoring, corners[i], towers.Radius,
                        towerHeight, plan.FloorHeight, in towers.Opening);
            }
        }

        public static void AuthorTower(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 at,
            int radius,
            int height,
            bool roof)
        {
            authoring.Cylinder(at.x, at.y - 30, at.z,
                radius + 4, 42, GameMaterialIds.DarkStone);
            authoring.Cylinder(at.x, at.y, at.z,
                radius, height, GameMaterialIds.Stone, radius - 12);

            for (int floor = 1; floor * plan.FloorHeight < height - 20; floor++)
                authoring.Disc(at.x, at.y + floor * plan.FloorHeight, at.z,
                    radius - 13, GameMaterialIds.Wood);

            authoring.SpiralStair(at.x, at.y + 2, at.z,
                radius - 14, height - 24, GameMaterialIds.Stone);

            for (int y = at.y + plan.FloorHeight; y < at.y + height - 28;
                 y += plan.FloorHeight)
            {
                authoring.Cylinder(at.x, y - 2, at.z,
                    radius + 2, 3, GameMaterialIds.DarkStone, radius - 1);
            }

            CarveTowerDoor(authoring, in plan, at, radius);

            var rng = new Random((uint)(at.x * 8191 + at.z * 131071) | 1u);
            for (int floor = 0; floor * plan.FloorHeight < height - 40; floor++)
            {
                int y = at.y + floor * plan.FloorHeight + 18;
                float phase = rng.NextFloat(0f, 6.28f);

                for (int slit = 0; slit < 3; slit++)
                {
                    float angle = phase + slit * 2.09f;
                    for (int r = radius - 14; r <= radius; r++)
                    for (int h = 0; h < 22; h++)
                    {
                        int x = at.x + (int)math.round(math.cos(angle) * r);
                        int z = at.z + (int)math.round(math.sin(angle) * r);
                        authoring.Set(x, y + h, z, GameMaterialIds.Empty);
                    }
                }
            }

            int parapetY = at.y + height;
            authoring.Cylinder(at.x, parapetY - 4, at.z,
                radius + 3, 5, GameMaterialIds.DarkStone, radius - 14);
            authoring.Cylinder(at.x, parapetY, at.z,
                radius + 2, 6, GameMaterialIds.Stone, radius - 12);
            authoring.CrenellateRing(at.x, parapetY + 6, at.z,
                radius + 2, 18, GameMaterialIds.Stone);

            if (!roof) return;

            authoring.Cone(at.x, parapetY + 8, at.z,
                radius - 4, radius * 2, GameMaterialIds.Slate);
            int peakY = parapetY + 8 + radius * 2;
            authoring.Box(new int3(at.x, peakY, at.z),
                new int3(2, 30, 2), GameMaterialIds.Wood);
            authoring.Box(new int3(at.x + 2, peakY + 17, at.z),
                new int3(22, 11, 2), GameMaterialIds.Cloth);
            authoring.Set(at.x, peakY + 30, at.z, GameMaterialIds.Gold);
        }

        public static void AuthorFrontWindows(
            IStructureAuthoringSession authoring,
            int3 at,
            int radius,
            int height,
            int floorHeight)
        {
            var opening = new OpeningConfig
            {
                Kind = StructureOpeningKind.Window,
                Width = 14,
                Height = 24,
                BottomOffset = 9,
                FrameThickness = 3,
            };
            AuthorFrontWindows(authoring, at, radius, height, floorHeight, in opening);
        }

        public static void AuthorFrontWindows(
            IStructureAuthoringSession authoring,
            int3 at,
            int radius,
            int height,
            int floorHeight,
            in OpeningConfig opening)
        {
            int width = opening.Width;
            int windowHeight = opening.Height;
            int frame = math.max(0, opening.FrameThickness);
            int frontZ = at.z - radius - 2;

            for (int floor = 1; floor * floorHeight + windowHeight + 12 < height; floor++)
            {
                int y = at.y + floor * floorHeight + opening.BottomOffset;
                authoring.Arch(new int3(at.x - width / 2 - frame, y - frame, frontZ - 3),
                    width + frame * 2, windowHeight + frame * 2, 5, 2, GameMaterialIds.DarkStone);
                authoring.Arch(new int3(at.x - width / 2, y, frontZ - 4),
                    width, windowHeight, 20, 2, GameMaterialIds.Empty);
                authoring.Arch(new int3(at.x - width / 2 + 3, y + 3, frontZ + 2),
                    width - 6, windowHeight - 7, 2, 2, GameMaterialIds.LitWindow);
                authoring.Box(new int3(at.x - 1, y + 4, frontZ + 1),
                    new int3(2, windowHeight - 10, 3), GameMaterialIds.DarkStone);
                authoring.Box(new int3(at.x - width / 2 + 3, y + windowHeight / 2, frontZ + 1),
                    new int3(width - 6, 2, 3), GameMaterialIds.DarkStone);
                authoring.Box(new int3(at.x - width / 2 - 4, y - 4, frontZ - 4),
                    new int3(width + 8, 3, 6), GameMaterialIds.DarkStone);
            }
        }

        private static void CarveTowerDoor(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 at,
            int radius)
        {
            const int width = 14;
            const int height = 30;
            int dx = plan.Centre.x - at.x;
            int dz = plan.Centre.z - at.z;

            if (math.abs(dx) > math.abs(dz))
            {
                int minX = dx >= 0 ? at.x + radius - 15 : at.x - radius - 1;
                authoring.Arch(new int3(minX, at.y + 2, at.z - width / 2),
                    width, height, 16, 0, GameMaterialIds.Empty);
            }
            else
            {
                int minZ = dz >= 0 ? at.z + radius - 15 : at.z - radius - 1;
                authoring.Arch(new int3(at.x - width / 2, at.y + 2, minZ),
                    width, height, 16, 2, GameMaterialIds.Empty);
            }
        }
    }
}
