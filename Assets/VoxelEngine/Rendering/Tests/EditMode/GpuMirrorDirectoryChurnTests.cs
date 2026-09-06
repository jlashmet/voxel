using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuMirrorDirectoryChurnTests
    {
        [TestCase(47662)]
        [TestCase(65536)]
        public void SharedDirectoryReallocationPreservesPreviousGpuByteCeiling(int previousSlots)
        {
            var layout = GpuVoxelBrickMirror.SharedLayout(previousSlots);
            int oldDirectory = 1024;
            while (oldDirectory < previousSlots * 4) oldDirectory *= 2;
            Assert.That(GpuVoxelBrickMirror.BytesForLayout(layout.Slots, layout.DirectoryEntries),
                Is.LessThanOrEqualTo(GpuVoxelBrickMirror.BytesForLayout(previousSlots, oldDirectory)));
            Assert.That(layout.Slots, Is.InRange(1, previousSlots));
        }

        [Test]
        public void TwoUniformCoarseCoresFitWithinExistingGpuByteCeiling()
        {
            const int previousSlots = 65536;
            var layout = GpuVoxelBrickMirror.SharedLayout(previousSlots);
            using var mirror = new GpuVoxelBrickMirror(layout.Slots, layout.DirectoryEntries);
            int published = 0;
            const int required = 2 * 64 * 64 * 64;
            for (int i = 0; i < required; i++)
            {
                int3 coordinate = new(i % 128, (i / 128) % 64, i / (128 * 64));
                if (mirror.Publish(VoxelBrickDelta.UniformAt(coordinate, 1, 1),
                    default, default, default, 0, false) != GpuBrickPublish.MetadataOnly) break;
                published++;
            }
            mirror.FlushPendingUploads();
            GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            Assert.That(published, Is.EqualTo(required), "Uniform coarse source keys must not exhaust the directory while mixed slots are empty.");
            Assert.That(mirror.CommittedBytes, Is.LessThanOrEqualTo(GpuVoxelBrickMirror.BytesForLayout(previousSlots, 1 << 18)));
            AssertGpuMaterial(mirror, new int3(127, 63, 63), 1);
        }

        [Test]
        public void ReusedCollisionAndClearRemainVisibleToGpuLookup()
        {
            using var mirror = new GpuVoxelBrickMirror(16);
            int3 first = new(-1, 0, 0), second = default;
            uint bucket = GpuVoxelBrickMirror.HashCoordinate(first) & (uint)mirror.DirectoryMask;
            bool found = false;
            for (int x = 0; x < 100000; x++)
                if ((GpuVoxelBrickMirror.HashCoordinate(new int3(x, 0, 0)) & (uint)mirror.DirectoryMask) == bucket)
                { second = new int3(x, 0, 0); found = true; break; }
            Assert.That(found, Is.True);
            Publish(mirror, first, 1); Publish(mirror, second, 2);
            mirror.Remove(first);
            Publish(mirror, second, 3); Publish(mirror, first, 4);
            AssertGpuMaterial(mirror, second, 3);
            mirror.Clear();
            Publish(mirror, second, 5);
            AssertGpuMaterial(mirror, second, 5);
        }

        private static void Publish(GpuVoxelBrickMirror mirror, int3 coordinate, byte material) =>
            Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(coordinate, 1, material),
                default, default, default, 0, false), Is.EqualTo(GpuBrickPublish.MetadataOnly));

        private static void AssertGpuMaterial(GpuVoxelBrickMirror mirror, int3 coordinate, uint material)
        {
            var shader = Object.Instantiate(Resources.Load<ComputeShader>("GpuBlockHlodSummary"));
            try
            {
                using var requests = new ComputeBuffer(1, 16);
                using var summaries = new ComputeBuffer(GpuBlockHlodSummary.WordsPerBlock, 4);
                requests.SetData(new[] { new int4(coordinate, 1) });
                GpuBlockHlodSummary.Dispatch(shader, mirror, requests, summaries, 1, 0);
                uint[] words = new uint[GpuBlockHlodSummary.WordsPerBlock];
                summaries.GetData(words);
                Assert.That(words[0], Is.EqualTo(uint.MaxValue));
                Assert.That(words[1], Is.EqualTo(uint.MaxValue));
                for (int i = 2; i < 18; i++)
                    Assert.That(words[i], Is.EqualTo(material * 0x01010101u));
            }
            finally { Object.DestroyImmediate(shader); }
        }

        [Test]
        public void MissingBricksDoNotScanAllTombstonesAfterDirectoryChurn()
        {
            using var mirror = new GpuVoxelBrickMirror(16);
            for (int i = 0; i < mirror.DirectoryCapacity; i++)
                Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(new int3(i, 0, 0), 1, 1),
                    default, default, default, 0, false), Is.EqualTo(GpuBrickPublish.MetadataOnly));
            for (int i = 0; i < mirror.DirectoryCapacity; i++) mirror.Remove(new int3(i, 0, 0));
            ulong before = mirror.DirectoryProbeChecks;
            for (int i = 0; i < 16; i++) mirror.Remove(new int3(-i - 1, 0, 0));
            mirror.FlushPendingUploads();
            var directory = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            for (int i = 0; i < mirror.DirectoryCapacity; i++)
                Assert.That(directory[i * 5 + 4], Is.Not.EqualTo(1u));
            Assert.That(mirror.DirectoryProbeChecks - before, Is.LessThanOrEqualTo(16UL),
                "An absent source must not walk the whole tombstone table for each recovery brick.");
        }
    }
}
