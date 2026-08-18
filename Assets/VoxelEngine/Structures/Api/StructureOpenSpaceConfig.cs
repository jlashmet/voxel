namespace VoxelEngine.Structures.Api
{
    /// <summary>Surface treatment for a courtyard, plaza, patio, or other intentional open space.</summary>
    public enum OpenSpaceSurfaceMode : byte
    {
        None = 0,
        Paved = 1,
        Floor = 2,
    }

    /// <summary>
    /// Archetype-neutral treatment of one edge of an open space. Colonnade and arcade are semantic
    /// composition requests; their geometry must delegate to the shared column/opening components.
    /// </summary>
    public enum OpenSpaceEdgeKind : byte
    {
        Open = 0,
        Wall = 1,
        Colonnade = 2,
        Arcade = 3,
        BuildingFace = 4,
    }

    /// <summary>Configuration for one cardinal edge of a reusable open-space component.</summary>
    public struct OpenSpaceEdgeConfig
    {
        public OpenSpaceEdgeKind Kind;
        public int Height;
        public int Thickness;
        public int RepetitionSpacing;
        public int EntranceWidth;
        public StructureMaterialRole PrimaryMaterialRole;
        public StructureMaterialRole TrimMaterialRole;

        public bool IsWellFormed
        {
            get
            {
                if (EntranceWidth < 0 || RepetitionSpacing < 0)
                    return false;

                if (Kind == OpenSpaceEdgeKind.Open || Kind == OpenSpaceEdgeKind.BuildingFace)
                    return Height >= 0 && Thickness >= 0;

                return Height > 0 && Thickness > 0;
            }
        }
    }

    /// <summary>
    /// Reusable rectangular courtyard/open-space composition. The area remains definition-local and
    /// half-open through <see cref="StructureFootprintRect"/>. Each edge independently selects an
    /// open, wall, colonnade, arcade, or existing-building treatment so archetypes can compose
    /// cloisters, castle courtyards, temple courts, plazas, and patios without duplicating geometry.
    /// </summary>
    public struct OpenSpaceConfig
    {
        public StructureFootprintRect Area;
        public OpenSpaceSurfaceMode SurfaceMode;
        public int SurfaceThickness;
        public StructureMaterialRole SurfaceMaterialRole;

        public OpenSpaceEdgeConfig North;
        public OpenSpaceEdgeConfig East;
        public OpenSpaceEdgeConfig South;
        public OpenSpaceEdgeConfig West;

        public bool IsWellFormed =>
            Area.IsValid &&
            (SurfaceMode == OpenSpaceSurfaceMode.None
                ? SurfaceThickness >= 0
                : SurfaceThickness > 0) &&
            North.IsWellFormed &&
            East.IsWellFormed &&
            South.IsWellFormed &&
            West.IsWellFormed;
    }
}
