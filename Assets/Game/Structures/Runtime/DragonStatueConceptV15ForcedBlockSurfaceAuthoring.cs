using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V15 fixes the V14 restyle bug: changing an already-solid voxel's material with Set preserves
    /// its prior surface semantics, so Smooth cells stayed smooth even after becoming Slate/Gold.
    /// This pass starts from V13 and uses SetWithPlacementStyle so every visible dragon voxel is
    /// rewritten with the selected material AND that material's catalogue placement style.
    /// Slate therefore becomes Planar; Gold becomes Sharp.
    /// </summary>
    public static class DragonStatueConceptV15ForcedBlockSurfaceAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Slate = GameMaterialIds.Slate;
        private const byte Gold = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            DragonStatueConceptV13WingPaletteAuthoring.Author(a, o);

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

                byte blockMaterial = material == Gold || material == GameMaterialIds.Dirt
                    ? Gold
                    : Slate;
                a.SetWithPlacementStyle(wx, wy, wz, blockMaterial);
            }
        }
    }
}
