using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Guards the editor lifecycle around GPU resources.
    ///
    /// This exists because of a specific failure: <see cref="VoxelRenderFeature.Create"/> is
    /// called by URP on every domain reload and every inspector edit, and an earlier version
    /// replaced its render pass without disposing the old one. A pass owns a set of
    /// ComputeBuffers, which the garbage collector never reclaims, so every script compile
    /// leaked them for the life of the process.
    ///
    /// Play-mode tests cannot see this. They run once, in a fresh domain, and never recreate a
    /// renderer feature — which is why the bug shipped past a suite that was passing.
    ///
    /// These count buffer lifetimes rather than measuring memory, and that choice was forced.
    /// Two memory metrics were tried first and both were blind:
    ///
    ///   Profiler.GetAllocatedMemoryForGraphicsDriver never decreases when a buffer is released,
    ///   so correct create/dispose code reads as a textbook leak — 250 MB per five cycles,
    ///   perfectly linear, no plateau.
    ///
    ///   Process RSS does not move at all for GPU allocations in a headless editor, so twelve
    ///   deliberately leaked buffer sets — over 600 MB — read as zero.
    ///
    /// Counting create against release tests the real contract and works on any device.
    /// </summary>
    public class RenderResourceLifetimeTests
    {
        /// <summary>Small enough to keep the test cheap; the fixed buffers dominate either way.</summary>
        private const int PoolCapacity = 4096;

        private const int Cycles = 25;

        /// <summary>ComputeBuffers one <see cref="VoxelGpuBuffers"/> holds when allocated.</summary>
        private const int BuffersPerSet = 5;

        [Test]
        public void CreatingAndDisposingGpuBuffersReturnsEveryBuffer()
        {
            int baseline = VoxelGpuBuffers.LiveBuffers;

            for (var i = 0; i < Cycles; i++)
            {
                var buffers = new VoxelGpuBuffers();
                buffers.EnsureCreated(PoolCapacity);
                buffers.Dispose();
            }

            int leaked = VoxelGpuBuffers.LiveBuffers - baseline;
            Debug.Log($"### BUFFERS {Cycles} create/dispose cycles: {leaked} buffers still live");

            Assert.AreEqual(0, leaked, $"{Cycles} cycles leaked {leaked} ComputeBuffers");
        }

        [Test]
        public void LeakCheckCanActuallyDetectALeak()
        {
            // The load-bearing test. If this passes vacuously, every other assertion here is
            // decoration — which is exactly what happened with both memory metrics.
            const int leakedSets = 4;

            int baseline = VoxelGpuBuffers.LiveBuffers;
            var held = new VoxelGpuBuffers[leakedSets];

            for (var i = 0; i < leakedSets; i++)
            {
                held[i] = new VoxelGpuBuffers();
                held[i].EnsureCreated(PoolCapacity);
            }

            int observed = VoxelGpuBuffers.LiveBuffers - baseline;
            Debug.Log($"### CANARY {leakedSets} undisposed sets: {observed} buffers live");

            foreach (var b in held) b.Dispose();

            Assert.AreEqual(leakedSets * BuffersPerSet, observed,
                "the leak check did not notice undisposed buffers — the measurement is blind " +
                "and every other assertion in this file is worthless");
            Assert.AreEqual(baseline, VoxelGpuBuffers.LiveBuffers, "cleanup did not return the buffers");
        }

        [Test]
        public void RenderFeatureCreateDoesNotLeakItsPass()
        {
            var feature = ScriptableObject.CreateInstance<VoxelRenderFeature>();

            try
            {
                // Create() then force the pass to allocate, which is what the first recorded
                // frame does. Repeating it is a domain-reload storm in miniature.
                feature.Create();
                feature.Pass.Buffers.EnsureCreated(PoolCapacity);

                int baseline = VoxelGpuBuffers.LiveBuffers;

                for (var i = 0; i < Cycles; i++)
                {
                    feature.Create();
                    feature.Pass.Buffers.EnsureCreated(PoolCapacity);
                }

                int leaked = VoxelGpuBuffers.LiveBuffers - baseline;
                Debug.Log($"### FEATURE {Cycles} Create() cycles: {leaked} buffers leaked");

                Assert.AreEqual(0, leaked,
                    $"{Cycles} Create() calls leaked {leaked} ComputeBuffers — Create must " +
                    "dispose the pass it replaces");
            }
            finally
            {
                feature.Pass?.Dispose();
                Object.DestroyImmediate(feature);
            }
        }

        [Test]
        public void OversizedPoolIsRefusedRatherThanAllocated()
        {
            // A bad capacity used to become a multi-gigabyte ComputeBuffer, which takes the
            // machine with it rather than failing locally.
            using var buffers = new VoxelGpuBuffers();

            LogAssert.ignoreFailingMessages = true;
            buffers.EnsureCreated(VoxelGpuBuffers.MaxMirroredBricks * 64);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(buffers.IsCreated, "an absurd pool capacity was allocated anyway");
        }
    }
}
