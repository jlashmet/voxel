using System.Collections;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-owned acceptance probe for the Water validation tableau. The generic built-player
    /// harness only waits for declared log evidence; this component defines the Water module's
    /// stronger requirement that the production liquid owner itself has published visible
    /// geometry, instead of accepting solid-terrain convergence as a proxy.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Water Liquid Publication Validation Probe")]
    [DisallowMultipleComponent]
    public sealed class WaterLiquidPublicationValidationProbe : MonoBehaviour
    {
        private const int RequiredStableFrames = 20;
        private const int MaximumWaitFrames = 900;

        private IEnumerator Start()
        {
            int stableFrames = 0;
            int waitedFrames = 0;
            while (waitedFrames++ < MaximumWaitFrames && stableFrames < RequiredStableFrames)
            {
                RenderingSurfaceDiagnostics.GetLiquidSurfaceCounts(
                    out int resident, out _, out int visible, out ulong completedBuilds);
                if (resident > 0 && visible > 0 && completedBuilds > 0)
                    stableFrames++;
                else
                    stableFrames = 0;
                yield return null;
            }

            RenderingSurfaceDiagnostics.GetLiquidSurfaceCounts(
                out int finalResident,
                out int finalDirty,
                out int finalVisible,
                out ulong finalCompletedBuilds);

            if (stableFrames < RequiredStableFrames)
            {
                Debug.LogError(
                    $"WATER_VALIDATION liquid renderer did not converge: resident={finalResident}, dirty={finalDirty}, visible={finalVisible}, completed={finalCompletedBuilds}.");
                yield break;
            }

            Debug.Log(
                $"WATER_VALIDATION liquid-ready: resident={finalResident}, dirty={finalDirty}, visible={finalVisible}, completed={finalCompletedBuilds}.");
        }
    }
}
