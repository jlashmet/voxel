using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Hero-quality cut-stone primitives for architectural assemblies. These shapes are intentionally
    /// calmer than rubble: load-bearing edges stay precise while exposed faces receive restrained,
    /// deterministic age variation. The result is reusable by arches, arcades, bridges and vaults.
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
            float a0, float a1, float depth, float faceBevel, int seed)
        {
            float radialThickness = Mathf.Max(0.08f, outerRadius - innerRadius);
            float angleSpan = Mathf.Abs(a1 - a0);
            float arcWidth = ((innerRadius + outerRadius) * 0.5f) * angleSpan;
            float bevel = Mathf.Clamp(faceBevel, 0.008f,
                Mathf.Min(radialThickness * 0.13f, arcWidth * 0.16f));

            // The intrados is deliberately disciplined: the opening must read as an authored curve.
            // Only the exposed extrados gets a few millimetres of age variation.
            float inner0 = innerRadius + (Hash(seed + 11) - 0.5f) * radialThickness * 0.006f;
            float inner1 = innerRadius + (Hash(seed + 13) - 0.5f) * radialThickness * 0.006f;
            float outer0 = outerRadius + (Hash(seed + 17) - 0.5f) * radialThickness * 0.024f;
            float outer1 = outerRadius + (Hash(seed + 19) - 0.5f) * radialThickness * 0.024f;

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
                    ? boundary[i] + toward / distance * Mathf.Min(bevel, distance * 0.22f)
                    : boundary[i];
            }

            // One restrained chipped outer corner in a minority of stones. It affects only the
            // face silhouette; the structural bearing surfaces remain regular.
            if (Hash(seed + 101) > 0.84f)
            {
                int corner = Hash(seed + 103) > 0.5f ? 2 : 3;
                Vector2 toward = centroid - face[corner];
                face[corner] += toward.normalized * bevel * (0.32f + Hash(seed + 107) * 0.28f);
            }

            float front = -depth * 0.5f;
            float bevelBack = front + Mathf.Min(bevel * 0.62f, depth * 0.065f);
            float back = depth * 0.5f;

            var vertices = new List<Vector3>(64);
            var triangles = new List<int>(96);

            // Broad hero face: perfectly planar so texture and light read as stone, not a low-poly blob.
            AddQuad(vertices, triangles,
                V(face[0], front), V(face[3], front), V(face[2], front), V(face[1], front));

            // Four narrow face chamfers catch a controlled highlight around each cut stone.
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) & 3;
                AddQuad(vertices, triangles,
                    V(face[i], front), V(face[next], front),
                    V(boundary[next], bevelBack), V(boundary[i], bevelBack));
            }

            // Deep bearing/joint faces. Each quad owns its normals, keeping the radial joints crisp.
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) & 3;
                AddQuad(vertices, triangles,
                    V(boundary[i], bevelBack), V(boundary[next], bevelBack),
                    V(boundary[next], back), V(boundary[i], back));
            }

            // Back plane completes the solid. It is rarely visible but matters when the bay is reused
            // as a freestanding arcade or destruction exposes the rear face.
            AddQuad(vertices, triangles,
                V(boundary[0], back), V(boundary[1], back),
                V(boundary[2], back), V(boundary[3], back));

            Mesh mesh = new Mesh { name = "Hero chamfered architectural voussoir" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
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
