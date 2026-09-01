using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.MeshVoxelization.Editor
{
    /// <summary>
    /// Authoring-only bridge from ordinary Unity mesh hierarchies to the engine-independent
    /// deterministic mesh voxelizer. Child transforms are flattened into root-local coordinates;
    /// the root transform remains the single source transform consumed by <see cref="MeshVoxelizer"/>.
    /// </summary>
    public static class UnityMeshVoxelizationAdapter
    {
        public static MeshVoxelizationSource BuildSource(
            GameObject root,
            byte fallbackMaterial,
            Func<Material, byte> mapMaterial = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (fallbackMaterial == 0)
                throw new ArgumentOutOfRangeException(nameof(fallbackMaterial));

            var vertices = new List<float3>(16_384);
            var triangles = new List<MeshVoxelTriangle>(32_768);
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter filter = meshFilters[i];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Material[] materials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
                Matrix4x4 meshToRoot = worldToRoot * filter.transform.localToWorldMatrix;
                AppendMesh(mesh, meshToRoot, materials, fallbackMaterial, mapMaterial,
                           vertices, triangles);
            }

            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinned[i];
                if (renderer.sharedMesh == null) continue;
                var baked = new Mesh { name = renderer.sharedMesh.name + "_MeshVoxelBake" };
                try
                {
                    renderer.BakeMesh(baked);
                    Matrix4x4 meshToRoot = worldToRoot * renderer.transform.localToWorldMatrix;
                    AppendMesh(baked, meshToRoot, renderer.sharedMaterials,
                               fallbackMaterial, mapMaterial, vertices, triangles);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }

            if (vertices.Count == 0 || triangles.Count == 0)
                throw new InvalidOperationException(
                    $"'{root.name}' does not contain any triangle mesh geometry.");

            return new MeshVoxelizationSource(
                vertices.ToArray(),
                triangles.ToArray(),
                ToFloat4x4(root.transform.localToWorldMatrix));
        }

        private static void AppendMesh(
            Mesh mesh,
            Matrix4x4 meshToRoot,
            Material[] materials,
            byte fallbackMaterial,
            Func<Material, byte> mapMaterial,
            List<float3> vertices,
            List<MeshVoxelTriangle> triangles)
        {
            Vector3[] sourceVertices;
            try
            {
                sourceVertices = mesh.vertices;
            }
            catch (UnityException exception)
            {
                throw new InvalidOperationException(
                    $"Mesh '{mesh.name}' is not readable. Enable Read/Write for authoring-time voxelization.",
                    exception);
            }

            int vertexOffset = vertices.Count;
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 p = meshToRoot.MultiplyPoint3x4(sourceVertices[i]);
                vertices.Add(new float3(p.x, p.y, p.z));
            }

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                int[] indices = mesh.GetIndices(submesh, false);
                if ((indices.Length % 3) != 0)
                    throw new InvalidOperationException(
                        $"Triangle submesh {submesh} on '{mesh.name}' has a non-triple index count.");

                Material material = submesh < materials.Length ? materials[submesh] : null;
                byte materialId = mapMaterial != null && material != null
                    ? mapMaterial(material)
                    : fallbackMaterial;
                if (materialId == 0) materialId = fallbackMaterial;

                for (int index = 0; index < indices.Length; index += 3)
                {
                    int a = indices[index];
                    int b = indices[index + 1];
                    int c = indices[index + 2];
                    if ((uint)a >= (uint)sourceVertices.Length
                        || (uint)b >= (uint)sourceVertices.Length
                        || (uint)c >= (uint)sourceVertices.Length)
                        throw new InvalidOperationException(
                            $"Triangle submesh {submesh} on '{mesh.name}' contains an invalid index.");

                    triangles.Add(new MeshVoxelTriangle(
                        vertexOffset + a,
                        vertexOffset + b,
                        vertexOffset + c,
                        materialId));
                }
            }
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix) => new(
            new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
            new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
            new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
            new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
    }
}
