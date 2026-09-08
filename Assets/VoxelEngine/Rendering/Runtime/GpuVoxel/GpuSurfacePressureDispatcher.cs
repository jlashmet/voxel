using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Bounded GPU pressure selection and soft retirement; the host retains handle ownership.</summary>
    internal sealed class GpuSurfacePressureDispatcher : IDisposable
    {
        internal const int MaximumVictims = 16;
        private readonly ComputeShader _shader;
        private readonly int _rank, _retire, _groups;
        private readonly GpuSurfacePageArena _arena;
        private readonly ComputeBuffer _candidates;
        private readonly Vector4[] _planes = new Vector4[6];
        internal readonly ComputeBuffer Outcomes;
        private bool _disposed;
        private bool _waiting, _ready, _retryReadback;
        private readonly uint[] _results = new uint[MaximumVictims * 4];
        private readonly Action<AsyncGPUReadbackRequest> _receive;
        internal bool Busy => _waiting || _ready || _retryReadback;

        internal GpuSurfacePressureDispatcher(GpuSurfacePageArena arena)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _receive = Receive;
            var asset = Resources.Load<ComputeShader>("GpuSurfacePressure");
            if (asset == null) throw new InvalidOperationException("Missing GPU pressure shader.");
            _shader = UnityEngine.Object.Instantiate(asset);
            _rank = _shader.FindKernel("CSRank");
            _retire = _shader.FindKernel("CSRetire");
            _groups = (arena.HandleCapacity + 63) / 64;
            _candidates = new ComputeBuffer(_groups * MaximumVictims, 8);
            Outcomes = new ComputeBuffer(MaximumVictims, 16);
            _shader.SetInt("_HandleCount", arena.HandleCapacity);
            _shader.SetInt("_GroupCount", _groups);
            _shader.SetInt("_VertexPageCount", arena.VertexPageCount);
            _shader.SetInt("_IndexPageCount", arena.IndexPageCount);
            _shader.SetInt("_RetirementDelay", GpuSurfacePageArena.RetirementDelayFrames);
            foreach (int kernel in new[] { _rank, _retire })
            {
                _shader.SetBuffer(kernel, "_Live", arena.LiveChunkGeometry);
                _shader.SetBuffer(kernel, "_Candidates", _candidates);
            }
            _shader.SetBuffer(_rank, "_Desired", arena.DesiredGenerations);
            _shader.SetBuffer(_rank, "_Owners", arena.ResidentOwners);
            _shader.SetBuffer(_rank, "_Pending", arena.PendingChunkGeometry);
            _shader.SetBuffer(_retire, "_VertexTable", arena.VertexPageTable);
            _shader.SetBuffer(_retire, "_IndexTable", arena.IndexPageTable);
            _shader.SetBuffer(_retire, "_ArenaState", arena.ArenaState);
            _shader.SetBuffer(_retire, "_RetiredVertices", arena.RetiredVertexPages);
            _shader.SetBuffer(_retire, "_RetiredIndices", arena.RetiredIndexPages);
            _shader.SetBuffer(_retire, "_Outcomes", Outcomes);
        }

        // Bounds are a resident GPU table, two float4s per handle (center, extent).
        // Caller must serialize dispatch/readback and acknowledge identities before reusing Outcomes.
        internal void Dispatch(ComputeBuffer bounds, Plane[] planes, Vector3 camera, int wanted,
            int frame, int sourceStep = 0, int shardIndex = 0, int shardCount = 1,
            bool includeStale = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfacePressureDispatcher));
            if (bounds == null || bounds.count != _arena.HandleCapacity || bounds.stride != 32)
                throw new ArgumentException("Expected resident bounds for every arena handle.", nameof(bounds));
            if (planes == null || planes.Length != 6) throw new ArgumentException("Six frustum planes required.", nameof(planes));
            if (wanted < 1 || wanted > MaximumVictims) throw new ArgumentOutOfRangeException(nameof(wanted));
            if (sourceStep < 0 || shardCount < 1 || shardIndex < 0 || shardIndex >= shardCount)
                throw new ArgumentOutOfRangeException(nameof(shardIndex));
            _shader.SetInt("_FilterStep", sourceStep);
            _shader.SetInt("_FilterShard", shardIndex);
            _shader.SetInt("_FilterShardCount", shardCount);
            _shader.SetInt("_IncludeStale", includeStale ? 1 : 0);
            _arena.FlushHandleCommands(frame);
            for (int i = 0; i < 6; i++) _planes[i] = new Vector4(
                planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            _shader.SetVectorArray("_Planes", _planes);
            _shader.SetVector("_Camera", camera);
            _shader.SetInt("_Wanted", wanted);
            _shader.SetInt("_Epoch", Math.Max(0, frame));
            _shader.SetBuffer(_rank, "_Bounds", bounds);
            _shader.Dispatch(_rank, _groups, 1, 1);
            _shader.Dispatch(_retire, 1, 1, 1);
        }

        internal void Request(Plane[] planes, Vector3 camera, int wanted, int frame,
            int sourceStep = 0, int shardIndex = 0, int shardCount = 1)
        {
            if (Busy) throw new InvalidOperationException("Pressure acknowledgment is still outstanding.");
            // Source step zero is the coordinator's unfiltered allocation-pressure path. A real
            // worker always has a positive LOD step, so filtered capacity pressure preserves the
            // ordinary current-generation/off-screen policy while allocation pressure may also
            // reclaim obsolete live generations that are pinning their own replacements.
            bool includeStale = sourceStep == 0;
            Dispatch(_arena.ResidentBounds, planes, camera, wanted, frame,
                sourceStep, shardIndex, shardCount, includeStale);
            Readback();
        }

        private void Readback()
        {
            _waiting = true;
            _retryReadback = false;
            _arena.RetainSubmission();
            try { AsyncGPUReadback.Request(Outcomes, _receive); }
            catch
            {
                _waiting = false;
                _retryReadback = true;
                _arena.ReleaseSubmission();
                throw;
            }
        }

        private void Receive(AsyncGPUReadbackRequest request)
        {
            try
            {
                _waiting = false;
                if (_disposed) return;
                if (request.hasError) { _retryReadback = true; return; }
                request.GetData<uint>().CopyTo(_results);
                _ready = true;
            }
            finally
            {
                _arena.ReleaseSubmission();
                if (_disposed) ReleaseResources();
            }
        }

        internal bool TryTakeResults(out uint[] results)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfacePressureDispatcher));
            if (_retryReadback) Readback(); // Retry the same identities; never repeat retirement.
            results = _ready ? _results : null;
            if (!_ready) return false;
            _ready = false;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_waiting) ReleaseResources();
        }

        private void ReleaseResources()
        {
            _candidates.Release();
            Outcomes.Release();
            UnityEngine.Object.DestroyImmediate(_shader);
        }
    }
}
