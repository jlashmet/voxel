using MountingForce.WorldGen;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Backend mapping supplied by the game/voxel host. World-generation content never contains
    /// voxel material bytes, so changing a renderer palette cannot rewrite semantic world data.
    /// </summary>
    public readonly struct VoxelMaterialMap
    {
        public readonly byte FoundationStone;
        public readonly byte Masonry;
        public readonly byte DarkMasonry;
        public readonly byte Timber;
        public readonly byte Glass;
        public readonly byte WarmWindow;
        public readonly byte RoofTile;
        public readonly byte Slate;
        public readonly byte Cloth;
        public readonly byte Moss;
        public readonly byte Water;
        public readonly byte RoadSurface;

        public VoxelMaterialMap(byte foundationStone, byte masonry, byte darkMasonry,
                                byte timber, byte glass, byte warmWindow, byte roofTile,
                                byte slate, byte cloth, byte moss, byte water)
            : this(foundationStone, masonry, darkMasonry, timber, glass, warmWindow, roofTile,
                   slate, cloth, moss, water, darkMasonry)
        {
        }

        public VoxelMaterialMap(byte foundationStone, byte masonry, byte darkMasonry,
                                byte timber, byte glass, byte warmWindow, byte roofTile,
                                byte slate, byte cloth, byte moss, byte water, byte roadSurface)
        {
            FoundationStone = foundationStone;
            Masonry = masonry;
            DarkMasonry = darkMasonry;
            Timber = timber;
            Glass = glass;
            WarmWindow = warmWindow;
            RoofTile = roofTile;
            Slate = slate;
            Cloth = cloth;
            Moss = moss;
            Water = water;
            RoadSurface = roadSurface;
        }

        public byte Resolve(MaterialRole role)
        {
            return role switch
            {
                MaterialRole.FoundationStone => FoundationStone,
                MaterialRole.Masonry => Masonry,
                MaterialRole.DarkMasonry => DarkMasonry,
                MaterialRole.Timber => Timber,
                MaterialRole.Glass => Glass,
                MaterialRole.WarmWindow => WarmWindow,
                MaterialRole.RoofTile => RoofTile,
                MaterialRole.Slate => Slate,
                MaterialRole.Cloth => Cloth,
                MaterialRole.Moss => Moss,
                MaterialRole.Water => Water,
                MaterialRole.RoadSurface => RoadSurface,
                _ => Masonry,
            };
        }
    }

    /// <summary>Conversion from semantic world units to a particular voxel backend.</summary>
    public readonly struct VoxelWorldGenSettings
    {
        /// <summary>Current engine uses one 10 cm voxel, so this is normally 1.</summary>
        public readonly int VoxelsPerDecimetre;
        public readonly VoxelMaterialMap Materials;

        public VoxelWorldGenSettings(int voxelsPerDecimetre, VoxelMaterialMap materials)
        {
            VoxelsPerDecimetre = voxelsPerDecimetre < 1 ? 1 : voxelsPerDecimetre;
            Materials = materials;
        }
    }
}
