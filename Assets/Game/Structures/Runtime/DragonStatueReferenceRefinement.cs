using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Version-A replacement pass. The failed prototype is erased by occupied-part envelopes before
    /// the reference-driven rebuild is authored. This keeps iteration deterministic without spending
    /// the structure write budget clearing the large amount of empty space inside the model bounds.
    /// </summary>
    public static class DragonStatueReferenceRefinement
    {
        public static void Apply(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Head + long neck, torso/limbs, both wing volumes, and low sweeping tail.
            Clear(a, origin, new int3(-38, 74, -92), new int3(76, 100, 98));
            Clear(a, origin, new int3(-50, 0, -48), new int3(100, 102, 122));
            Clear(a, origin, new int3(-110, 30, 0), new int3(88, 118, 54));
            Clear(a, origin, new int3(22, 30, 0), new int3(88, 118, 54));
            Clear(a, origin, new int3(-18, 0, -86), new int3(126, 58, 146));

            DragonStatueAAAAuthoring.Author(a, origin);
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
