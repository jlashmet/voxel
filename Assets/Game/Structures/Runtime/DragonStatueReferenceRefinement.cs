using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public static class DragonStatueReferenceRefinement
    {
        public static void Apply(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Remove the previous Dragon A envelopes without brute-force clearing the whole model
            // bounds. The reference voxel sculpture below then owns all visible anatomy.
            Clear(a, origin, new int3(-42, 72, -98), new int3(84, 106, 108));
            Clear(a, origin, new int3(-58, 0, -72), new int3(116, 108, 154));
            Clear(a, origin, new int3(-112, 28, -2), new int3(92, 124, 62));
            Clear(a, origin, new int3(20, 28, -2), new int3(92, 124, 62));
            Clear(a, origin, new int3(-58, 0, -94), new int3(168, 64, 166));

            DragonStatueReferenceVoxelArt.Author(a, origin);
        }

        private static void Clear(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(o.x + x, o.y + y, o.z + z, GameMaterialIds.Empty);
        }
    }
}
