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
            CastlePosternGeometry geometry = CastlePosternGeometryResolver.Resolve(
                in plan, in postern);
            VoxelWallRasterizer.FillSegment(
                ref brush,
                geometry.OpeningStart,
                geometry.OpeningEnd,
                geometry.BaseY,
                geometry.OpeningHeight,
                geometry.OpeningDepth,
                Mat.Empty);
        }

        public static void BuildDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastlePosternGeometry geometry = CastlePosternGeometryResolver.Resolve(
                in plan, in postern);

            VoxelWallRasterizer.FillSegment(
                ref brush,
                geometry.LeafStart,
                geometry.LeafEnd,
                geometry.BaseY,
                geometry.LeafHeight,
                geometry.LeafDepth,
                Mat.Wood);

            VoxelWallRasterizer.FillSegment(
                ref brush,
                geometry.LeafStart,
                geometry.LeafEnd,
                geometry.FirstStrapY,
                geometry.StrapHeight,
                geometry.StrapDepth,
                Mat.DarkStone);
            VoxelWallRasterizer.FillSegment(
                ref brush,
                geometry.LeafStart,
                geometry.LeafEnd,
                geometry.SecondStrapY,
                geometry.StrapHeight,
                geometry.StrapDepth,
                Mat.DarkStone);
        }
    }
}
