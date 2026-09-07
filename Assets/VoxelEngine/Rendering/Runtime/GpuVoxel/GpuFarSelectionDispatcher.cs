using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Resident tier history and current-camera selection; no admission readback.</summary>
    internal sealed class GpuFarSelectionDispatcher : IDisposable
    {
        private readonly ComputeShader _shader;
        private readonly int _kernel, _count;
        private bool _disposed;
        internal ComputeBuffer PreviousTier { get; }

        internal GpuFarSelectionDispatcher(int count)
        {
            if (count <= 0 || count > 65536) throw new ArgumentOutOfRangeException(nameof(count));
            _count = count;
            _shader = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuFarSelection"));
            try
            {
                _kernel = _shader.FindKernel("CSSelect");
                PreviousTier = new ComputeBuffer(count, 4);
                PreviousTier.SetData(new uint[count]);
            }
            catch { Dispose(); throw; }
        }

        internal void ClearReplacement(ComputeBuffer replacement)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarSelectionDispatcher));
            if (replacement == null || replacement.count < _count || replacement.stride != 4)
                throw new ArgumentException("Replacement buffer must cover the resident source set.");
            int kernel = _shader.FindKernel("CSClearReplacement");
            _shader.SetInt("_Count", _count);
            _shader.SetBuffer(kernel, "_Replacement", replacement);
            _shader.Dispatch(kernel, (_count + 63) / 64, 1, 1);
        }

        internal void Inherit(GpuFarSelectionDispatcher previous, int[] sourceIndices)
        {
            if (_disposed || previous._disposed) throw new ObjectDisposedException(nameof(GpuFarSelectionDispatcher));
            if (sourceIndices == null || sourceIndices.Length != _count)
                throw new ArgumentException("History remap must match the destination source set.");
            foreach (int index in sourceIndices)
                if (index < -1 || index >= previous._count) throw new ArgumentOutOfRangeException(nameof(sourceIndices));
            using var remap = new ComputeBuffer(_count, 4);
            remap.SetData(sourceIndices);
            int kernel = _shader.FindKernel("CSInherit");
            _shader.SetInt("_Count", _count);
            _shader.SetBuffer(kernel, "_SourceIndices", remap);
            _shader.SetBuffer(kernel, "_SourceTier", previous.PreviousTier);
            _shader.SetBuffer(kernel, "_PreviousTier", PreviousTier);
            _shader.Dispatch(kernel, (_count + 63) / 64, 1, 1);
        }

        internal void Select(ComputeBuffer bounds, ComputeBuffer replacement,
                             in FarFeatureSelectionSettings settings, float3 camera, float radius, float voxelSize)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarSelectionDispatcher));
            if (bounds == null || bounds.count < _count || bounds.stride != 32
                || replacement == null || replacement.count < _count || replacement.stride != 4)
                throw new ArgumentException("Selection buffers must cover the resident source set.");
            if (!(radius > 0) || !math.isfinite(radius) || !(voxelSize > 0) || !math.isfinite(voxelSize)
                || !math.all(math.isfinite(camera)) || !(settings.FocalPixels > 0))
                throw new ArgumentOutOfRangeException(nameof(radius));
            float inverse = 1f / voxelSize;
            float3 min = math.floor((camera - radius) * inverse) * voxelSize;
            float3 max = (math.ceil((camera + radius) * inverse) + 1f) * voxelSize;
            _shader.SetInt("_Count", _count);
            _shader.SetVector("_Camera", new Vector4(camera.x, camera.y, camera.z, 0));
            _shader.SetVector("_QueryMin", new Vector4(min.x, min.y, min.z, 0));
            _shader.SetVector("_QueryMax", new Vector4(max.x, max.y, max.z, 0));
            _shader.SetVector("_Detail", settings.DetailThresholds);
            _shader.SetVector("_Horizon", new Vector4(settings.HorizonThresholds.x, settings.HorizonThresholds.y, 0, 0));
            _shader.SetVector("_Caps", new Vector4(settings.DistanceCaps.x, settings.DistanceCaps.y, settings.DistanceCaps.z, 0));
            _shader.SetFloat("_FocalPixels", settings.FocalPixels);
            _shader.SetBuffer(_kernel, "_Bounds", bounds);
            _shader.SetBuffer(_kernel, "_Replacement", replacement);
            _shader.SetBuffer(_kernel, "_PreviousTier", PreviousTier);
            _shader.Dispatch(_kernel, (_count + 63) / 64, 1, 1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            PreviousTier?.Release();
            if (_shader != null) UnityEngine.Object.DestroyImmediate(_shader);
        }
    }
}
