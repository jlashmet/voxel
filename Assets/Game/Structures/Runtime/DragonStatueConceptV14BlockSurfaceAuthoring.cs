using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V14 enforces a deliberately block-authored material language. Previous passes selected some
    /// rows for colour even though their authored surface style was Smooth, which undermined the
    /// voxel sculpture and softened otherwise intentional facets. All visible cool structure is
    /// rewritten as Slate/Planar; warm armor, horns, teeth and claws are Gold/Sharp. Moss survives
    /// only as a coating over a planar Slate substrate, never as a smooth solid voxel material.
    /// </summary>
    public static class DragonStatueConceptV14BlockSurfaceAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Slate = GameMaterialIds.Slate;
        private const byte Gold = GameMaterialIds.Gold;
        private const byte Moss = GameMaterialIds.Moss;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            DragonStatueConceptV13WingPaletteAuthoring.Author(a, o);

            // Keep the scan tightly around authored dragon volumes. These regions cover head/neck,
            // body/limbs, both wings and the complete foreground tail without walking the entire
            // 270 x 170 x 235 placement box.
            RestyleRegion(a, o, new int3(-40, 52, -110), new int3(80, 108, 116));
            RestyleRegion(a, o, new int3(-65, 0, -80), new int3(130, 82, 145));
            RestyleRegion(a, o, new int3(-135, 36, -38), new int3(270, 124, 96));
            RestyleRegion(a, o, new int3(5, 0, -72), new int3(110, 45, 132));
        }

        private static void RestyleRegion(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
            {
                int wx = o.x + x;
                int wy = o.y + y;
                int wz = o.z + z;
                byte material = a.Get(wx, wy, wz);
                if (material == Empty)
                    continue;

                bool moss = material == Moss || a.GetCoating(wx, wy, wz) == Coatings.Moss;

                // Gold is the only warm visible solid. Everything else is a cool Slate block.
                // This intentionally removes Smooth Stone, DarkStone, Dirt and solid Moss from
                // the dragon even if an older authored layer introduced one of those rows.
                if (material == Gold || material == GameMaterialIds.Dirt)
                    a.SetStyled(wx, wy, wz, Gold, SurfaceStyles.Sharp);
                else
                    a.SetStyled(wx, wy, wz, Slate, SurfaceStyles.Planar);

                if (moss)
                    a.Coat(wx, wy, wz, Coatings.Moss);
            }
        }
    }
}
