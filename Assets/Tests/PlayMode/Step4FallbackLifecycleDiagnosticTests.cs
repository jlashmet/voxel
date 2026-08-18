using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Diagnostic-only wrapper for the production LOD fixture. The normal suite already runs
    /// <see cref="LodRenderingTests.CastleKeepsVoxelGeometryAcrossEveryLodBand"/>; this explicit
    /// test exists only to append the step-4 fallback predicate counters when that fixture fails.
    /// It deliberately changes no renderer behavior, budgets, LOD distances or acceptance limits.
    /// </summary>
    public sealed class Step4FallbackLifecycleDiagnosticTests
    {
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator CastleStep4FailureReportsFallbackAdmissionPredicates()
        {
            Step4FalseEmptyDiagnostics.Reset();
            IEnumerator inner = new LodRenderingTests().CastleKeepsVoxelGeometryAcrossEveryLodBand();

            while (true)
            {
                bool moved;
                try
                {
                    moved = inner.MoveNext();
                }
                catch (AssertionException failure)
                {
                    Assert.Fail($"{failure.Message}\nstep4Lifecycle={Step4FalseEmptyDiagnostics.Current}");
                    yield break;
                }

                if (!moved)
                    yield break;

                yield return inner.Current;
            }
        }
    }
}
