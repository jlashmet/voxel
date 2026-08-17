using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Validation-only wrapper around the production LOD fixture. Renderer behaviour and the
    /// wrapped acceptance test are unchanged; this wrapper only appends the detailed step-4
    /// lifecycle counters to the first production assertion that stops the existing path.
    /// </summary>
    public sealed class Step4LifecycleDiagnosticPlayTests
    {
        [UnityTest, Timeout(900000)]
        public IEnumerator ExistingLodFailureReportsStep4LifecycleCounters()
        {
            Step4FalseEmptyDiagnostics.Reset();
            IEnumerator inner = new LodRenderingTests().CastleKeepsVoxelGeometryAcrossEveryLodBand();
            try
            {
                while (true)
                {
                    bool moved = false;
                    object yielded = null;
                    AssertionException failure = null;
                    try
                    {
                        moved = inner.MoveNext();
                        if (moved) yielded = inner.Current;
                    }
                    catch (AssertionException ex)
                    {
                        failure = ex;
                    }

                    if (failure != null)
                    {
                        Assert.Fail($"{failure.Message}\nstep4Lifecycle={Step4FalseEmptyDiagnostics.Current}");
                        yield break;
                    }
                    if (!moved) break;
                    yield return yielded;
                }

                Assert.Fail($"Production LOD fixture unexpectedly passed; "
                          + $"step4Lifecycle={Step4FalseEmptyDiagnostics.Current}");
            }
            finally
            {
                (inner as IDisposable)?.Dispose();
            }
        }
    }
}
