using NUnit.Framework;
using System;
using System.Reflection;
using VoxelEngine.Storage.Api;
using Object = UnityEngine.Object;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuMirrorDirectoryChurnTests
    {
        [Test]
        public void QueuedGpuReadsSurviveCollisionChainRelocationAcrossDirectoryWrap()
        {
            using var mirror = new GpuVoxelBrickMirror(16);
            var keys = new int3[3]; int found = 0;
            for (int x = 0; x < 100000 && found < keys.Length; x++)
            {
                int3 key = new(x, -3, 7);
                if ((GpuVoxelBrickMirror.HashCoordinate(key) & (uint)mirror.DirectoryMask) == mirror.DirectoryMask)
                    keys[found++] = key;
            }
            Assert.That(found, Is.EqualTo(3));
            for (int i = 0; i < keys.Length; i++) Publish(mirror, keys[i], (byte)(i + 1));
            var shader = Object.Instantiate(Resources.Load<ComputeShader>("GpuBlockHlodSummary"));
            using var requests = new ComputeBuffer(2, 16);
            using var before = new ComputeBuffer(38, 4);
            using var after = new ComputeBuffer(38, 4);
            requests.SetData(new[] { new int4(keys[1], 1), new int4(keys[2], 1) });
            mirror.RetainSubmission();
            try
            {
                GpuBlockHlodSummary.Dispatch(shader, mirror, requests, before, 2, 0);
                mirror.Remove(keys[0]);
                GpuBlockHlodSummary.Dispatch(shader, mirror, requests, after, 2, 0);
                uint[] oldWords = new uint[38], newWords = new uint[38];
                before.GetData(oldWords); after.GetData(newWords);
                CollectionAssert.AreEqual(oldWords, newWords);
                Assert.That(newWords[2], Is.EqualTo(0x02020202u));
                Assert.That(newWords[21], Is.EqualTo(0x03030303u));
                var directory = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
                Assert.That(directory[mirror.DirectoryMask * 5], Is.EqualTo((uint)keys[1].x));
                Assert.That(directory[0], Is.EqualTo((uint)keys[2].x));
                Assert.That(directory[9], Is.Zero);
            }
            finally { mirror.ReleaseSubmission(); Object.DestroyImmediate(shader); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectoryPressureReclaimsColdUniformKeysButProtectsReaders(bool allProtected)
        {
            var type = typeof(GpuSurfaceMirrorCoordinator);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var field = type.GetField("s_Mirror", flags);
            var previous = field.GetValue(null);
            var clear = (Action)type.GetMethod("ClearReadyBlocks", flags).CreateDelegate(typeof(Action));
            var add = (Action<int3>)type.GetMethod("AddReadyBlock", flags).CreateDelegate(typeof(Action<int3>));
            using var mirror = new GpuVoxelBrickMirror(16);
            clear();
            field.SetValue(null, mirror);
            int demandEdge = allProtected ? mirror.DirectoryCapacity : 16;
            ulong demand = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, demandEdge, default, default);
            Assert.That(GpuSurfaceMirrorCoordinator.TryBeginExtraction(new int3(16, 0, 0), 16, out ulong active), Is.True);
            try
            {
                for (int i = 0; i < mirror.DirectoryLiveCapacity; i++)
                {
                    int3 key = new(i, 0, 0);
                    Publish(mirror, key, 1); add(key);
                }
                var delta = VoxelBrickDelta.UniformAt(new int3(-1, 0, 0), 2, 2);
                GpuBrickPublish result = (GpuBrickPublish)type.GetMethod("PublishBlock", flags)
                    .Invoke(null, new object[] { delta, default(RegionReadView), int3.zero, VoxelReadBlockKind.Uniform });
                mirror.FlushPendingUploads();
                var words = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
                Assert.That(result, Is.EqualTo(allProtected ? GpuBrickPublish.DirectoryFull : GpuBrickPublish.MetadataOnly),
                    "Directory pressure must reclaim a cold uniform entry even when there are no mixed entries to evict.");
                for (int protectedX = 0; protectedX < 32; protectedX++)
                {
                    bool found = false;
                    for (int i = 0; i < mirror.DirectoryCapacity; i++)
                        if (words[i * 5 + 4] == 1 && words[i * 5] == protectedX
                            && words[i * 5 + 1] == 0 && words[i * 5 + 2] == 0) found = true;
                    Assert.That(found, Is.True, $"Protected source {protectedX} was evicted.");
                }
                AssertGpuMaterial(mirror, new int3(-1, 0, 0), allProtected ? 0u : 2u);
            }
            finally
            {
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(int3.zero, demandEdge, default, default, demand);
                GpuSurfaceMirrorCoordinator.EndExtraction(new int3(16, 0, 0), 16, active);
                clear();
                field.SetValue(null, previous);
            }
        }

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
                Assert.That(words[0], Is.EqualTo(material == 0 ? 0u : uint.MaxValue));
                Assert.That(words[1], Is.EqualTo(material == 0 ? 0u : uint.MaxValue));
                for (int i = 2; i < 18; i++)
                    Assert.That(words[i], Is.EqualTo(material * 0x01010101u));
            }
            finally { Object.DestroyImmediate(shader); }
        }

        [Test]
        public void MissingBricksRemainBoundedAfterEveryDirectoryBucketHasBeenReused()
        {
            using var mirror = new GpuVoxelBrickMirror(16);
            // Visit every home bucket without filling the live-key limit. Deletion must leave
            // a bounded missing-key lookup, with each insertion at a proven one-probe distance.
            var homes = new int3[mirror.DirectoryCapacity];
            var seen = new bool[mirror.DirectoryCapacity];
            int remaining = homes.Length;
            for (int x = 0; remaining > 0 && x < 100000; x++)
            {
                int bucket = (int)(GpuVoxelBrickMirror.HashCoordinate(new int3(x, 0, 0)) & (uint)mirror.DirectoryMask);
                if (seen[bucket]) continue;
                seen[bucket] = true; homes[bucket] = new int3(x, 0, 0); remaining--;
            }
            Assert.That(remaining, Is.Zero);
            foreach (int3 home in homes) { Publish(mirror, home, 1); mirror.Remove(home); }
            Assert.That(mirror.MaximumDirectoryProbeCount, Is.EqualTo(1));
            ulong before = mirror.DirectoryProbeChecks;
            for (int i = 0; i < 16; i++) mirror.Remove(new int3(-i - 1, 0, 0));
            mirror.FlushPendingUploads();
            var directory = GpuMirrorClearLifetimeTests.ReadDirectory(mirror);
            for (int i = 0; i < mirror.DirectoryCapacity; i++)
                Assert.That(directory[i * 5 + 4], Is.Not.EqualTo(1u));
            Assert.That(mirror.DirectoryProbeChecks - before, Is.LessThanOrEqualTo(16UL),
                "An absent source must not walk the whole tombstone table for each recovery brick.");
            AssertGpuMaterial(mirror, new int3(-1, 0, 0), 0);
            using var preparation = new GpuBrickCachePreparation(1, 1);
            preparation.Dispatch(mirror, new[] { new GpuChunkExtraction(int3.zero, new int3(-1), 1, 0.1f) }, 1);
            var resolved = new uint[1]; preparation.DenseEntries.GetData(resolved);
            Assert.That(resolved[0], Is.Zero);
        }
    }
}
