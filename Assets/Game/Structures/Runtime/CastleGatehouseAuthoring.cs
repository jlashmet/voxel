using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned gatehouse, gate, bridge, and defended approach authoring.</summary>
    public static class CastleGatehouseAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int radius = plan.GateTowerRadius;
            const int spacing = 54;

            var left = new int3(plan.Centre.x - spacing, baseY, gateZ);
            var right = new int3(plan.Centre.x + spacing, baseY, gateZ);

            int blockHeight = plan.WallHeight + 22;
            authoring.Box(
                new int3(plan.Centre.x - spacing, baseY, gateZ - plan.WallThickness),
                new int3(spacing * 2, blockHeight, plan.WallThickness * 2),
                GameMaterialIds.Stone);

            int leftHeight = plan.GateTowerHeight + 38;
            int rightHeight = plan.GateTowerHeight + 12;
            CastleTowerAuthoring.AuthorTower(authoring, in plan, left, radius, leftHeight, false);
            CastleTowerAuthoring.AuthorTower(authoring, in plan, right, radius, rightHeight, false);
            CastleTowerAuthoring.AuthorFrontWindows(
                authoring, left, radius, leftHeight, plan.FloorHeight);
            CastleTowerAuthoring.AuthorFrontWindows(
                authoring, right, radius, rightHeight, plan.FloorHeight);

            authoring.Arch(
                new int3(plan.Centre.x - 26, baseY, gateZ - plan.WallThickness),
                52, 74, plan.WallThickness * 2, 2, GameMaterialIds.Empty);

            int3 gateMin = CastleLayout.FrontGateMinimum(in plan);
            authoring.Arch(
                gateMin,
                CastleLayout.FrontGateWidth,
                CastleLayout.FrontGateHeight,
                CastleLayout.FrontGateDepth,
                2,
                GameMaterialIds.Wood);
            for (int band = 0; band < 3; band++)
                authoring.Box(
                    new int3(gateMin.x + 2, gateMin.y + 10 + band * 13, gateMin.z),
                    new int3(CastleLayout.FrontGateWidth - 4, 3, CastleLayout.FrontGateDepth),
                    GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(plan.Centre.x - 2, gateMin.y + 2, gateMin.z),
                new int3(4, 44, CastleLayout.FrontGateDepth),
                GameMaterialIds.DarkStone);
            for (int side = -1; side <= 1; side += 2)
                authoring.Box(
                    new int3(plan.Centre.x + side * 8 - 2, gateMin.y + 23, gateMin.z),
                    new int3(4, 4, 2),
                    GameMaterialIds.Gold);

            authoring.Box(
                new int3(plan.Centre.x - 28, baseY + 74, gateZ - 4),
                new int3(56, 6, 8),
                GameMaterialIds.Empty);

            for (int i = 0; i < 9; i++)
            {
                int x = plan.Centre.x - 36 + i * 9;
                authoring.Box(
                    new int3(x, baseY + plan.WallHeight + 6,
                             gateZ - plan.WallThickness - 6),
                    new int3(5, 14, 6),
                    GameMaterialIds.DarkStone);
            }

            authoring.Crenellate(
                new int3(plan.Centre.x - spacing, baseY + blockHeight,
                         gateZ - plan.WallThickness),
                new int3(1, 0, 0),
                spacing * 2,
                8,
                18,
                18,
                12,
                GameMaterialIds.Stone);

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 29;
                authoring.Box(
                    new int3(bannerX - 7, baseY + 52,
                             gateZ - plan.WallThickness - 2),
                    new int3(14, 42, 2),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(bannerX - 10, baseY + 92,
                             gateZ - plan.WallThickness - 3),
                    new int3(20, 3, 3),
                    GameMaterialIds.Gold);
            }

            for (int z = 0; z < 150; z++)
            for (int x = -34; x <= 34; x++)
                authoring.FillColumnBulk(
                    plan.Centre.x + x,
                    baseY - 2,
                    baseY - 1,
                    gateZ - plan.WallThickness - z,
                    GameMaterialIds.Wood);

            int bridgeNearZ = gateZ - plan.WallThickness - 149;
            int bridgeFarZ = gateZ - plan.WallThickness;
            for (int side = -1; side <= 1; side += 2)
                authoring.Box(
                    new int3(plan.Centre.x + side * 25 - 4, baseY - 7, bridgeNearZ),
                    new int3(8, 5, 150),
                    GameMaterialIds.DarkStone);

            int riverZ = gateZ - plan.WallThickness - 92;
            int riverY = baseY - CastleLayout.LowerRiverDepth;
            int[] pierOffsets = { -27, 0, 27 };
            for (int pier = 0; pier < pierOffsets.Length; pier++)
            for (int side = -1; side <= 1; side += 2)
            {
                int pierZ = riverZ + pierOffsets[pier];
                authoring.Box(
                    new int3(plan.Centre.x + side * 24 - 6, riverY - 2, pierZ - 6),
                    new int3(12, baseY - riverY - 5, 12),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(plan.Centre.x + side * 24 - 9, baseY - 12, pierZ - 8),
                    new int3(18, 6, 16),
                    GameMaterialIds.Stone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int railX = plan.Centre.x + side * 32;
                authoring.Box(
                    new int3(railX - 2, baseY + 8, bridgeNearZ),
                    new int3(4, 4, 150),
                    GameMaterialIds.Wood);
                for (int z = bridgeNearZ; z <= bridgeFarZ; z += 24)
                    authoring.Box(
                        new int3(railX - 3, baseY - 1, z),
                        new int3(6, 17, 6),
                        GameMaterialIds.Wood);
            }

            authoring.Box(
                new int3(plan.Centre.x - 42, baseY - 12, bridgeNearZ - 8),
                new int3(84, 12, 14),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(plan.Centre.x - 40, baseY - 5, bridgeFarZ - 5),
                new int3(80, 7, 12),
                GameMaterialIds.Stone);
        }
    }
}
