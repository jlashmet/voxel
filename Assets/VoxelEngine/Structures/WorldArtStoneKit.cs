using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Reusable masonry vocabulary for high-quality ruined architecture.  This layer deliberately
    /// sits above raw primitives: callers ask for ashlar, voussoirs and an arch rather than manually
    /// arranging boxes.  Every returned piece exposes semantic sockets so larger procedural systems
    /// can attach walls, ivy, rubble and adjacent bays without knowing the construction details.
    /// </summary>
    public static class WorldArtStoneKit
    {
        public static WorldArtPiece Ashlar(Transform parent, string name, Vector3 localPosition,
            Vector3 size, float bevel, int seed, WorldArtPalette palette)
        {
            float sx = 0.97f + Hash(seed + 3) * 0.06f;
            float sy = 0.97f + Hash(seed + 7) * 0.05f;
            float sz = 0.98f + Hash(seed + 11) * 0.04f;
            Vector3 varied = Vector3.Scale(size, new Vector3(sx, sy, sz));

            WorldArtPiece piece = WorldArtKit.BeveledBlock(parent, name + " weathered ashlar",
                localPosition, varied, Mathf.Min(bevel, Mathf.Min(varied.x, varied.y) * 0.18f),
                WorldArtSurfaceRole.Stone, palette);

            piece.Transform.localRotation = Quaternion.Euler(
                (Hash(seed + 19) - 0.5f) * 1.1f,
                (Hash(seed + 23) - 0.5f) * 1.4f,
                (Hash(seed + 29) - 0.5f) * 1.8f);

            piece.AddSocket("bottom", new Vector3(0f, -varied.y * 0.5f, 0f));
            piece.AddSocket("left", new Vector3(-varied.x * 0.5f, 0f, 0f));
            piece.AddSocket("right", new Vector3(varied.x * 0.5f, 0f, 0f));
            piece.AddSocket("front", new Vector3(0f, 0f, -varied.z * 0.5f));
            return piece;
        }

        /// <summary>
        /// A true radial arch stone rather than a rotated rectangular block.  The tapered inner
        /// face and wider outer face make the masonry read immediately as cut voussoir stone.
        /// </summary>
        public static WorldArtPiece Voussoir(Transform parent, string name, Vector3 localCenter,
            float innerRadius, float outerRadius, float startAngleDeg, float endAngleDeg,
            float depth, int seed, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, localCenter);
            Mesh mesh = CreateVoussoirMesh(innerRadius, outerRadius,
                startAngleDeg * Mathf.Deg2Rad, endAngleDeg * Mathf.Deg2Rad, depth, seed);
            MeshObject(piece.Transform, name + " cut stone voussoir", mesh,
                palette.Get(WorldArtSurfaceRole.Stone));

            float middle = (startAngleDeg + endAngleDeg) * 0.5f * Mathf.Deg2Rad;
            float midRadius = (innerRadius + outerRadius) * 0.5f;
            piece.AddSocket("inner", new Vector3(Mathf.Cos(middle) * innerRadius,
                Mathf.Sin(middle) * innerRadius, 0f));
            piece.AddSocket("outer", new Vector3(Mathf.Cos(middle) * outerRadius,
                Mathf.Sin(middle) * outerRadius, 0f));
            piece.AddSocket("face", new Vector3(Mathf.Cos(middle) * midRadius,
                Mathf.Sin(middle) * midRadius, -depth * 0.5f));
            return piece;
        }

        public static WorldArtPiece RuinArch(Transform parent, string name, Vector3 position,
            float halfOpening, int pierCourses, Vector3 nominalBlockSize, float depth,
            bool broken, int seed, WorldArtPalette palette)
        {
            WorldArtPiece arch = Piece(parent, name, position);
            float courseH = nominalBlockSize.y;
            float joint = Mathf.Clamp(courseH * 0.055f, 0.018f, 0.040f);
            float pierX = halfOpening + nominalBlockSize.x * 0.48f;

            // Proper coursed ashlar piers. Alternate long/short faces and stagger the joints so the
            // arch no longer reads as a stack of identical cubes.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < pierCourses; row++)
                {
                    if (broken && side > 0 && row >= pierCourses - 1) continue;
                    bool longCourse = (row & 1) == 0;
                    float width = nominalBlockSize.x * (longCourse ? 1.16f : 0.92f);
                    float y = row * courseH;
                    float x = side * (pierX + (longCourse ? 0.02f : -0.015f));
                    float z = (Hash(seed + row * 41 + side * 13) - 0.5f) * 0.055f;
                    Ashlar(arch.Transform, name + " pier", new Vector3(x, y, z),
                        new Vector3(width - joint, courseH - joint, depth),
                        Mathf.Min(0.075f, courseH * 0.15f), seed + row * 53 + side * 97, palette);
                }
            }

            float springY = pierCourses * courseH - courseH * 0.43f;
            float stoneRadial = nominalBlockSize.y * 0.90f;
            float innerRadius = halfOpening;
            float outerRadius = halfOpening + stoneRadial;
            const int voussoirCount = 13;
            float gapDeg = 1.15f;

            for (int i = 0; i < voussoirCount; i++)
            {
                // Broken arches lose a few whole stones, not arbitrary pixels.  This produces a
                // believable ruin silhouette while retaining enough structure to read as an arch.
                if (broken && (i == 1 || i == voussoirCount - 2)) continue;
                float a0 = Mathf.Lerp(180f, 0f, i / (float)voussoirCount);
                float a1 = Mathf.Lerp(180f, 0f, (i + 1) / (float)voussoirCount);
                float lo = Mathf.Min(a0, a1) + gapDeg * 0.5f;
                float hi = Mathf.Max(a0, a1) - gapDeg * 0.5f;

                float radialJitter = (Hash(seed + 311 + i * 31) - 0.5f) * 0.018f;
                WorldArtPiece stone = Voussoir(arch.Transform, name + " arch stone " + i,
                    new Vector3(0f, springY, radialJitter), innerRadius, outerRadius,
                    lo, hi, depth * (0.985f + Hash(seed + i * 17) * 0.025f), seed + i * 71, palette);
                stone.Transform.localRotation *= Quaternion.Euler(0f,
                    (Hash(seed + i * 101) - 0.5f) * 0.7f,
                    (Hash(seed + i * 103) - 0.5f) * 0.55f);
            }

            // A slightly proud keystone gives the arch a designed focal point rather than a smooth
            // anonymous ring. It is still generated by the same socketable voussoir component.
            float keyHalfAngle = 8.2f;
            WorldArtPiece key = Voussoir(arch.Transform, name + " keystone",
                new Vector3(0f, springY + 0.018f, -0.012f), innerRadius * 0.985f,
                outerRadius + nominalBlockSize.y * 0.12f, 90f - keyHalfAngle,
                90f + keyHalfAngle, depth * 1.045f, seed + 701, palette);
            key.AddSocket("moss", new Vector3(0f, outerRadius + nominalBlockSize.y * 0.10f, -depth * 0.48f));

            if (broken)
            {
                Ashlar(arch.Transform, name + " broken crown", new Vector3(
                        -pierX - nominalBlockSize.x * 0.05f,
                        springY + outerRadius * 0.72f,
                        0.015f),
                    new Vector3(nominalBlockSize.x * 1.12f, nominalBlockSize.y * 1.10f, depth * 0.96f),
                    0.075f, seed + 811, palette).Transform.localRotation = Quaternion.Euler(0f, -2f, -7f);
            }

            arch.AddSocket("opening", new Vector3(0f, springY + innerRadius * 0.35f, 0f));
            arch.AddSocket("crown", new Vector3(0f, springY + outerRadius, 0f));
            arch.AddSocket("keystone", new Vector3(0f, springY + outerRadius, -depth * 0.5f));
            arch.AddSocket("left-base", new Vector3(-pierX, -courseH * 0.5f, 0f));
            arch.AddSocket("right-base", new Vector3(pierX, -courseH * 0.5f, 0f));
            arch.AddSocket("left-pier", new Vector3(-pierX, springY * 0.55f, -depth * 0.5f));
            arch.AddSocket("right-pier", new Vector3(pierX, springY * 0.55f, -depth * 0.5f));
            arch.AddSocket("wall-left", new Vector3(-pierX - nominalBlockSize.x * 0.55f,
                courseH * 1.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
            arch.AddSocket("wall-right", new Vector3(pierX + nominalBlockSize.x * 0.55f,
                courseH * 1.5f, 0f), Quaternion.Euler(0f, -90f, 0f));
            return arch;
        }

        private static Mesh CreateVoussoirMesh(float innerRadius, float outerRadius,
            float a0, float a1, float depth, int seed)
        {
            float front = -depth * 0.5f;
            float back = depth * 0.5f;
            float frontJitter = (Hash(seed + 5) - 0.5f) * depth * 0.025f;
            float backJitter = (Hash(seed + 9) - 0.5f) * depth * 0.018f;

            Vector3[] v =
            {
                P(innerRadius, a0, front + frontJitter), P(innerRadius, a1, front),
                P(outerRadius, a1, front - frontJitter), P(outerRadius, a0, front),
                P(innerRadius, a0, back), P(innerRadius, a1, back + backJitter),
                P(outerRadius, a1, back), P(outerRadius, a0, back - backJitter)
            };

            // Six hard planar faces. The radial taper is the important silhouette; surface shader
            // provides the fine material breakup while these normals retain hand-cut stone facets.
            int[] tris =
            {
                0,2,1, 0,3,2,       // front
                4,5,6, 4,6,7,       // back
                0,1,5, 0,5,4,       // inner
                3,7,6, 3,6,2,       // outer
                0,4,7, 0,7,3,       // start radial
                1,2,6, 1,6,5        // end radial
            };
            Mesh mesh = new Mesh { name = "WorldArt cut-stone voussoir" };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 P(float radius, float angle, float z)
        {
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
        }

        private static WorldArtPiece Piece(Transform parent, string name, Vector3 localPosition)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            return new WorldArtPiece(root);
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
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
