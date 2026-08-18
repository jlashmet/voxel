using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Resolved cathedral composition bundle. Core cathedral semantics remain in CathedralConfig;
    /// exterior structural support is a shared ButtressConfig rather than cathedral-private geometry.
    /// </summary>
    public struct CathedralWorldbuildingConfig
    {
        public CathedralConfig Cathedral;
        public ButtressConfig NaveButtresses;
        public bool ButtressesEnabled;

        public bool IsWellFormed =>
            Cathedral.IsWellFormed &&
            (!ButtressesEnabled ||
             (NaveButtresses.IsWellFormed &&
              NaveButtresses.MaxCountForSpan(Cathedral.Church.NaveLength) > 0));
    }

    public static class CathedralWorldbuildingPresets
    {
        public static CathedralWorldbuildingConfig Simple(in StructureMaterialPalette palette)
        {
            return new CathedralWorldbuildingConfig
            {
                Cathedral = CathedralPresets.Simple(in palette),
                ButtressesEnabled = true,
                NaveButtresses = new ButtressConfig
                {
                    Width = 6,
                    Depth = 7,
                    Height = 34,
                    Spacing = 28,
                    StartMargin = 16,
                    EndMargin = 16,
                    TaperPercent = 30,
                    FlyingEnabled = false,
                    FlyingSpan = 0,
                    FlyingRise = 0,
                    FlyingThickness = 0,
                    FlyingSupportHeight = 0,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                    FlyingMaterialRole = StructureMaterialRole.Trim,
                },
            };
        }

        public static CathedralWorldbuildingConfig Gothic(in StructureMaterialPalette palette)
        {
            return new CathedralWorldbuildingConfig
            {
                Cathedral = CathedralPresets.Gothic(in palette),
                ButtressesEnabled = true,
                NaveButtresses = new ButtressConfig
                {
                    Width = 7,
                    Depth = 10,
                    Height = 58,
                    Spacing = 30,
                    StartMargin = 18,
                    EndMargin = 18,
                    TaperPercent = 45,
                    FlyingEnabled = true,
                    FlyingSpan = 18,
                    FlyingRise = 12,
                    FlyingThickness = 3,
                    FlyingSupportHeight = 34,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                    FlyingMaterialRole = StructureMaterialRole.Trim,
                },
            };
        }
    }
}
