using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Diagnostics-only wrapper around the production LOD fixture. It does not alter rendering,
    /// budgets, camera bands, or acceptance thresholds; it only appends the step-4 cache lifecycle
    /// counters to the existing failure so the false-empty adjudication can be identified exactly.
    /// </summary>
    public sealed class Step4LifecycleDiagnosticTests
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator ProductionLodFailureReportsStep4Lifecycle()
        {
            Step4FalseEmptyDiagnostics.Reset();
            IEnumerator inner = new LodRenderingTests().CastleKeepsVoxelGeometryAcrossEveryLodBand();

            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = inner.MoveNext();
                    if (moved) current = inner.Current;
                }
                catch (AssertionException failure)
                {
                    Assert.Fail($"{failure.Message}\nstep4Lifecycle={Step4FalseEmptyDiagnostics.Current}");
                    yield break;
                }

                if (!moved) break;
                yield return current;
            }

            Assert.Fail(
                $"Production LOD fixture unexpectedly passed; "
              + $"step4Lifecycle={Step4FalseEmptyDiagnostics.Current}");
        }
    }
}
