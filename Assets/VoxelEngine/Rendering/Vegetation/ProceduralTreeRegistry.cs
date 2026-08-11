using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation registry for semantic tree instances. World generation publishes an immutable
    /// deterministic snapshot; render geometry and LODs are derived from these identities.
    /// </summary>
    public static class ProceduralTreeRegistry
    {
        private static readonly List<TreeInstance> s_Instances = new();
        private static int s_Version;

        public static IReadOnlyList<TreeInstance> Instances => s_Instances;
        public static int Version => s_Version;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Instances.Clear();
            unchecked { s_Version++; }
        }

        /// <summary>
        /// Atomically replaces the vegetation snapshot. One version increment lets presentation
        /// rebuild once rather than once per tree.
        /// </summary>
        public static void Replace(IReadOnlyList<TreeInstance> instances)
        {
            s_Instances.Clear();
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++) s_Instances.Add(instances[i]);
            }
            unchecked { s_Version++; }
        }
    }
}
