using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceDiscoveryMirrorTests
    {
        private ComputeShader _shader;
        private GpuSurfaceDiscoveryMirror _mirror;
        private int _kernel;

        [SetUp] public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _shader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfaceDiscovery"));
            _kernel = _shader.FindKernel("CSQueryDiscovery");
            _mirror = new GpuSurfaceDiscoveryMirror(_shader);
        }
        [TearDown] public void TearDown()
        {
            _mirror?.Dispose();
            if (_shader != null) Object.DestroyImmediate(_shader);
        }

        private void Compare(SurfaceDiscoveryCoverage coverage, List<int4> queries, int frame)
        {
            using var input = new ComputeBuffer(queries.Count, 16);
            using var result = new ComputeBuffer(queries.Count, 4);
            input.SetData(queries);
            _mirror.Prepare(coverage, frame);
            _mirror.Bind(_shader, _kernel);
            _shader.SetInt("_DiscoveryQueryCount", queries.Count);
            _shader.SetBuffer(_kernel, "_DiscoveryQueries", input);
            _shader.SetBuffer(_kernel, "_DiscoveryResults", result);
            _shader.Dispatch(_kernel, (queries.Count + 63) / 64, 1, 1);
            var actual = new uint[queries.Count];
            result.GetData(actual);
            for (int i = 0; i < queries.Count; i++)
            {
                int4 q = queries[i];
                bool expected = coverage.IsKnownEmpty(new SurfaceLodNodeKey(q.w, q.xyz));
                Assert.That(actual[i], Is.EqualTo(expected ? 1u : 0u), $"Frame {frame}, query {q}");
            }
        }

        [Test]
        public void EveryBitAndLodTracksCompletionRediscoveryAndEvictionAcrossBufferedFrames()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            var region = new int3(-1);
            coverage.Begin(region);
            var queries = new List<int4>();
            for (int bit = 0; bit < 512; bit++)
            {
                int3 fine = region * 8 + new int3(bit & 7, (bit >> 3) & 7, bit >> 6);
                if (bit % 17 == 0 || bit == 31 || bit == 32 || bit == 63 || bit == 64 || bit == 511)
                    coverage.AddSurfaceBlock(fine * 8);
                for (int shift = 0; shift < 4; shift++) queries.Add(new int4(fine >> shift, 1 << shift));
            }
            queries.Add(new int4(0, 0, 0, 1)); // unknown neighbor is never empty
            int frame = 0;
            for (int phase = 0; phase < 5; phase++)
            {
                if (phase == 1) coverage.Complete(region);
                if (phase == 2) coverage.Invalidate(region);
                if (phase == 3) { coverage.Begin(region); coverage.Complete(region); }
                if (phase == 4) coverage.Forget(region);
                for (int repeat = 0; repeat < 3; repeat++) Compare(coverage, queries, frame++);
                if (phase == 0)
                    coverage.AddSurfaceBlock((region * 8 + new int3(1, 0, 0)) * 8);
                int uploads = _mirror.UploadCount;
                for (int repeat = 0; repeat < 3; repeat++) Compare(coverage, queries, frame++);
                Assert.That(_mirror.UploadCount, Is.EqualTo(uploads), "Stable images must reuse all three GPU buffers.");
            }
            // A new world's matching revision number must not reuse the old world's image.
            var next = new SurfaceDiscoveryCoverage(); next.Begin(region); next.Complete(region);
            while (next.Version < coverage.Version) { next.Invalidate(region); next.Complete(region); }
            Assert.That(next.Version, Is.EqualTo(coverage.Version));
            Compare(next, queries, frame);
            Assert.That(_mirror.ResidentBytes, Is.LessThan(300 * 1024), "Discovery transport must stay bounded.");
        }

        [Test]
        public void FullCapacityHashCollisionsAndSlotReusePreserveUnknownProofs()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            var queries = new List<int4>();
            for (int i = 0; i < SurfaceDiscoveryCoverage.MaximumRegions; i++)
            {
                var region = new int3(i - 512, i * 17 % 127 - 63, i * 31 % 61 - 30);
                coverage.Begin(region); coverage.Complete(region);
                queries.Add(new int4(region, 8));
            }
            var rejected = new int3(9000, -73, 17);
            coverage.Begin(rejected); coverage.Complete(rejected);
            queries.Add(new int4(rejected, 8));
            Compare(coverage, queries, 0);
            coverage.Forget(queries[51].xyz);
            coverage.Begin(rejected); coverage.Complete(rejected);
            Compare(coverage, queries, 3);
            coverage.Clear();
            Compare(coverage, queries, 6);
        }
    }
}
