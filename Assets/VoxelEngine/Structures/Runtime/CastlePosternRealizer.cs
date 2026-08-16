using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Semantic postern wrapper over the reusable arched wall-door realizer. A postern differs
    /// from an inner-ward gate only by authored dimensions, not by voxel realization logic.
    /// </summary>
    public static class CastlePosternRealizer
    {
        public static void CarveOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern) =>
            CastleWallDoorRealizer.CarveArchedOpening(
                ref brush,
                in plan,
                in postern,
                CastleLayout.PosternGateWidth,
                CastleLayout.PosternGateHeight);

        public static void BuildDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern) =>
            CastleWallDoorRealizer.BuildArchedDoor(
                ref brush,
                in plan,
                in postern,
                CastleLayout.PosternGateWidth,
                CastleLayout.PosternGateHeight,
                CastleLayout.PosternGateDepth);
    }
}
