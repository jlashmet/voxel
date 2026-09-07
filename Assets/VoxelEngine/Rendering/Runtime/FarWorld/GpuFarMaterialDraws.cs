using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    // Geometry is stored once per unique production mesh. Visible pages reference it and
    // the persistent instance transform; no per-frame geometry upload or CPU admission.
    internal sealed class GpuFarMaterialDraws : IDisposable
    {
        internal const int IndicesPerPage = 192;
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex { internal Vector3 Position, Normal; }
        private readonly ComputeShader _shader;
        private readonly int _clear, _emit, _clearCount, _countVisible;
        private readonly Material[] _materials;
        private readonly MaterialPropertyBlock[] _properties;
        private readonly ComputeBuffer _vertices, _indices, _transforms, _descriptors, _pages, _arguments;
        private readonly int _descriptorCount;
        private bool _disposed;
        internal int DrawCount => _materials.Length;
        internal ComputeBuffer Arguments => _arguments;
        internal ComputeBuffer Pages => _pages;
        internal ComputeBuffer Indices => _indices;
        internal ComputeBuffer Vertices => _vertices;
        internal ComputeBuffer Transforms => _transforms;
        private readonly int[] _offsets;
        internal int PageOffset(int material) => _offsets[material];
        internal long ResidentBytes { get; }

        internal GpuFarMaterialDraws(IReadOnlyList<GpuFarInstanceBatch> batches)
        {
            if (batches == null || batches.Count == 0) throw new ArgumentException("Nonempty far batches are required.", nameof(batches));
            _shader = Resources.Load<ComputeShader>("GpuFarMaterialCompact");
            _clear = _shader.FindKernel("CSClear"); _emit = _shader.FindKernel("CSEmitPages");
            _clearCount = _shader.FindKernel("CSClearCount"); _countVisible = _shader.FindKernel("CSCountVisible");
            var vertices = new List<Vertex>(); var indices = new List<uint>();
            var meshStarts = new Dictionary<Mesh, int[]>();
            var materials = new List<Material>(); var materialIds = new Dictionary<Material, int>();
            var capacities = new List<int>(); var descriptors = new List<uint4>();
            int firstInstance = 0;
            foreach (var batch in batches)
            {
                Mesh mesh = batch.Mesh;
                if (!meshStarts.TryGetValue(mesh, out int[] starts))
                {
                    foreach (var attribute in mesh.GetVertexAttributes())
                        if ((attribute.attribute != VertexAttribute.Position && attribute.attribute != VertexAttribute.Normal)
                            || attribute.format != VertexAttributeFormat.Float32 || attribute.dimension != 3)
                            throw new InvalidOperationException("Far atlas vertex layout requires a matching shader reader.");
                    starts = new int[mesh.subMeshCount]; meshStarts.Add(mesh, starts);
                    int vertexStart = vertices.Count;
                    var positions = mesh.vertices; var normals = mesh.normals;
                    if (positions.Length != normals.Length) throw new InvalidOperationException("Far mesh normals are required.");
                    for (int i = 0; i < positions.Length; i++) vertices.Add(new Vertex { Position = positions[i], Normal = normals[i] });
                    for (int submesh = 0; submesh < starts.Length; submesh++)
                    {
                        if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                            throw new InvalidOperationException("Far atlas requires triangle topology.");
                        starts[submesh] = indices.Count;
                        // GetIndices applies the production submesh base vertex.
                        foreach (int index in mesh.GetIndices(submesh)) indices.Add(checked((uint)(vertexStart + index)));
                    }
                }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    int count = checked((int)mesh.GetIndexCount(submesh));
                    if (count == 0) continue;
                    Material material = batch.MaterialAt(submesh);
                    if (!materialIds.TryGetValue(material, out int id))
                    { id = materials.Count; materials.Add(material); materialIds.Add(material, id); capacities.Add(0); }
                    capacities[id] = checked(capacities[id] + ((count + IndicesPerPage - 1) / IndicesPerPage) * batch.Count);
                    descriptors.Add(new uint4((uint)firstInstance, (uint)batch.Count, (uint)starts[submesh], (uint)count));
                    descriptors.Add(new uint4((uint)id, 0, 0, 0));
                }
                firstInstance = checked(firstInstance + batch.Count);
            }
            _materials = materials.ToArray(); _properties = new MaterialPropertyBlock[materials.Count];
            _descriptorCount = descriptors.Count / 2;
            var offsets = new int[materials.Count]; _offsets = offsets; int pageCapacity = 0;
            for (int i = 0; i < offsets.Length; i++) { offsets[i] = pageCapacity; pageCapacity = checked(pageCapacity + capacities[i]); }
            for (int i = 1; i < descriptors.Count; i += 2)
            { var d = descriptors[i]; d.y = (uint)offsets[d.x]; descriptors[i] = d; }
            var args = new uint[Math.Max(4, materials.Count * 4)];
            for (int i = 0; i < materials.Count; i++) args[i * 4] = IndicesPerPage;
            try
            {
                _vertices = new ComputeBuffer(Math.Max(1, vertices.Count), 24);
                _indices = new ComputeBuffer(Math.Max(1, indices.Count), 4);
                _transforms = new ComputeBuffer(firstInstance, 128);
                _descriptors = new ComputeBuffer(Math.Max(1, descriptors.Count), 16);
                _pages = new ComputeBuffer(Math.Max(1, pageCapacity), 16);
                _arguments = new ComputeBuffer(args.Length, 4, ComputeBufferType.IndirectArguments);
                if (vertices.Count > 0) _vertices.SetData(vertices);
                if (indices.Count > 0) _indices.SetData(indices);
                if (descriptors.Count > 0) _descriptors.SetData(descriptors);
                _arguments.SetData(args);
                int copy = _shader.FindKernel("CSCopyTransforms"), start = 0;
                foreach (var batch in batches)
                {
                    _shader.SetInt("_CopyCount", batch.Count); _shader.SetInt("_CopyStart", start);
                    _shader.SetBuffer(copy, "_CopySource", batch.Transforms); _shader.SetBuffer(copy, "_CopyDestination", _transforms);
                    _shader.Dispatch(copy, (batch.Count + 63) / 64, 1, 1); start += batch.Count;
                }
                for (int i = 0; i < materials.Count; i++)
                {
                    var p = new MaterialPropertyBlock(); _properties[i] = p;
                    p.SetBuffer("_FarAtlasVertices", _vertices); p.SetBuffer("_FarAtlasIndices", _indices);
                    p.SetBuffer("_FarTransforms", _transforms); p.SetBuffer("_FarDrawPages", _pages);
                    p.SetInteger("_FarPageOffset", offsets[i]);
                }
                ResidentBytes = (long)_vertices.count * 24 + (long)_indices.count * 4 + (long)firstInstance * 128
                    + (long)_descriptors.count * 16 + (long)_pages.count * 16 + (long)args.Length * 4;
                Debug.Log($"GPU_FAR_MATERIAL batches={batches.Count} draws={DrawCount} instances={firstInstance} uniqueMeshes={meshStarts.Count} pages={pageCapacity} residentBytes={ResidentBytes}");
            }
            catch { Dispose(); throw; }
        }

        internal void Prepare(ComputeBuffer replacement)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarMaterialDraws));
            _shader.SetInt("_MaterialCount", _materials.Length);
            _shader.SetBuffer(_clear, "_Arguments", _arguments);
            if (_materials.Length > 0) _shader.Dispatch(_clear, (_materials.Length + 63) / 64, 1, 1);
            _shader.SetInt("_DescriptorCount", _descriptorCount);
            _shader.SetBuffer(_emit, "_Arguments", _arguments); _shader.SetBuffer(_emit, "_Descriptors", _descriptors);
            _shader.SetBuffer(_emit, "_Replacement", replacement); _shader.SetBuffer(_emit, "_Pages", _pages);
            if (_descriptorCount > 0) _shader.Dispatch(_emit, Math.Min(1024, _descriptorCount), (_descriptorCount + 1023) / 1024, 1);
        }
        internal void CountVisible(ComputeBuffer replacement, ComputeBuffer count, bool includeNear = false)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarMaterialDraws));
            _shader.SetInt("_IncludeNear", includeNear ? 1 : 0);
            _shader.SetBuffer(_clearCount, "_VisibleCount", count);
            _shader.Dispatch(_clearCount, 1, 1, 1);
            _shader.SetInt("_InstanceCount", replacement.count);
            _shader.SetBuffer(_countVisible, "_Replacement", replacement);
            _shader.SetBuffer(_countVisible, "_VisibleCount", count);
            _shader.Dispatch(_countVisible, (replacement.count + 63) / 64, 1, 1);
        }
        internal void Record(CommandBuffer command)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuFarMaterialDraws));
            for (int i = 0; i < _materials.Length; i++)
            {
                var keyword = new LocalKeyword(_materials[i].shader, "VOXEL_FAR_PAGED_DRAW");
                command.SetKeyword(_materials[i], keyword, true);
                command.DrawProceduralIndirect(Matrix4x4.identity, _materials[i], 0, MeshTopology.Triangles,
                    _arguments, i * 4 * sizeof(uint), _properties[i]);
                command.SetKeyword(_materials[i], keyword, false);
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _vertices?.Release(); _indices?.Release(); _transforms?.Release();
            _descriptors?.Release(); _pages?.Release(); _arguments?.Release();
        }
    }
}
