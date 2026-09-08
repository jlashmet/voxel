using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    /// <summary>
    /// Regression for allocation-pressure liveness when a replacement generation cannot stage
    /// beside the currently published generation. Test setup writes only bounded GPU bookkeeping;
    /// retirement itself runs through the production pressure compute path.
    /// </summary>
    public sealed class GpuAllocationPressureStaleLiveTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct GeometryRecord
        {
            public uint GenerationLow;
            public uint GenerationHigh;
            public uint Bank;
            public uint VertexCount;
            public uint IndexCount;
            public uint VertexPageCount;
            public uint IndexPageCount;
            public uint Ready;
        }

        private ComputeShader _arenaShader;
        private GpuSurfacePageArena _arena;

        [SetUp]
        public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True,
                "This allocation-pressure regression requires a real compute device.");
            ComputeShader asset = Resources.Load<ComputeShader>("GpuSurfacePageArena");
            Assert.That(asset, Is.Not.Null);
            _arenaShader = Object.Instantiate(asset);
            _arena = new GpuSurfacePageArena(
                _arenaShader,
                GpuSurfacePageArena.VertexPageSize * 4,
                GpuSurfacePageArena.IndexPageSize * 4,
                2);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (_arena != null)
                {
                    // Drain the real compute dispatch before disposing GPU resources.
                    var records = new GeometryRecord[_arena.HandleCapacity];
                    _arena.LiveChunkGeometry.GetData(records);
                }
            }
            finally
            {
                _arena?.Dispose();
                _arena = null;
                if (_arenaShader != null) Object.DestroyImmediate(_arenaShader);
                _arenaShader = null;
            }
        }

        [Test]
        public void AllocationPressureRetiresStaleVisibleLiveGenerationWhileCapacityPressureDoesNot()
        {
            Assert.That(_arena.TryAcquireHandle(out int currentHandle), Is.True);
            Assert.That(_arena.TryAcquireHandle(out int staleHandle), Is.True);

            _arena.QueueGeneration(currentHandle, 1UL, sourceStep: 1, ownerHash: 1);
            // The published geometry below is generation 1, but replacement generation 2 is now
            // desired. This is the exact state left after a replacement allocation exhausts.
            _arena.QueueGeneration(staleHandle, 2UL, sourceStep: 1, ownerHash: 2);
            _arena.FlushHandleCommands(1);

            var live = new GeometryRecord[2];
            live[currentHandle] = new GeometryRecord
            {
                GenerationLow = 1,
                VertexCount = 3,
                IndexCount = 3,
                VertexPageCount = 1,
                IndexPageCount = 1,
                Ready = 1,
            };
            live[staleHandle] = new GeometryRecord
            {
                GenerationLow = 1,
                VertexCount = 3,
                IndexCount = 3,
                VertexPageCount = 1,
                IndexPageCount = 1,
                Ready = 1,
            };
            _arena.LiveChunkGeometry.SetData(live);

            // Both live records are well inside every plane. Current visible geometry must never
            // be a pressure victim, while stale visible geometry is reclaimable only for the
            // allocation-pressure path.
            var bounds = new Vector4[4]
            {
                new Vector4(0, 0, 0, 0), Vector4.one,
                new Vector4(1, 0, 0, 0), Vector4.one,
            };
            _arena.ResidentBounds.SetData(bounds);
            var planes = new Plane[6];
            for (int i = 0; i < planes.Length; i++)
                planes[i] = new Plane(Vector3.up, 1000f);

            using var pressure = new GpuSurfacePressureDispatcher(_arena);
            var outcomes = new uint[GpuSurfacePressureDispatcher.MaximumVictims * 4];

            pressure.Dispatch(_arena.ResidentBounds, planes, Vector3.zero, 2, 2,
                sourceStep: 1, includeStale: false);
            pressure.Outcomes.GetData(outcomes);
            Assert.That(outcomes[0], Is.Zero,
                "Ordinary filtered capacity pressure must preserve visible live geometry.");

            pressure.Dispatch(_arena.ResidentBounds, planes, Vector3.zero, 2, 3,
                includeStale: true);
            pressure.Outcomes.GetData(outcomes);
            Assert.That(outcomes[0], Is.EqualTo(1));
            Assert.That(outcomes[1], Is.EqualTo((uint)staleHandle));
            Assert.That(outcomes[2], Is.EqualTo(1u),
                "Pressure acknowledgement must report the published live generation, not Desired.");
            Assert.That(outcomes[3], Is.Zero);
            Assert.That(outcomes[4], Is.Zero,
                "The current visible generation must remain resident.");

            var observed = new GeometryRecord[2];
            _arena.LiveChunkGeometry.GetData(observed);
            Assert.That(observed[currentHandle].Ready, Is.EqualTo(1u));
            Assert.That(observed[staleHandle].Ready, Is.Zero,
                "Stale live pages must stop pinning replacement capacity under allocation pressure.");
        }
    }
}
