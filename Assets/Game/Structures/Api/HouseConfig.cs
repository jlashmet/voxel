using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Archetype-level house shell configuration composed entirely from shared structure contracts.
    /// Detailed roof, facade, interior, and optional feature policies are layered on this type by the
    /// following house tasks; authoritative geometry still compiles to the existing shape pipeline.
    /// </summary>
    public struct HouseConfig
    {
        /// <summary>House footprint plus foundation/terrain-adaptation policy.</summary>
        public StructureFootprintConfig Footprint;

        /// <summary>Total exterior wall run policy, including thickness and semantic material bands.</summary>
        public StructureWallRunConfig ExteriorWall;

        /// <summary>Repeated floor/level policy. Exterior wall height spans all configured levels.</summary>
        public FloorLevelConfig Levels;

        /// <summary>Semantic material-role mapping used by all house components.</summary>
        public StructureMaterialPalette Palette;

        public int Width => Footprint.Primary.Size.x;
        public int Depth => Footprint.Primary.Size.y;
        public int FloorCount => Levels.FloorCount;
        public int FloorHeight => Levels.LevelHeight;
        public int WallThickness => ExteriorWall.Thickness;
        public int FoundationDepth => Footprint.FoundationDepth;
        public StructureFoundationStyle FoundationStyle => Footprint.FoundationStyle;
        public int TotalWallHeight => Levels.FloorCount * Levels.LevelHeight;

        /// <summary>
        /// Universal shell invariants. More detailed house components add their own validation but
        /// may rely on the shell dimensions being internally consistent and strictly bounded.
        /// </summary>
        public bool IsWellFormed
        {
            get
            {
                if (!Footprint.IsWellFormed || !ExteriorWall.IsWellFormed || !Levels.IsWellFormed)
                    return false;

                if (ExteriorWall.Length != Width)
                    return false;

                return ExteriorWall.Height == TotalWallHeight;
            }
        }
    }
}
