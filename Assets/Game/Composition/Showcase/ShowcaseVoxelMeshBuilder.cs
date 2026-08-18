using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Showcase
{
    /// <summary>Builds material-separated greedy meshes from a bounded showcase voxel volume.</summary>
    internal static class ShowcaseVoxelMeshBuilder
    {
        private const byte CarveMaterial = 255;

        private struct FaceCell : IEquatable<FaceCell>
        {
            public byte Material;
            public sbyte Sign;
            public bool Equals(FaceCell other) => Material == other.Material && Sign == other.Sign;
        }

        private sealed class Buffers
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
        }

        public static GameObject Build(
            ShowcaseVoxelAuthoringSession volume,
            Transform parent,
            string name,
            float voxelSizeMetres = 0.1f,
            bool carvedVoid = false)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            Dictionary<byte, Buffers> byMaterial = BuildBuffers(volume, voxelSizeMetres, carvedVoid);
            foreach (var pair in byMaterial)
            {
                if (pair.Value.Vertices.Count == 0) continue;
                var child = new GameObject($"material-{pair.Key}");
                child.transform.SetParent(root.transform, false);
                var filter = child.AddComponent<MeshFilter>();
                var renderer = child.AddComponent<MeshRenderer>();

                var mesh = new Mesh { name = $"{name}-material-{pair.Key}" };
                if (pair.Value.Vertices.Count > ushort.MaxValue)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(pair.Value.Vertices);
                mesh.SetNormals(pair.Value.Normals);
                mesh.SetTriangles(pair.Value.Triangles, 0, true);
                mesh.RecalculateBounds();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = CreateMaterial(pair.Key, carvedVoid);
            }
            return root;
        }

        private static Dictionary<byte, Buffers> BuildBuffers(
            ShowcaseVoxelAuthoringSession volume,
            float scale,
            bool carvedVoid)
        {
            var result = new Dictionary<byte, Buffers>();
            int[] dims = { volume.Size.x, volume.Size.y, volume.Size.z };

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                var mask = new FaceCell[dims[u] * dims[v]];
                int[] x = { 0, 0, 0 };
                int[] q = { 0, 0, 0 };
                q[d] = 1;

                for (x[d] = -1; x[d] < dims[d]; x[d]++)
                {
                    int n = 0;
                    for (x[v] = 0; x[v] < dims[v]; x[v]++)
                    for (x[u] = 0; x[u] < dims[u]; x[u]++)
                    {
                        byte a = x[d] >= 0
                            ? Sample(volume, x[0], x[1], x[2], carvedVoid)
                            : (byte)0;
                        byte b = x[d] < dims[d] - 1
                            ? Sample(volume, x[0] + q[0], x[1] + q[1], x[2] + q[2], carvedVoid)
                            : (byte)0;
                        mask[n++] = a != 0 && b == 0
                            ? new FaceCell { Material = a, Sign = 1 }
                            : a == 0 && b != 0
                                ? new FaceCell { Material = b, Sign = -1 }
                                : default;
                    }

                    n = 0;
                    for (int j = 0; j < dims[v]; j++)
                    {
                        for (int i = 0; i < dims[u];)
                        {
                            FaceCell cell = mask[n];
                            if (cell.Material == 0)
                            {
                                i++;
                                n++;
                                continue;
                            }

                            int w = 1;
                            while (i + w < dims[u] && mask[n + w].Equals(cell)) w++;

                            int h = 1;
                            bool done = false;
                            while (j + h < dims[v] && !done)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    if (!mask[n + k + h * dims[u]].Equals(cell))
                                    {
                                        done = true;
                                        break;
                                    }
                                }
                                if (!done) h++;
                            }

                            int[] p = { 0, 0, 0 };
                            p[d] = x[d] + 1;
                            p[u] = i;
                            p[v] = j;
                            int[] du = { 0, 0, 0 };
                            int[] dv = { 0, 0, 0 };
                            du[u] = w;
                            dv[v] = h;
                            AddQuad(result, cell, d, p, du, dv, volume.Min, scale);

                            for (int y = 0; y < h; y++)
                            for (int k = 0; k < w; k++)
                                mask[n + k + y * dims[u]] = default;

                            i += w;
                            n += w;
                        }
                    }
                }
            }
            return result;
        }

        private static byte Sample(ShowcaseVoxelAuthoringSession volume,
            int lx, int ly, int lz, bool carvedVoid)
        {
            int x = volume.Min.x + lx;
            int y = volume.Min.y + ly;
            int z = volume.Min.z + lz;
            if (carvedVoid)
                return volume.WasCarved(x, y, z) ? CarveMaterial : (byte)0;
            return volume.Get(x, y, z);
        }

        private static void AddQuad(Dictionary<byte, Buffers> byMaterial,
            FaceCell cell, int axis, int[] p, int[] du, int[] dv, int3 min, float scale)
        {
            if (!byMaterial.TryGetValue(cell.Material, out Buffers b))
            {
                b = new Buffers();
                byMaterial.Add(cell.Material, b);
            }

            Vector3 P(int[] a) => new Vector3(
                (min.x + a[0]) * scale,
                (min.y + a[1]) * scale,
                (min.z + a[2]) * scale);
            int[] p1 = { p[0] + du[0], p[1] + du[1], p[2] + du[2] };
            int[] p2 = { p1[0] + dv[0], p1[1] + dv[1], p1[2] + dv[2] };
            int[] p3 = { p[0] + dv[0], p[1] + dv[1], p[2] + dv[2] };
            int start = b.Vertices.Count;
            b.Vertices.Add(P(p));
            b.Vertices.Add(P(p1));
            b.Vertices.Add(P(p2));
            b.Vertices.Add(P(p3));

            Vector3 normal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            normal *= cell.Sign;
            b.Normals.Add(normal); b.Normals.Add(normal); b.Normals.Add(normal); b.Normals.Add(normal);
            if (cell.Sign > 0)
            {
                b.Triangles.Add(start); b.Triangles.Add(start + 1); b.Triangles.Add(start + 2);
                b.Triangles.Add(start); b.Triangles.Add(start + 2); b.Triangles.Add(start + 3);
            }
            else
            {
                b.Triangles.Add(start); b.Triangles.Add(start + 2); b.Triangles.Add(start + 1);
                b.Triangles.Add(start); b.Triangles.Add(start + 3); b.Triangles.Add(start + 2);
            }
        }

        private static Material CreateMaterial(byte material, bool carvedVoid)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = $"ShowcaseVoxel-{material}" };
            Color color = carvedVoid ? new Color(0.12f, 0.78f, 1f, 0.72f) : Palette(material);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", carvedVoid ? 0.65f : 0.18f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (carvedVoid)
            {
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", color * 0.55f);
                }
            }
            return m;
        }

        private static Color Palette(byte id)
        {
            unchecked
            {
                uint h = (uint)(id * 2654435761u + 0x9e3779b9u);
                float hue = (h & 1023u) / 1023f;
                float saturation = 0.32f + ((h >> 10) & 255u) / 255f * 0.32f;
                float value = 0.56f + ((h >> 18) & 255u) / 255f * 0.30f;
                return Color.HSVToRGB(hue, saturation, value);
            }
        }
    }
}
