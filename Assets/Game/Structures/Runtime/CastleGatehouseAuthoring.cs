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
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            Author(authoring, in plan, in components.Gatehouse);
        }

        /// <summary>
        /// Transitional compatibility overload for branch callers that still pass the three shared
        /// gatehouse pieces independently. New authoring should use <see cref="CastleGatehouseConfig"/>
        /// so dimensions, portcullis clearance, flanking-tower placement, and the road anchor travel
        /// together as one validated castle composition.
        /// </summary>
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in TowerConfig gateTowers,
            in OpeningConfig mainGate,
            in BattlementConfig battlements)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            CastleGatehouseConfig gatehouse = components.Gatehouse;
            gatehouse.FlankingTowers = gateTowers;
            gatehouse.GateOpening = mainGate;
            gatehouse.PortcullisOpening.Width = mainGate.Width + 4;
            gatehouse.PortcullisOpening.Height = mainGate.Height + 14;
            gatehouse.PortcullisOpening.BottomOffset = math.min(0, mainGate.BottomOffset);
            gatehouse.Battlements = battlements;
            Author(authoring, in plan, in gatehouse);
        }

        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleGatehouseConfig gatehouse)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!gatehouse.IsWellFormed)
                throw new System.ArgumentException("Castle gatehouse configuration is invalid.");

            TowerConfig gateTowers = gatehouse.FlankingTowers;
            OpeningConfig mainGate = gatehouse.GateOpening;
            OpeningConfig portcullisOpening = gatehouse.PortcullisOpening;
            BattlementConfig battlements = gatehouse.Battlements;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            int gatehouseMinX = plan.Centre.x - gatehouse.Width / 2;
            int gatehouseMinZ = gateZ - gatehouse.Depth / 2;
            int radius = gateTowers.Radius;
            int spacing = gatehouse.TowerCentreOffset;

            var left = new int3(plan.Centre.x - spacing, baseY, gateZ);
            var right = new int3(plan.Centre.x + spacing, baseY, gateZ);

            authoring.Box(
                new int3(gatehouseMinX, baseY, gatehouseMinZ),
                new int3(gatehouse.Width, gatehouse.Height, gatehouse.Depth),
                GameMaterialIds.Stone);

            int leftHeight = gateTowers.Height + gatehouse.LeftTowerHeightOffset;
            int rightHeight = gateTowers.Height + gatehouse.RightTowerHeightOffset;
            CastleTowerAuthoring.AuthorTower(authoring, in plan, left, radius, leftHeight, false);
            CastleTowerAuthoring.AuthorTower(authoring, in plan, right, radius, rightHeight, false);
            if (gateTowers.OpeningsEnabled)
            {
                CastleTowerAuthoring.AuthorFrontWindows(
                    authoring, left, radius, leftHeight, plan.FloorHeight, in gateTowers.Opening);
                CastleTowerAuthoring.AuthorFrontWindows(
                    authoring, right, radius, rightHeight, plan.FloorHeight, in gateTowers.Opening);
            }

            authoring.Arch(
                new int3(plan.Centre.x - portcullisOpening.Width / 2,
                         baseY + portcullisOpening.BottomOffset,
                         gatehouseMinZ),
                portcullisOpening.Width,
                portcullisOpening.Height,
                gatehouse.Depth,
                2,
                GameMaterialIds.Empty);

            int3 gateMin = new(
                plan.Centre.x - mainGate.Width / 2,
                baseY + mainGate.BottomOffset,
                gatehouseMinZ + gatehouse.GateLeafInset);
            authoring.Arch(
                gateMin,
                mainGate.Width,
                mainGate.Height,
                gatehouse.GateLeafDepth,
                2,
                GameMaterialIds.Wood);
            for (int band = 0; band < 3; band++)
                authoring.Box(
                    new int3(gateMin.x + 2, gateMin.y + 10 + band * 13, gateMin.z),
                    new int3(mainGate.Width - 4, 3, gatehouse.GateLeafDepth),
                    GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(plan.Centre.x - 2, gateMin.y + 2, gateMin.z),
                new int3(4, 44, gatehouse.GateLeafDepth),
                GameMaterialIds.DarkStone);
            for (int side = -1; side <= 1; side += 2)
                authoring.Box(
                    new int3(plan.Centre.x + side * 8 - 2, gateMin.y + 23, gateMin.z),
                    new int3(4, 4, math.min(2, gatehouse.GateLeafDepth)),
                    GameMaterialIds.Gold);

            // The interaction clears only the four-voxel closed leaf above. Keep the physical
            // opened state behind it in the already-carved portcullis passage: while closed these
            // leaves are occluded by the front gate; after E removes that front leaf they read as
            // the same two doors swung inward rather than as a gate that simply vanished.
            AuthorOpenedGateLeaves(authoring, in plan, in gatehouse, in mainGate, gateMin);

            authoring.Box(
                new int3(plan.Centre.x - 28, baseY + 74, gateZ - 4),
                new int3(56, 6, 8),
                GameMaterialIds.Empty);

            for (int i = 0; i < 9; i++)
            {
                int x = plan.Centre.x - 36 + i * 9;
                authoring.Box(
                    new int3(x, baseY + gatehouse.Height - 16,
                             gatehouseMinZ - 6),
                    new int3(5, 14, 6),
                    GameMaterialIds.DarkStone);
            }

            // Preserve the legacy raw crenellation dimensions (8,18,18,12) while mapping them to
            // the shared contract by meaning: wall thickness, merlon height, merlon width, gap.
            authoring.Crenellate(
                new int3(gatehouseMinX, baseY + gatehouse.Height, gatehouseMinZ),
                new int3(1, 0, 0),
                gatehouse.Width,
                battlements.ParapetThickness,
                battlements.MerlonHeight,
                battlements.MerlonWidth,
                battlements.GapWidth,
                GameMaterialIds.Stone);

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 29;
                authoring.Box(
                    new int3(bannerX - 7, baseY + 52,
                             gatehouseMinZ - 2),
                    new int3(14, 42, 2),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(bannerX - 10, baseY + 92,
                             gatehouseMinZ - 3),
                    new int3(20, 3, 3),
                    GameMaterialIds.Gold);
            }

            int bridgeFarZ = gatehouseMinZ;
            int bridgeNearZ = bridgeFarZ - 149;
            for (int z = bridgeNearZ; z <= bridgeFarZ; z++)
            for (int x = -34; x <= 34; x++)
                authoring.FillColumnBulk(
                    plan.Centre.x + x,
                    baseY - 2,
                    baseY - 1,
                    z,
                    GameMaterialIds.Wood);

            int bridgeLength = bridgeFarZ - bridgeNearZ + 1;
            for (int side = -1; side <= 1; side += 2)
                authoring.Box(
                    new int3(plan.Centre.x + side * 25 - 4, baseY - 7, bridgeNearZ),
                    new int3(8, 5, bridgeLength),
                    GameMaterialIds.DarkStone);

            int riverZ = gatehouseMinZ - 92;
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
                    new int3(4, 4, bridgeLength),
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

        private static void AuthorOpenedGateLeaves(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleGatehouseConfig gatehouse,
            in OpeningConfig mainGate,
            int3 gateMin)
        {
            // A leaf is half the closed gate width. Swing it about seventy degrees into the
            // passage: most of its width becomes Z depth while only a small X projection remains,
            // which leaves a generous central player lane. Begin immediately behind the closed
            // leaf so TryOpenCastleFrontGate's four-deep clear cannot erase the open state.
            int availableDepth = gatehouse.Depth - gatehouse.GateLeafInset - gatehouse.GateLeafDepth - 2;
            int leafLength = math.min(mainGate.Width / 2 - 2, availableDepth);
            if (leafLength < 8) return;

            int half = mainGate.Width / 2;
            int archTop = mainGate.Height - half;
            int startZ = gateMin.z + gatehouse.GateLeafDepth;
            int maxInward = math.max(3, math.min(8, half - 6));

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < leafLength; i++)
                {
                    float t = leafLength <= 1 ? 0f : i / (float)(leafLength - 1);
                    int inward = (int)math.round(t * maxInward);
                    int x = side < 0
                        ? plan.Centre.x - half + inward
                        : plan.Centre.x + half - 2 - inward;
                    int z = startZ + i;

                    // Preserve the original arched silhouette on each half leaf: the hinge-side
                    // edge is lower and the free edge rises toward the crown.
                    int originalDx = (int)math.round(math.lerp(half - 1, 0, t));
                    int rise = (int)math.floor(math.sqrt(math.max(0,
                        half * half - originalDx * originalDx)));
                    int leafHeight = math.clamp(archTop + rise, 8, mainGate.Height);

                    authoring.Box(
                        new int3(x, gateMin.y, z),
                        new int3(2, leafHeight, 1),
                        GameMaterialIds.Wood);

                    // Iron straps make the state legible at the same material cadence as the
                    // closed gate, instead of leaving two featureless brown slabs in the tunnel.
                    for (int band = 0; band < 3; band++)
                    {
                        int bandY = 10 + band * 13;
                        if (bandY >= leafHeight) continue;
                        authoring.Box(
                            new int3(x, gateMin.y + bandY, z),
                            new int3(2, math.min(3, leafHeight - bandY), 1),
                            GameMaterialIds.DarkStone);
                    }
                }

                // Put the latch hardware near the free edge of each leaf so the opened state has
                // a readable front/back orientation rather than looking like arbitrary wall trim.
                int handleStep = math.max(0, leafLength - 3);
                float handleT = leafLength <= 1 ? 0f : handleStep / (float)(leafLength - 1);
                int handleInward = (int)math.round(handleT * maxInward);
                int handleX = side < 0
                    ? plan.Centre.x - half + handleInward
                    : plan.Centre.x + half - 2 - handleInward;
                authoring.Box(
                    new int3(handleX, gateMin.y + 23, startZ + handleStep),
                    new int3(2, 4, 2),
                    GameMaterialIds.Gold);
            }
        }
    }
}
