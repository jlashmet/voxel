namespace VoxelEngine.Structures.Api
{
    public enum CastlePerimeterKind : byte
    {
        Rectangular,
        IrregularQuadrilateral,
        IrregularPolygon,
        Concentric,
    }

    public enum CastleKeepPlacement : byte
    {
        Central,
        Rear,
        HighestGround,
        WallIntegrated,
    }

    public enum CastleWardPattern : byte
    {
        SingleWard,
        InnerAndOuterWards,
    }

    /// <summary>
    /// Planning-only semantic choices for a castle before coordinates or voxel realization are
    /// assigned. Runtime realization does not consume this type yet; it exists so topology can be
    /// developed and validated independently without perturbing the current castle output.
    /// </summary>
    public struct CastleTopologyPlan
    {
        public CastlePerimeterKind Perimeter;
        public CastleKeepPlacement KeepPlacement;
        public CastleWardPattern Wards;
        public int DesiredTowerCount;
        public bool HasPosternGate;
    }
}
