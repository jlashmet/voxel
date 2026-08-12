using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Hero-quality cut-stone primitives for architectural assemblies. Load-bearing edges stay
    /// disciplined while exposed faces receive only restrained deterministic age variation.
    /// </summary>
    public static class WorldArtArchitecturalStone
    {
        public static WorldArtPiece Voussoir(Transform parent, string name, Vector3 localCenter,
            float innerRadius, float outerRadius, float startAngleDeg, float endAngleDeg,
            float depth, float faceBevel, int seed, WorldArtPalette palette)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localCenter;
            WorldArtPiece piece = new WorldArtPiece(root);

            Mesh mesh = CreateChamferedVoussoir(innerRadius, outerRadius,
                startAngleDeg * Mathf.Deg2Rad, endAngleDeg * Mathf.Deg2Rad,
                depth, faceBevel, seed);

            GameObject meshObject = new GameObject(name + " chamfered limestone mesh");
            meshObject.transform.SetParent(root.transform, false);
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = palette.Get(WorldArtSurfaceRole.Stone);

            float middle = (startAngleDeg + endAngleDeg) * 0.5f * Mathf.Deg2Rad;
            float midRadius = (innerRadius + outerRadius) * 0.5f;
            piece.AddSocket("inner", Polar(innerRadius, middle, -depth * 0.5f));
            piece.AddSocket("outer", Polar(outerRadius, middle, -depth * 0.5f));
            piece.AddSocket("face", Polar(midRadius, middle, -depth * 0.5f));
            return piece;
        }

        private static Mesh CreateChamferedVoussoir(float innerRadius, float outerRadius,
            float a0, float a1, float depth, float requestedBevel, int seed)
        {
            float radialThickness = Mathf.Max(0.08f, outerRadius - innerRadius);
            float originalSpan = Mathf.Abs(a1 - a0);

            float overcut = Mathf.Min(originalSpan * 0.035f, 0.17f * Mathf.Deg2Rad);
            if (a0 < a1)
            {
                a0 -= overcut;
                a1 += overcut;
            }
            else
            {
                a0 += overcut;
                a1 -= overcut;
            }

            float angleSpan = Mathf.Abs(a1 - a0);
            float arcWidth = ((innerRadius + outerRadius) * 0.5f) * angleSpan;
            float bevel = Mathf.Clamp(requestedBevel * 0.36f, 0.006f,
                Mathf.Min(radialThickness * 0.030f, arcWidth * 0.055f));

            float inner0 = innerRadius + (Hash(seed + 11) - 0.5f) * radialThickness * 0.003f;
            float inner1 = innerRadius + (Hash(seed + 13) - 0.5f) * radialThickness * 0.003f;
            float outer0 = outerRadius + (Hash(seed + 17) - 0.5f) * radialThickness * 0.014f;
            float outer1 = outerRadius + (Hash(seed + 19) - 0.5f) * radialThickness * 0.014f;

            Vector2[] boundary =
            {
                Polar2(inner0, a0),
                Polar2(inner1, a1),
                Polar2(outer1, a1),
                Polar2(outer0, a0)
            };

            Vector2 centroid = (boundary[0] + boundary[1] + boundary[2] + boundary[3]) * 0.25f;
            Vector2[] face = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 toward = centroid - boundary[i];
                float distance = toward.magnitude;
                face[i] = distance > 0.0001f
                    ? boundary[i] + toward / distance * Mathf.Min(bevel, distance * 0.12f)
                    : boundary[i];
            }

            if (Hash(seed + 101) > 0.93f)
            {
                int corner = Hash(seed + 103) > 0.5f ? 2 : 3;
                Vector2 toward = centroid - face[corner];
                face[corner] += toward.normalized * bevel * (0.18f + Hash(seed + 107) * 0.18f);
            }

            float front = -depth * 0.5f;
            float bevelBack = front + Mathf.Min(bevel * 0.45f, depth * 0.018f);
            float back = depth * 0.5f;

            var vertices = new List<Vector3>(64);
            var triangles = new List<int>(96);

            // Explicit outward normals are critical. The camera sees the -Z face; letting polygon
            // ordering choose +Z made the broad stone face light like the rear of the asset.
            AddQuadFacing(vertices, triangles,
                V(face[0], front), V(face[3], front), V(face[2], front), V(face[1], front),
                Vector3.back);

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) & 3;
                Vector2 edgeMid = (boundary[i] + boundary[next]) * 0.5f;
                Vector2 outward2 = (edgeMid - centroid).normalized;
                Vector3 expected = new Vector3(outward2.x, outward2.y, -0.65f).normalized;
                AddQuadFacing(vertices, triangles,
                    V(face[i], front), V(face[next], front),
                    V(boundary[next], bevelBack), V(boundary[i], bevelBack), expected);
            }

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) & 3;
                Vector2 edgeMid = (boundary[i] + boundary[next]) * 0.5f;
                Vector2 outward2 = (edgeMid - centroid).normalized;
                Vector3 expected = new Vector3(outward2.x, outward2.y, 0f);
                AddQuadFacing(vertices, triangles,
                    V(boundary[i], bevelBack), V(boundary[next], bevelBack),
                    V(boundary[next], back), V(boundary[i], back), expected);
            }

            AddQuadFacing(vertices, triangles,
                V(boundary[0], back), V(boundary[1], back),
                V(boundary[2], back), V(boundary[3], back), Vector3.forward);

            Mesh mesh = new Mesh { name = "Hero chamfered architectural voussoir" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuadFacing(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
            {
                Vector3 swap = b;
                b = d;
                d = swap;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Vector2 Polar2(float radius, float angle)
        {
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private static Vector3 Polar(float radius, float angle, float z)
        {
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
        }

        private static Vector3 V(Vector2 p, float z)
        {
            return new Vector3(p.x, p.y, z);
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
