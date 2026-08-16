using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative world-voxel envelope for a frozen primary gatehouse recipe.
    /// Min is inclusive and MaxExclusive is exclusive.
    /// </summary>
    public readonly struct CastleGatehouseBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal CastleGatehouseBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Pure bounds resolver matching CastlePlannedGatehouseRealizer geometry without depending on
    /// Runtime. This keeps streaming dependencies tied to the frozen bridge/tower recipe.
    /// </summary>
    public static class CastleGatehouseBuildBoundsResolver
    {
        public static CastleGatehouseBuildBounds Resolve(
            in CastlePlan castle,
            in CastleGatePlacementSpec placement,
            in CastleGatehousePlan gatehouse)
        {
            if (!CastleGatehousePlanValidator.TryValidate(
                    in gatehouse, out CastleGatehousePlanIssue issue) ||
                !CastleGatehousePlanValidator.TryValidateTowerDetails(
                    in gatehouse, castle.FloorHeight, out issue))
            {
                throw new InvalidOperationException(
                    $"Castle gatehouse bounds require a valid frozen recipe: {issue}.");
            }

            CastleGateGeometry geometry = CastleGateGeometryResolver.Resolve(
                in castle, in placement);
            float2 gate = geometry.PerimeterCentre;
            float2 tangent = geometry.Tangent;
            float2 outward = geometry.Outward;
            int baseY = castle.Centre.y + castle.PlateauHeight;

            int2 left = Round(gate - tangent * gatehouse.TowerSpacing);
            int2 right = Round(gate + tangent * gatehouse.TowerSpacing);

            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            int towerReach = castle.GateTowerRadius + 4;
            IncludeDisc(left, towerReach, ref minX, ref minZ, ref maxX, ref maxZ);
            IncludeDisc(right, towerReach, ref minX, ref minZ, ref maxX, ref maxZ);

            // The upper masonry span uses thickness = WallThickness * 2, so its rasterized
            // capsule radius is exactly WallThickness.
            IncludeCapsule(
                new float2(left.x, left.y),
                new float2(right.x, right.y),
                castle.WallThickness,
                ref minX, ref minZ, ref maxX, ref maxZ);

            // Gate leaf occupies the resolver's tangent/outward basis from Origin.
            float2 gateOrigin = new float2(geometry.Origin.x, geometry.Origin.z);
            IncludePoint(gateOrigin, ref minX, ref minZ, ref maxX, ref maxZ);
            IncludePoint(
                gateOrigin + geometry.Tangent * (geometry.Width - 1),
                ref minX, ref minZ, ref maxX, ref maxZ);
            IncludePoint(
                gateOrigin - geometry.Outward * (geometry.Depth - 1),
                ref minX, ref minZ, ref maxX, ref maxZ);
            IncludePoint(
                gateOrigin + geometry.Tangent * (geometry.Width - 1)
                           - geometry.Outward * (geometry.Depth - 1),
                ref minX, ref minZ, ref maxX, ref maxZ);

            float2 bridgeNear = gate + outward * gatehouse.BridgeNearDistance;
            float2 bridgeFar = bridgeNear + outward * gatehouse.BridgeLength;
            float bridgeHalfWidth = math.max(
                gatehouse.BridgeWidth * 0.5f,
                gatehouse.BridgeSupportOffset +
                    math.max(
                        gatehouse.BridgeSupportThickness,
                        gatehouse.BridgeRailThickness) * 0.5f);
            IncludeCapsule(
                bridgeNear,
                bridgeFar,
                bridgeHalfWidth,
                ref minX, ref minZ, ref maxX, ref maxZ);

            int minY = math.min(
                baseY - 30,
                math.min(
                    baseY + gatehouse.BridgeDeckYOffset,
                    math.min(
                        baseY + gatehouse.BridgeSupportYOffset,
                        baseY + gatehouse.BridgeRailYOffset)));
            int maxY = baseY + math.max(
                math.max(gatehouse.LeftTowerHeight, gatehouse.RightTowerHeight) + 23,
                math.max(
                    gatehouse.BlockHeight + 19,
                    math.max(
                        gatehouse.BridgeDeckYOffset + gatehouse.BridgeDeckHeight - 1,
                        math.max(
                            gatehouse.BridgeSupportYOffset + gatehouse.BridgeSupportHeight - 1,
                            math.max(
                                gatehouse.BridgeRailYOffset + gatehouse.BridgeRailHeight - 1,
                                CastleLayout.FrontGateHeight)))));

            return new CastleGatehouseBuildBounds(
                new int3(minX, minY, minZ),
                new int3(maxX + 1, maxY + 1, maxZ + 1));
        }

        private static void IncludeDisc(
            int2 centre,
            int radius,
            ref int minX,
            ref int minZ,
            ref int maxX,
            ref int maxZ)
        {
            minX = math.min(minX, centre.x - radius);
            minZ = math.min(minZ, centre.y - radius);
            maxX = math.max(maxX, centre.x + radius);
            maxZ = math.max(maxZ, centre.y + radius);
        }

        private static void IncludeCapsule(
            float2 start,
            float2 end,
            float radius,
            ref int minX,
            ref int minZ,
            ref int maxX,
            ref int maxZ)
        {
            minX = math.min(minX, (int)math.floor(math.min(start.x, end.x) - radius));
            minZ = math.min(minZ, (int)math.floor(math.min(start.y, end.y) - radius));
            maxX = math.max(maxX, (int)math.ceil(math.max(start.x, end.x) + radius));
            maxZ = math.max(maxZ, (int)math.ceil(math.max(start.y, end.y) + radius));
        }

        private static void IncludePoint(
            float2 point,
            ref int minX,
            ref int minZ,
            ref int maxX,
            ref int maxZ)
        {
            int x0 = (int)math.floor(point.x);
            int x1 = (int)math.ceil(point.x);
            int z0 = (int)math.floor(point.y);
            int z1 = (int)math.ceil(point.y);
            minX = math.min(minX, x0);
            minZ = math.min(minZ, z0);
            maxX = math.max(maxX, x1);
            maxZ = math.max(maxZ, z1);
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}
