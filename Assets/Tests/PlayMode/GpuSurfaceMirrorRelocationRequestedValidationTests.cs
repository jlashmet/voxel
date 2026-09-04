using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Targeted-CI adapter for the relocation discriminator. The generic isolated PlayMode harness
    /// intentionally sets VOXEL_DISABLE_GPU_CUTOVER=1 for broad CPU-baseline validation. A request
    /// whose sole purpose is to exercise GPU mirror liveness must temporarily restore the production
    /// GPU path, then delegate to the exact regression so the behavioral discriminator stays in one
    /// place.
    /// </summary>
    public sealed class GpuSurfaceMirrorRelocationRequestedValidationTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator DistantRelocationExecutesProductionGpuLivenessRegression()
        {
            const string variable = "VOXEL_DISABLE_GPU_CUTOVER";
            string previous = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
            try
            {
                var regression = new GpuSurfaceMirrorRelocationLivenessTests();
                IEnumerator execution = regression.DistantRelocationCannotLeaveEveryGpuWorkerAdmissionPending();
                while (execution.MoveNext())
                    yield return execution.Current;
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }
    }
}
