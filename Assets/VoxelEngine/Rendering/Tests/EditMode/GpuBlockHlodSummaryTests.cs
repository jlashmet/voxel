using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuBlockHlodSummaryTests
    {
        private ComputeShader _shader;
        private GpuVoxelBrickMirror _mirror;
        private ComputeBuffer _requests, _summaries;
        private static readonly int3 Coordinate = new(-9, 3, -2);

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _shader = Object.Instantiate(Resources.Load<ComputeShader>("GpuBlockHlodSummary"));
            Assert.That(_shader, Is.Not.Null);
            _mirror = new GpuVoxelBrickMirror(4);
            _requests = new ComputeBuffer(2, 16);
            _summaries = new ComputeBuffer(2 * GpuBlockHlodSummary.WordsPerBlock, 4);
            _requests.SetData(new[] { new int4(Coordinate, 0), new int4(Coordinate + 1, 0) });
        }

        [TearDown]
        public void TearDown()
        {
            _summaries?.Dispose(); _requests?.Dispose(); _mirror?.Dispose();
            if (_shader != null) Object.DestroyImmediate(_shader);
        }

        [TestCase(-1)]
        [TestCase(63)]
        public void CanonicalReferencesDecodeUniformAirAndWaterWithoutDirectoryKeys(int x)
        {
            int3 origin = new(x, 63, -1), second = origin + new int3(1, 0, 0);
            _requests.SetData(new[] { new int4(origin >> 6, 1), new int4(second >> 6, 1) });
            using var references = new ComputeBuffer(8192, 4);
            using var missing = new ComputeBuffer(3, 4);
            var encoded = new int[8192];
            var feedback = new uint[3]; var summary = new uint[_summaries.count];
            void SetReference(int3 coordinate, int value)
            {
                for (int r = 0; r < 2; r++)
                {
                    int3 region = (r == 0 ? origin : second) >> 6;
                    if (!math.all(region == (coordinate >> 6))) continue;
                    int3 local = coordinate - region * 64;
                    encoded[r * 4096 + local.x + 64 * local.y] = value;
                }
            }
            void Dispatch()
            {
                references.SetData(encoded); missing.SetData(new uint[3]);
                GpuBlockHlodSummary.DispatchSourceRange(_shader, _mirror, _requests, 2,
                    origin, new int3(2, 1, 1), _summaries, 0, missing, 1u << 11,
                    blockReferences: references);
                missing.GetData(feedback); _summaries.GetData(summary);
            }
            SetReference(origin, -201); SetReference(second, -1);
            // Canonical uniform metadata must override an obsolete directory payload.
            _mirror.Publish(VoxelBrickDelta.UniformAt(origin, 1, 3), default, default, default, 0, false);
            _mirror.InvalidateReadiness(origin);
            Dispatch(); Assert.That(feedback[0], Is.Zero);
            Assert.That(summary[2], Is.EqualTo(0xc8c8c8c8u));
            Assert.That(summary[19] | summary[20] | summary[21] | summary[37], Is.Zero);
            SetReference(origin, -12); SetReference(second, 123456);
            Dispatch(); Assert.That(feedback[0], Is.EqualTo(1)); Assert.That(feedback[1], Is.EqualTo(1));
            Assert.That(summary[0] | summary[1] | summary[2] | summary[18], Is.Zero,
                "Uniform water is not solid geometry; mixed storage addresses are never GPU slot addresses.");
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            using var surfaces = new NativeArray<ushort>(512, Allocator.Temp);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) voxels[i] = 3;
            _mirror.Publish(VoxelBrickDelta.MixedAt(second, 2, 0), voxels, surfaces, boundaries, 0, true);
            Dispatch(); Assert.That(feedback[0], Is.Zero);
            Assert.That(summary[21], Is.EqualTo(0x03030303u));
            _mirror.InvalidateReadiness(second);
            Dispatch(); Assert.That(feedback[0], Is.EqualTo(1)); Assert.That(feedback[1], Is.EqualTo(1));
            SetReference(second, -1);
            Dispatch(); Assert.That(feedback[0], Is.Zero);
            Assert.That(summary[19] | summary[20] | summary[21] | summary[37], Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(31)]
        [TestCase(63)]
        public void OccupancySliceProvesAirWithoutKeysAndRequiresCurrentOccupiedPayload(int x)
        {
            int3 origin = new(x, 63, -1), second = origin + new int3(1, 0, 0);
            _requests.SetData(new[] { new int4(origin >> 6, 1), new int4(second >> 6, 1) });
            using var occupancy = new ComputeBuffer(256, 4);
            using var missing = new ComputeBuffer(3, 4);
            var bits = new uint[256];
            var feedback = new uint[3];
            var summary = new uint[_summaries.count];
            void SetOccupied(int3 coordinate, bool value)
            {
                // Both region records may refer to the same region; keep their snapshots equal.
                for (int r = 0; r < 2; r++)
                {
                    int3 region = (r == 0 ? origin : second) >> 6;
                    if (!math.all(region == (coordinate >> 6))) continue;
                    int3 local = coordinate - region * 64;
                    int bit = local.x + 64 * local.y, word = r * 128 + bit / 32;
                    if (value) bits[word] |= 1u << (bit & 31);
                    else bits[word] &= ~(1u << (bit & 31));
                }
            }
            void Dispatch()
            {
                occupancy.SetData(bits); missing.SetData(new uint[3]);
                GpuBlockHlodSummary.DispatchSourceRange(_shader, _mirror, _requests, 2,
                    origin, new int3(2, 1, 1), _summaries, 0, missing, 0, occupancy);
                missing.GetData(feedback); _summaries.GetData(summary);
            }
            SetOccupied(origin, true);
            // Stale material must not override authoritative air, even with a live directory key.
            _mirror.Publish(VoxelBrickDelta.UniformAt(second, 1, 200), default, default, default, 0, false);
            Dispatch();
            Assert.That(feedback[0], Is.EqualTo(1)); Assert.That(feedback[1], Is.Zero);
            Assert.That(summary[18], Is.EqualTo(1));
            Assert.That(summary[19] | summary[20] | summary[21] | summary[37], Is.Zero);
            _mirror.Publish(VoxelBrickDelta.UniformAt(origin, 1, 200), default, default, default, 0, false);
            Dispatch(); Assert.That(feedback[0], Is.Zero);
            Assert.That(summary[2], Is.EqualTo(0xc8c8c8c8u));
            SetOccupied(second, true); _mirror.InvalidateReadiness(second);
            Dispatch(); Assert.That(feedback[0], Is.EqualTo(1)); Assert.That(feedback[1], Is.EqualTo(1));
            _mirror.Publish(VoxelBrickDelta.UniformAt(second, 2, 3), default, default, default, 0, false);
            SetOccupied(origin, false);
            Dispatch(); Assert.That(feedback[0], Is.Zero);
            Assert.That(summary[0] | summary[1] | summary[2] | summary[18], Is.Zero);
            Assert.That(summary[21], Is.EqualTo(0x03030303u));
        }

        [TestCase(-1)]
        [TestCase(63)]
        public void SourceRangeFindsMissingKeysAndDistinguishesKnownAirWithoutCpuBrickCoverage(int x)
        {
            _mirror.Dispose();
            _mirror = new GpuVoxelBrickMirror(4, retainKnownEmpty: true);
            int3 origin = new(x, -1, 0), extent = new(2, 1, 1);
            int3 second = origin + new int3(1, 0, 0);
            _requests.SetData(new[] { new int4(origin >> 6, 1), new int4(second >> 6, 1) });
            using var missing = new ComputeBuffer(3, 4);
            var feedback = new uint[3];
            var summary = new uint[2 * GpuBlockHlodSummary.WordsPerBlock];
            void Dispatch()
            {
                missing.SetData(new uint[3]);
                GpuBlockHlodSummary.DispatchSourceRange(_shader, _mirror, _requests, 2,
                    origin, extent, _summaries, 0, missing, 0);
                missing.GetData(feedback);
                _summaries.GetData(summary);
            }
            Dispatch();
            Assert.That(feedback[0], Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new uint[] { 0, 1 }, new[] { feedback[1], feedback[2] });
            Assert.That(summary[18], Is.EqualTo(1));
            Assert.That(summary[37], Is.EqualTo(1));

            _mirror.Publish(VoxelBrickDelta.EmptyAt(origin, 1), default, default, default, 0, false);
            _mirror.Publish(VoxelBrickDelta.UniformAt(second, 1, 200), default, default, default, 0, false);
            Dispatch();
            Assert.That(feedback[0], Is.Zero, "The GPU must recognize uploaded air without CPU per-brick proof.");
            Assert.That(summary[0] | summary[1] | summary[18] | summary[37], Is.Zero);
            Assert.That(summary[19], Is.EqualTo(uint.MaxValue));
            Assert.That(summary[20], Is.EqualTo(uint.MaxValue));
            for (int word = 21; word < 37; word++) Assert.That(summary[word], Is.EqualTo(0xc8c8c8c8u));

            _mirror.InvalidateReadiness(origin);
            _mirror.InvalidateReadiness(second);
            Dispatch();
            Assert.That(feedback[0], Is.EqualTo(2), "Present but pending source keys are not ready.");
            Assert.That(summary[18], Is.EqualTo(1));
            Assert.That(summary[37], Is.EqualTo(1));
            using (var oldRequest = new ComputeBuffer(1, 16))
            {
                oldRequest.SetData(new[] { new int4(second, 0) });
                GpuBlockHlodSummary.Dispatch(_shader, _mirror, oldRequest, _summaries, 1, 0);
                _summaries.GetData(summary);
                Assert.That(summary[0], Is.EqualTo(uint.MaxValue));
                Assert.That(summary[2], Is.EqualTo(0xc8c8c8c8u),
                    "Readiness invalidation must not overwrite data retained by an older reader.");
            }
            _mirror.Publish(VoxelBrickDelta.EmptyAt(origin, 2), default, default, default, 0, false);
            _mirror.Publish(VoxelBrickDelta.UniformAt(second, 2, 200), default, default, default, 0, false);
            Dispatch();
            Assert.That(feedback[0], Is.Zero, "A replacement publication clears pending readiness.");

            _mirror.Remove(second);
            Dispatch();
            Assert.That(feedback[0], Is.EqualTo(1));
            Assert.That(feedback[1], Is.EqualTo(1));
            Assert.That(summary[37], Is.EqualTo(1), "A retired key must not reuse its old summary.");

            // Optional nonresident halo is authorized at region granularity; stale directory
            // content there must not be treated as resident world data.
            _mirror.Publish(VoxelBrickDelta.UniformAt(second, 2, 200), default, default, default, 0, false);
            _requests.SetData(new[] { new int4(origin >> 6, 0), new int4(second >> 6, 0) });
            Dispatch();
            Assert.That(feedback[0], Is.Zero);
            foreach (uint word in summary) Assert.That(word, Is.Zero);
        }

        [Test]
        public void ProductionSizedSourceRangeMatchesExplicitBrickSummariesAcrossRegionBoundaries()
        {
            const int count = 66 * 15;
            int3 origin = new(-1), extent = new(66, 15, 1);
            using var mirror = new GpuVoxelBrickMirror(4, 4096, retainKnownEmpty: true);
            using var explicitRequests = new ComputeBuffer(count, 16);
            using var regions = new ComputeBuffer(6, 16);
            using var expected = new ComputeBuffer(count * GpuBlockHlodSummary.WordsPerBlock, 4);
            using var actual = new ComputeBuffer(count * GpuBlockHlodSummary.WordsPerBlock, 4);
            using var missing = new ComputeBuffer(count + 1, 4);
            var requests = new int4[count];
            for (int i = 0; i < count; i++)
            {
                int3 coordinate = origin + new int3(i % 66, i / 66, 0);
                requests[i] = new int4(coordinate, 0);
                VoxelBrickDelta delta = i % 5 == 0 ? VoxelBrickDelta.EmptyAt(coordinate, 1)
                    : VoxelBrickDelta.UniformAt(coordinate, 1, (byte)(2 + i % 29));
                Assert.That(mirror.Publish(delta, default, default, default, 0, false),
                    Is.EqualTo(GpuBrickPublish.MetadataOnly));
            }
            var regionRecords = new int4[6];
            int next = 0;
            for (int y = -1; y <= 0; y++)
            for (int x = -1; x <= 1; x++) regionRecords[next++] = new int4(x, y, -1, 1);
            explicitRequests.SetData(requests);
            regions.SetData(regionRecords);
            missing.SetData(new uint[count + 1]);
            GpuBlockHlodSummary.Dispatch(_shader, mirror, explicitRequests, expected, count, 0);
            GpuBlockHlodSummary.DispatchSourceRange(_shader, mirror, regions, 6, origin, extent,
                actual, 0, missing, 0);
            var expectedWords = new uint[expected.count];
            var actualWords = new uint[actual.count];
            var feedback = new uint[count + 1];
            expected.GetData(expectedWords); actual.GetData(actualWords); missing.GetData(feedback);
            Assert.That(feedback[0], Is.Zero);
            CollectionAssert.AreEqual(expectedWords, actualWords,
                "GPU coordinate expansion must preserve every ordered occupancy/material summary.");
        }

        [TestCase(1)]
        [TestCase(200)]
        public void UniformSolidPreservesEverySubcellAndPackedMaterial(int material)
        {
            _mirror.Publish(VoxelBrickDelta.UniformAt(Coordinate, 1, (byte)material),
                default, default, default, 0, false);
            uint[] result = Read();
            Assert.That(result[0], Is.EqualTo(uint.MaxValue));
            Assert.That(result[1], Is.EqualTo(uint.MaxValue));
            for (int subcell = 0; subcell < 64; subcell++) Assert.That(Material(result, subcell), Is.EqualTo(material));
            Assert.That(result[18], Is.Zero);
        }

        [Test]
        public void RetiredOrResettingMirrorCannotAdmitCoarseWork()
        {
            _mirror.RetainSubmission();
            try
            {
                _mirror.Clear();
                Assert.Throws<System.InvalidOperationException>(() => Read());
                _mirror.Dispose();
                Assert.Throws<System.ObjectDisposedException>(() => Read());
            }
            finally { _mirror.ReleaseSubmission(); }
        }

        [Test]
        public void MissingSourceIsDistinctFromKnownAirInTheSameBatch()
        {
            _mirror.Publish(VoxelBrickDelta.EmptyAt(Coordinate, 1), default, default, default, 0, false);
            // Empty bricks have no directory entry. Only a held ready-region proof
            // may authorize absence as air; an unresolved request keeps w = 0.
            _requests.SetData(new[] { new int4(Coordinate, 1), new int4(Coordinate + 1, 0) });
            uint[] result = Read(2);
            Assert.That(result[18], Is.Zero);
            Assert.That(result[37], Is.EqualTo(1), "Unresolved source must block downstream publication.");
            Assert.That(result[0] | result[1] | result[19] | result[20], Is.Zero);
        }

        [TestCase(0, 0, 0)]
        [TestCase(7, 7, 7)]
        [TestCase(1, 6, 5)]
        public void OneVoxelFeatureSurvivesCoarseSummarization(int x, int y, int z)
        {
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            voxels[x + 8 * (y + 8 * z)] = 3;
            Publish(voxels);
            uint[] result = Read();
            Assert.That(_mirror.TryGetSlot(Coordinate, out int sourceSlot), Is.True);
            var payload = new uint[128];
            _mirror.Materials.GetData(payload, 0, sourceSlot * 128, 128);
            for (int source = 0; source < 512; source++)
                Assert.That((payload[source / 4] >> ((source % 4) * 8)) & 255u,
                    Is.EqualTo(source == x + 8 * (y + 8 * z) ? 3u : 0u), $"Source voxel {source}");
            int subcell = x / 2 + 4 * (y / 2 + 4 * (z / 2));
            ulong occupied = result[0] | ((ulong)result[1] << 32);
            Assert.That(occupied, Is.EqualTo(1UL << subcell), string.Join(",", result));
            Assert.That(Material(result, subcell), Is.EqualTo(3));
        }

        [Test]
        public void EveryVoxelPositionSurvivesWithoutCreatingNeighbourOccupancy()
        {
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            for (int source = 0; source < 512; source++)
            {
                if (source > 0) voxels[source - 1] = 0;
                voxels[source] = 200;
                Publish(voxels, (uint)source + 1);
                uint[] result = Read();
                int x = source & 7, y = (source >> 3) & 7, z = source >> 6;
                int subcell = x / 2 + 4 * (y / 2 + 4 * (z / 2));
                ulong occupied = result[0] | ((ulong)result[1] << 32);
                Assert.That(occupied, Is.EqualTo(1UL << subcell), $"Source voxel {source}");
                for (int cell = 0; cell < 64; cell++)
                    Assert.That(Material(result, cell), Is.EqualTo(cell == subcell ? 200 : 0),
                        $"Source voxel {source}, subcell {cell}");
                Assert.That(result[18], Is.Zero);
            }
        }

        [Test]
        public void ExposedColumnMajorityWinsOverBuriedMaterialAndOneHigherOutlier()
        {
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            voxels[0] = 2; voxels[1] = 2; voxels[64] = 2; voxels[65] = 2;
            voxels[8] = 3; voxels[9] = 3; voxels[72] = 3; voxels[73] = 4;
            Publish(voxels);
            Assert.That(Material(Read(), 0), Is.EqualTo(3));
        }

        [Test]
        public void EqualVotesKeepFirstExposedColumnAndIgnoreConfiguredWater()
        {
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            voxels[8] = 5; voxels[9] = 4; voxels[72] = 4; voxels[73] = 5;
            Publish(voxels);
            Assert.That(Material(Read(), 0), Is.EqualTo(5));
            Assert.That(Material(Read(waterMask: 1u << 5), 0), Is.EqualTo(4));
        }

        [TestCase(11)]
        [TestCase(16)]
        [TestCase(7)]
        public void UniformWaterDoesNotCreateCoarseSolidOccupancy(int water)
        {
            _mirror.Publish(VoxelBrickDelta.UniformAt(Coordinate, 1, (byte)water), default, default, default, 0, false);
            uint[] result = Read(waterMask: 1u << water);
            for (int word = 0; word < 19; word++) Assert.That(result[word], Is.Zero);
        }

        [Test]
        public void SummaryPortionsSurviveSourceSlotReuseBeyondMirrorCapacity()
        {
            const int portions = 7;
            _mirror.Dispose();
            _mirror = new GpuVoxelBrickMirror(1);
            _summaries.Dispose();
            _summaries = new ComputeBuffer(portions * GpuBlockHlodSummary.WordsPerBlock, 4);
            using var ownedVoxels = new NativeArray<byte>(512, Allocator.Temp);
            var voxels = ownedVoxels;
            using var semantics = new NativeArray<ushort>(512, Allocator.Temp);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
            var result = new uint[_summaries.count];
            for (int portion = 0; portion < portions; portion++)
            {
                int3 coordinate = Coordinate + new int3(portion, 0, 0);
                for (int voxel = 0; voxel < 512; voxel++) voxels[voxel] = (byte)(portion + 1);
                _mirror.Publish(VoxelBrickDelta.MixedAt(coordinate, 1, 0),
                    voxels, semantics, boundaries, 0, true);
                Assert.That(_mirror.TryGetSlot(coordinate, out int slot), Is.True);
                Assert.That(slot, Is.Zero, "Every portion must reuse the single source slot.");
                _requests.SetData(new[] { new int4(coordinate, 0) });
                GpuBlockHlodSummary.Dispatch(_shader, _mirror, _requests, _summaries,
                    1, 0, outputBlockOffset: portion);
                // Test-only completion observation before releasing the source lease. Production
                // orchestration must use ordered completion, never a blocking GetData.
                _summaries.GetData(result);
                _mirror.Remove(coordinate);
            }
            _summaries.GetData(result);
            for (int portion = 0; portion < portions; portion++)
            {
                int start = portion * GpuBlockHlodSummary.WordsPerBlock;
                Assert.That(result[start], Is.EqualTo(uint.MaxValue));
                Assert.That(result[start + 1], Is.EqualTo(uint.MaxValue));
                for (int cell = 0; cell < 64; cell++)
                    Assert.That((result[start + 2 + cell / 4] >> ((cell % 4) * 8)) & 255u,
                        Is.EqualTo(portion + 1), $"portion {portion}, subcell {cell}");
                Assert.That(result[start + 18], Is.Zero);
            }
        }

        [Test]
        public void OffsetPortionPreservesNeighboursAndUnknownSourceFlag()
        {
            var sentinel = new uint[_summaries.count];
            for (int i = 0; i < sentinel.Length; i++) sentinel[i] = 0x12345678u;
            _summaries.SetData(sentinel);
            GpuBlockHlodSummary.Dispatch(_shader, _mirror, _requests, _summaries, 1, 0, 1);
            var result = new uint[_summaries.count];
            _summaries.GetData(result);
            for (int i = 0; i < 19; i++) Assert.That(result[i], Is.EqualTo(sentinel[i]));
            for (int i = 19; i < 37; i++) Assert.That(result[i], Is.Zero);
            Assert.That(result[37], Is.EqualTo(1), "Unknown must survive at the destination offset.");
            // Reusing the shader for the default offset must reset its previous range.
            _requests.SetData(new[] { new int4(Coordinate, 1) });
            GpuBlockHlodSummary.Dispatch(_shader, _mirror, _requests, _summaries, 1, 0);
            _summaries.GetData(result);
            for (int i = 0; i < 19; i++) Assert.That(result[i], Is.Zero);
            Assert.That(result[37], Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void InvalidSummaryDestinationIsRejectedBeforeDispatch(int offset)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                GpuBlockHlodSummary.Dispatch(_shader, _mirror, _requests, _summaries, 1, 0, offset));
        }

        private void Publish(NativeArray<byte> voxels, uint version = 1)
        {
            using var semantics = new NativeArray<ushort>(512, Allocator.Temp);
            using var boundaries = new NativeArray<byte>(512, Allocator.Temp);
            _mirror.Publish(VoxelBrickDelta.MixedAt(Coordinate, version, 0), voxels, semantics, boundaries, 0, true);
        }

        private uint[] Read(int count = 1, uint waterMask = (1u << 11) | (1u << 16))
        {
            GpuBlockHlodSummary.Dispatch(_shader, _mirror, _requests, _summaries, count, waterMask);
            var result = new uint[count * GpuBlockHlodSummary.WordsPerBlock];
            // Test-only observation; production preparation keeps summaries entirely on GPU.
            _summaries.GetData(result, 0, 0, result.Length);
            return result;
        }

        private static uint Material(uint[] words, int subcell) =>
            (words[2 + subcell / 4] >> ((subcell % 4) * 8)) & 255;
    }
}
