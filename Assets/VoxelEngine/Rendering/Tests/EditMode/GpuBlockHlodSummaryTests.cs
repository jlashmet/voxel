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
