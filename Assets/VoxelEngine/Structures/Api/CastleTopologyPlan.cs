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
    /// assigned. Runtime never chooses from this type directly; CastleSpatialPlanner resolves it
    /// into validated spatial geometry before Composition hands the result to realization.
    /// </summary>
    public struct CastleTopologyPlan
    {
        public CastlePerimeterKind Perimeter;
        public CastleKeepPlacement KeepPlacement;
        public CastleWardPattern Wards;
        public int DesiredTowerCount;
        public bool HasPosternGate;
        public bool HasKeepAnnexPlan;
        public CastleKeepAnnexPlan KeepAnnexes;
    }
}
