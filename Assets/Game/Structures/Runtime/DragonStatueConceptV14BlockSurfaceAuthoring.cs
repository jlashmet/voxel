using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V14 enforces a deliberately block-authored material language. Previous passes selected some
    /// rows for colour even though their material defaults produced smooth-looking surfaces. The
    /// final visible sculpt is rewritten to Slate for the cool body/wings and Gold for the warm
    /// armor, horns, teeth and claws. Those catalogue materials use the intended block-authored
    /// Planar/Sharp placement styles through the supported structure authoring API.
    /// </summary>
    public static class DragonStatueConceptV14BlockSurfaceAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Slate = GameMaterialIds.Slate;
        private const byte Gold = GameMaterialIds.Gold;

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

                // Gold is the only warm visible solid. Everything else becomes Slate. Using the
                // normal structure Set API preserves assembly boundaries while allowing each
                // material's catalogue placement style (Slate=Planar, Gold=Sharp) to define the
                // block surface correctly.
                if (material == Gold || material == GameMaterialIds.Dirt)
                    a.Set(wx, wy, wz, Gold);
                else
                    a.Set(wx, wy, wz, Slate);
            }
        }
    }
}
