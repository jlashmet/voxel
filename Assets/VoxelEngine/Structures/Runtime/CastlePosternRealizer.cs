using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the secondary pedestrian postern as a low opening in an otherwise intact curtain
    /// wall. It deliberately has no gate towers, bridge, or gatehouse semantics.
    /// </summary>
    public static class CastlePosternRealizer
    {
        public static void CarveOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastleApproachFrame frame = CastleApproachFrame.FromGate(in postern);
            int half = CastleLayout.PosternGateWidth / 2;
            int2 localLeft = frame.LocalPoint(-half, 0f);
            int2 localRight = frame.LocalPoint(half, 0f);
            int2 left = ToWorld(in plan, localLeft);
            int2 right = ToWorld(in plan, localRight);
            int baseY = plan.Centre.y + plan.PlateauHeight;

            VoxelWallRasterizer.FillSegment(
                ref brush,
                left,
                right,
                baseY + 1,
                CastleLayout.PosternGateHeight,
                plan.WallThickness + 4,
                Mat.Empty);
        }

        public static void BuildDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastleApproachFrame frame = CastleApproachFrame.FromGate(in postern);
            int half = CastleLayout.PosternGateWidth / 2 - 2;
            int2 localLeft = frame.LocalPoint(-half, 0f);
            int2 localRight = frame.LocalPoint(half, 0f);
            int2 left = ToWorld(in plan, localLeft);
            int2 right = ToWorld(in plan, localRight);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int doorHeight = CastleLayout.PosternGateHeight - 4;

            VoxelWallRasterizer.FillSegment(
                ref brush,
                left,
                right,
                baseY + 1,
                doorHeight,
                CastleLayout.PosternGateDepth,
                Mat.Wood);

            VoxelWallRasterizer.FillSegment(
                ref brush,
                left,
                right,
                baseY + 11,
                2,
                CastleLayout.PosternGateDepth + 1,
                Mat.DarkStone);
            VoxelWallRasterizer.FillSegment(
                ref brush,
                left,
                right,
                baseY + 25,
                2,
                CastleLayout.PosternGateDepth + 1,
                Mat.DarkStone);
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);
    }
}
