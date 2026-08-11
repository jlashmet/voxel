using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation registry for semantic tree instances.
    ///
    /// The renderer consumes immutable snapshots published by world generation. Legacy showcase
    /// foliage also publishes the brick coordinates occupied by its old voxel crowns so the smooth
    /// terrain field can ignore those blobs while the new procedural representation is active.
    /// </summary>
    public static class ProceduralTreeRegistry
    {
        private static readonly List<TreeInstance> s_Instances = new();
        private static readonly HashSet<int3> s_ExcludedSmoothBricks = new();
        private static int s_Version;

        public static IReadOnlyList<TreeInstance> Instances => s_Instances;
        public static int Version => s_Version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Instances.Clear();
            s_ExcludedSmoothBricks.Clear();
            unchecked { s_Version++; }
        }

        /// <summary>
        /// Atomically replaces the derived vegetation snapshot. Incrementing the version once lets
        /// tree rendering and Transvoxel invalidate exactly once rather than once per tree.
        /// </summary>
        public static void Replace(IReadOnlyList<TreeInstance> instances,
                                   IEnumerable<int3> excludedSmoothBricks)
        {
            s_Instances.Clear();
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++) s_Instances.Add(instances[i]);
            }

            s_ExcludedSmoothBricks.Clear();
            if (excludedSmoothBricks != null)
            {
                foreach (int3 brick in excludedSmoothBricks)
                    s_ExcludedSmoothBricks.Add(brick);
            }

            unchecked { s_Version++; }
        }

        public static bool IsExcludedSmoothBrick(int3 worldBrick) =>
            s_ExcludedSmoothBricks.Contains(worldBrick);
    }
}
