using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes a primary gatehouse from frozen planning data. Gate position/orientation comes from
    /// CastleGatePlacementSpec; all gatehouse and bridge dimensions come from CastleGatehousePlan.
    /// This component makes no seed/RNG or semantic placement decisions during voxel mutation.
    /// </summary>
    public static class CastlePlannedGatehouseRealizer
    {
        public static void Build(
            ref VoxelBrush brush,
            in CastlePlan castle,
            in CastleGatePlacementSpec placement,
            in CastleGatehousePlan gatehouse)
        {
            CastleGatehousePlanValidator.RequireValid(in gatehouse);

            CastleGateGeometry gateGeometry = CastleGateGeometryResolver.Resolve(
                in castle, in placement);
            float2 outward = gateGeometry.Outward;
            float2 tangent = gateGeometry.Tangent;
            float2 gate = gateGeometry.PerimeterCentre;
            int baseY = castle.Centre.y + castle.PlateauHeight;

            int2 left = Round(gate - tangent * gatehouse.TowerSpacing);
            int2 right = Round(gate + tangent * gatehouse.TowerSpacing);

            CastleTowerRealizer.Build(
                ref brush,
                in castle,
                new int3(left.x, baseY, left.y),
                castle.GateTowerRadius,
                gatehouse.LeftTowerHeight,
                false);
            CastleTowerRealizer.Build(
                ref brush,
                in castle,
                new int3(right.x, baseY, right.y),
                castle.GateTowerRadius,
                gatehouse.RightTowerHeight,
                false);

            if (gatehouse.BlockHeight > gatehouse.OpeningHeight)
            {
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    left,
                    right,
                    baseY + gatehouse.OpeningHeight,
                    gatehouse.BlockHeight - gatehouse.OpeningHeight,
                    castle.WallThickness * 2,
                    Mat.Stone);
            }

            BuildGateLeaf(ref brush, in gateGeometry);
            Crenellate(
                ref brush,
                left,
                right,
                baseY + gatehouse.BlockHeight,
                castle.WallThickness * 2);
            ApproachBridge(
                ref brush,
                gate,
                tangent,
                outward,
                baseY,
                in gatehouse);
        }

        private static void BuildGateLeaf(
            ref VoxelBrush brush,
            in CastleGateGeometry geometry)
        {
            for (int d = 0; d < geometry.Depth; d++)
            for (int w = 0; w < geometry.Width; w++)
            for (int h = 0; h < geometry.Height; h++)
            {
                if (!geometry.ContainsArchVoxel(w, h))
                    continue;

                int3 voxel = geometry.WorldVoxel(w, h, d);
                bool ironBand = (h >= 10 && h < 13)
                             || (h >= 23 && h < 26)
                             || (h >= 36 && h < 39);
                if (ironBand)
                {
                    brush.Set(voxel.x, voxel.y, voxel.z, Mat.DarkStone);
                    continue;
                }

                brush.SetStyled(
                    voxel.x,
                    voxel.y,
                    voxel.z,
                    Mat.Wood,
                    SurfaceStyles.Rounded,
                    Coatings.None,
                    VoxelSurfaceFlags.PreserveFeature);
            }
        }

        private static void Crenellate(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int parapetY,
            int wallThickness)
        {
            float2 a = new float2(start.x, start.y);
            float2 delta = new float2(end.x - start.x, end.y - start.y);
            float length = math.length(delta);
            if (length < 1f)
                return;

            float2 tangent = delta / length;
            const float merlon = 26f;
            const float gap = 18f;
            float period = merlon + gap;
            int thickness = math.max(2, math.min(8, wallThickness));

            for (float distance = 0f; distance < length; distance += period)
            {
                float endDistance = math.min(length, distance + merlon);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    Round(a + tangent * distance),
                    Round(a + tangent * endDistance),
                    parapetY,
                    20,
                    thickness,
                    Mat.Stone);
            }
        }

        private static void ApproachBridge(
            ref VoxelBrush brush,
            float2 gate,
            float2 tangent,
            float2 outward,
            int baseY,
            in CastleGatehousePlan gatehouse)
        {
            float2 near = gate + outward * gatehouse.BridgeNearDistance;
            float2 far = near + outward * gatehouse.BridgeLength;

            VoxelWallRasterizer.FillSegment(
                ref brush,
                Round(near),
                Round(far),
                baseY + gatehouse.BridgeDeckYOffset,
                gatehouse.BridgeDeckHeight,
                gatehouse.BridgeWidth,
                Mat.Wood);

            for (int side = -1; side <= 1; side += 2)
            {
                float2 offset = tangent * (side * gatehouse.BridgeSupportOffset);
                int2 supportNear = Round(near + offset);
                int2 supportFar = Round(far + offset);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    supportNear,
                    supportFar,
                    baseY + gatehouse.BridgeSupportYOffset,
                    gatehouse.BridgeSupportHeight,
                    gatehouse.BridgeSupportThickness,
                    Mat.DarkStone);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    supportNear,
                    supportFar,
                    baseY + gatehouse.BridgeRailYOffset,
                    gatehouse.BridgeRailHeight,
                    gatehouse.BridgeRailThickness,
                    Mat.Wood);
            }
        }

        private static int2 Round(float2 value) =>
            new int2((int)math.round(value.x), (int)math.round(value.y));
    }
}
