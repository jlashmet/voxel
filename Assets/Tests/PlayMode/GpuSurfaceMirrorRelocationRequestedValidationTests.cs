using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Targeted-CI adapters for relocation discriminators. The generic isolated PlayMode harness
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
            IEnumerator execution = RunWithGpuCutover(
                new GpuSurfaceMirrorRelocationLivenessTests()
                    .DistantRelocationCannotLeaveEveryGpuWorkerAdmissionPending());
            while (execution.MoveNext())
                yield return execution.Current;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression()
        {
            IEnumerator execution = RunWithGpuCutover(
                new GpuSurfaceMirrorRelocationLivenessTests()
                    .DistantUnrelatedReadyBlockChangesCannotStarveRelocatedCoverage());
            while (execution.MoveNext())
                yield return execution.Current;
        }

        private static IEnumerator RunWithGpuCutover(IEnumerator regression)
        {
            const string variable = "VOXEL_DISABLE_GPU_CUTOVER";
            string previous = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
            try
            {
                while (regression.MoveNext())
                    yield return regression.Current;
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, previous);
            }
        }
    }
}
