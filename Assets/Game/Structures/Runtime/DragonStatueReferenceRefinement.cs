using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Version-A replacement pass. The earlier detailed prototype is deliberately erased before the
    /// reference-driven AAA authoring is applied, preserving the stable Model Viewer entry and capture
    /// plumbing while allowing the model itself to be rebuilt aggressively between visual iterations.
    /// </summary>
    public static class DragonStatueReferenceRefinement
    {
        public static void Apply(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Clear the complete legacy detailed-model envelope. Add a small guard band because prior
            // detail passes projected horns/claws just beyond the nominal LocalMin/LocalSize bounds.
            int3 min = DragonStatueDetailedVoxelAuthoring.LocalMin - new int3(8, 0, 8);
            int3 max = DragonStatueDetailedVoxelAuthoring.LocalMin
                     + DragonStatueDetailedVoxelAuthoring.LocalSize
                     + new int3(8, 8, 8);
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(origin.x + x, origin.y + y, origin.z + z, GameMaterialIds.Empty);

            DragonStatueAAAAuthoring.Author(a, origin);
        }
    }
}
