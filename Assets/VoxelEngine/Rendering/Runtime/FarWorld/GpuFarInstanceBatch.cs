using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    /// <summary>Immutable instance transforms with GPU compaction and indirect draw counts.</summary>
    internal sealed class GpuFarInstanceBatch : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct InstanceTransform
        {
            internal Matrix4x4 ObjectToWorld, WorldToObject;
        }

        private readonly ComputeShader _shader;
        private readonly int _clear, _compact, _write;
        private readonly Mesh _mesh;
        private readonly Material[] _materials;
        private readonly Bounds[] _bounds;
        private readonly Bounds _drawBounds;
        private readonly uint[] _visibility;
        private readonly MaterialPropertyBlock _properties = new();
        private ComputeBuffer _transforms, _visibilityBuffer, _visibleIndices, _count, _arguments;
        private bool _dirty = true, _disposed;
        internal int Count => _visibility.Length;
        internal int VisibleCount { get; private set; }
        internal int VisibilityUploads { get; private set; }
        internal ComputeBuffer Arguments => _arguments;
        internal ComputeBuffer VisibleIndices => _visibleIndices;
        internal ComputeBuffer Transforms => _transforms;

        internal GpuFarInstanceBatch(ComputeShader shader, Mesh mesh, Material[] materials,
                                     IReadOnlyList<FarFeatureInstance> instances)
        {
            if (instances == null || instances.Count == 0) throw new ArgumentException("A nonempty batch is required.");
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            _mesh = mesh;
            _materials = materials;
            _clear = shader.FindKernel("CSClear");
            _compact = shader.FindKernel("CSCompact");
            _write = shader.FindKernel("CSWriteArguments");
            _bounds = new Bounds[instances.Count];
            _visibility = new uint[instances.Count];
            var transforms = new InstanceTransform[instances.Count];
            for (int i = 0; i < instances.Count; i++)
            {
                var source = instances[i];
                Matrix4x4 matrix = Matrix4x4.TRS(source.Position, source.Rotation, source.Scale);
                transforms[i] = new InstanceTransform { ObjectToWorld = matrix, WorldToObject = matrix.inverse };
                _bounds[i] = new Bounds(source.BoundsCenter, source.BoundsExtents * 2);
                if (i == 0) _drawBounds = _bounds[i];
                else _drawBounds.Encapsulate(_bounds[i]);
                _visibility[i] = 1;
            }
            VisibleCount = instances.Count;
            var args = new uint[mesh.subMeshCount * 5];
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                args[submesh * 5] = mesh.GetIndexCount(submesh);
                args[submesh * 5 + 2] = mesh.GetIndexStart(submesh);
                args[submesh * 5 + 3] = mesh.GetBaseVertex(submesh);
            }
            try
            {
                _transforms = new ComputeBuffer(Count, 128);
                _visibilityBuffer = new ComputeBuffer(Count, sizeof(uint));
                _visibleIndices = new ComputeBuffer(Count, sizeof(uint));
                _count = new ComputeBuffer(1, sizeof(uint));
                _arguments = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                _transforms.SetData(transforms);
                _arguments.SetData(args);
                _properties.SetBuffer("_FarTransforms", _transforms);
                _properties.SetBuffer("_FarVisibleIndices", _visibleIndices);
            }
            catch { Dispose(); throw; }
        }

        internal void UpdateReplacement(Func<Bounds, bool> hasReplacement)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarInstanceBatch));
            int visible = 0;
            for (int i = 0; i < Count; i++)
            {
                uint next = hasReplacement != null && hasReplacement(_bounds[i]) ? 0u : 1u;
                _dirty |= _visibility[i] != next;
                _visibility[i] = next;
                visible += (int)next;
            }
            VisibleCount = visible;
        }

        internal void Prepare()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarInstanceBatch));
            if (!_dirty) return;
            _visibilityBuffer.SetData(_visibility);
            VisibilityUploads++;
            _shader.SetInt("_FarInstanceCount", Count);
            _shader.SetInt("_FarSubmeshCount", _mesh.subMeshCount);
            _shader.SetBuffer(_clear, "_FarCount", _count);
            _shader.SetBuffer(_compact, "_FarCount", _count);
            _shader.SetBuffer(_compact, "_FarVisibility", _visibilityBuffer);
            _shader.SetBuffer(_compact, "_FarVisibleIndices", _visibleIndices);
            _shader.SetBuffer(_write, "_FarCount", _count);
            _shader.SetBuffer(_write, "_FarArguments", _arguments);
            _shader.Dispatch(_clear, 1, 1, 1);
            _shader.Dispatch(_compact, (Count + 63) / 64, 1, 1);
            _shader.Dispatch(_write, (_mesh.subMeshCount + 63) / 64, 1, 1);
            _dirty = false;
        }

        internal void RecordDraws(CommandBuffer command)
        {
            if (VisibleCount == 0) return;
            for (int submesh = 0; submesh < _mesh.subMeshCount; submesh++)
                command.DrawMeshInstancedIndirect(_mesh, submesh, _materials[submesh], 0,
                    _arguments, submesh * 5 * sizeof(uint), _properties);
        }

        internal void DrawNow(int layer)
        {
            Prepare();
            if (VisibleCount == 0) return;
            for (int submesh = 0; submesh < _mesh.subMeshCount; submesh++)
                Graphics.DrawMeshInstancedIndirect(_mesh, submesh, _materials[submesh], _drawBounds,
                    _arguments, submesh * 5 * sizeof(uint), _properties,
                    ShadowCastingMode.Off, false, layer);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transforms?.Release(); _visibilityBuffer?.Release(); _visibleIndices?.Release();
            _count?.Release(); _arguments?.Release();
            _transforms = _visibilityBuffer = _visibleIndices = _count = _arguments = null;
        }
    }
}
