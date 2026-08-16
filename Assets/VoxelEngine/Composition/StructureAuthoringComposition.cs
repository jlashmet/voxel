using System;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition boundary for application/game-authored structures. Callers receive only the
    /// Structures.Api capability while the concrete VoxelBrush adapter remains in Structures.Runtime.
    /// </summary>
    public static class StructureAuthoringComposition
    {
        public static IStructureAuthoringSession Begin(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            int writeBudget)
        {
            if (reads == null) throw new ArgumentNullException(nameof(reads));
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            if (writeBudget <= 0) throw new ArgumentOutOfRangeException(nameof(writeBudget));

            return new StructureAuthoringSession(reads, mutations, materials, writeBudget);
        }
    }
}
