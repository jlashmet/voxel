using System.Reflection;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge-only renderer composition for the discrete ShowcaseWorld streaming lattice.
    /// Runs after scene objects have enabled so the slice has installed its normal renderer world,
    /// then narrows only the near-surface completeness ring to the radius residency can guarantee.
    /// Streaming radius, far-field coverage, generation budgets, and renderer policy are unchanged.
    /// </summary>
    internal static class KentridgeStreamingCoverageInstaller
    {
        private static readonly FieldInfo s_LoadRadiusField = typeof(KentridgePlayableSlice).GetField(
            "m_LoadRadiusRegions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyForLoadedPlayableSlice()
        {
            KentridgePlayableSlice slice = Object.FindFirstObjectByType<KentridgePlayableSlice>();
            if (slice == null || s_LoadRadiusField == null) return;

            int loadRadiusRegions = (int)s_LoadRadiusField.GetValue(slice);
            float guaranteedRadius = KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(
                loadRadiusRegions,
                ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelRingRadiusMetres(guaranteedRadius);
        }
    }
}
