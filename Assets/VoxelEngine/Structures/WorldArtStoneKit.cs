using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Reusable masonry vocabulary for high-quality ruined architecture. This layer deliberately
    /// sits above raw primitives: callers ask for ashlar, voussoirs and an arch rather than manually
    /// arranging boxes. Every returned piece exposes semantic sockets so larger procedural systems
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

            // Keep ruin masonry planar. The old beveled-block primitive made repeated courses read
            // like soft pebbles; this mesh keeps six hard faces and only lets the corners wander a
            // few millimetres, which reads as hand-cut/chipped stone at scene distance.
            WorldArtPiece piece = Piece(parent, name + " weathered ashlar", localPosition);
            MeshObject(piece.Transform, name + " cut ashlar mesh",
                CreateCutAshlarMesh(varied, bevel, seed), palette.Get(WorldArtSurfaceRole.Stone));

            piece.Transform.localRotation = Quaternion.Euler(
                (Hash(seed + 19) - 0.5f) * 0.8f,
                (Hash(seed + 23) - 0.5f) * 1.0f,
                (Hash(seed + 29) - 0.5f) * 1.25f);

            piece.AddSocket("bottom", new Vector3(0f, -varied.y * 0.5f, 0f));
            piece.AddSocket("left", new Vector3(-varied.x * 0.5f, 0f, 0f));
            piece.AddSocket("right", new Vector3(varied.x * 0.5f, 0f, 0f));
            piece.AddSocket("front", new Vector3(0f, 0f, -varied.z * 0.5f));
            return piece;
        }

        /// <summary>
        /// A true radial arch stone rather than a rotated rectangular block. The tapered inner
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
            float pierX = halfOpening + nominalBlockSize.x * 0.52f;
            float pierWidth = nominalBlockSize.x * 1.22f;

            // Two ashlar units per course with a wandering bond. The seam moves left/right on
            // alternating rows instead of producing the toy-like single-block tower silhouette.
            // The top of a broken pier loses an exposed outside unit rather than an entire course.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < pierCourses; row++)
                {
                    float y = row * courseH;
                    float seam = ((row & 1) == 0 ? -0.12f : 0.12f) * pierWidth;
                    seam += (Hash(seed + side * 131 + row * 43) - 0.5f) * pierWidth * 0.045f;

                    float leftEdge = -pierWidth * 0.5f;
                    float rightEdge = pierWidth * 0.5f;
                    float leftWidth = seam - leftEdge;
                    float rightWidth = rightEdge - seam;
                    float z = (Hash(seed + row * 41 + side * 13) - 0.5f) * 0.040f;

                    bool omitLeft = broken && row == pierCourses - 1 && side < 0;
                    bool omitRight = broken && row == pierCourses - 1 && side > 0;

                    if (!omitLeft)
                    {
                        float localX = leftEdge + leftWidth * 0.5f;
                        Ashlar(arch.Transform, name + " pier " + side + " row " + row + " L",
                            new Vector3(side * pierX + localX, y, z),
                            new Vector3(Mathf.Max(0.12f, leftWidth - joint), courseH - joint,
                                depth * (0.985f + Hash(seed + row * 59 + side * 17) * 0.025f)),
                            Mathf.Min(0.055f, courseH * 0.10f),
                            seed + row * 107 + side * 211 + 1, palette);
                    }

                    if (!omitRight)
                    {
                        float localX = seam + rightWidth * 0.5f;
                        Ashlar(arch.Transform, name + " pier " + side + " row " + row + " R",
                            new Vector3(side * pierX + localX, y, z - 0.006f),
                            new Vector3(Mathf.Max(0.12f, rightWidth - joint), courseH - joint,
                                depth * (0.98f + Hash(seed + row * 61 + side * 19) * 0.030f)),
                            Mathf.Min(0.055f, courseH * 0.10f),
                            seed + row * 109 + side * 223 + 2, palette);
                    }
                }
            }

            float springY = pierCourses * courseH - courseH * 0.43f;
            float stoneRadial = nominalBlockSize.y * 0.90f;
            float innerRadius = halfOpening;
            float outerRadius = halfOpening + stoneRadial;
            const int voussoirCount = 13;
            const int keystoneIndex = voussoirCount / 2;
            float gapDeg = 1.15f;

            for (int i = 0; i < voussoirCount; i++)
            {
                // The dedicated keystone replaces the centre voussoir instead of overlapping it.
                // Broken arches lose whole stones near the haunches, preserving structural legibility.
                if (i == keystoneIndex) continue;
                if (broken && (i == 1 || i == voussoirCount - 2)) continue;

                float a0 = Mathf.Lerp(180f, 0f, i / (float)voussoirCount);
                float a1 = Mathf.Lerp(180f, 0f, (i + 1) / (float)voussoirCount);
                float localGap = gapDeg * (0.88f + Hash(seed + i * 37) * 0.24f);
                float lo = Mathf.Min(a0, a1) + localGap * 0.5f;
                float hi = Mathf.Max(a0, a1) - localGap * 0.5f;

                float radialJitter = (Hash(seed + 311 + i * 31) - 0.5f) * 0.018f;
                WorldArtPiece stone = Voussoir(arch.Transform, name + " arch stone " + i,
                    new Vector3(0f, springY, radialJitter), innerRadius, outerRadius,
                    lo, hi, depth * (0.985f + Hash(seed + i * 17) * 0.025f), seed + i * 71, palette);
                stone.Transform.localRotation *= Quaternion.Euler(0f,
                    (Hash(seed + i * 101) - 0.5f) * 0.55f,
                    (Hash(seed + i * 103) - 0.5f) * 0.38f);
            }

            // A proud keystone gives the arch a designed focal point. Its deeper outer radius and
            // slight front projection make the crown readable even when the scene is viewed small.
            float keyHalfAngle = 8.2f;
            WorldArtPiece key = Voussoir(arch.Transform, name + " keystone",
                new Vector3(0f, springY + 0.018f, -depth * 0.018f), innerRadius * 0.985f,
                outerRadius + nominalBlockSize.y * 0.14f, 90f - keyHalfAngle,
                90f + keyHalfAngle, depth * 1.06f, seed + 701, palette);
            key.AddSocket("moss", new Vector3(0f, outerRadius + nominalBlockSize.y * 0.11f,
                -depth * 0.50f));

            if (broken)
            {
                // One displaced crown block supplies an intentional jagged termination rather than
                // noise everywhere. The rest of the arch stays calm enough to read architecturally.
                Ashlar(arch.Transform, name + " broken crown", new Vector3(
                        -pierX - nominalBlockSize.x * 0.05f,
                        springY + outerRadius * 0.72f,
                        0.015f),
                    new Vector3(nominalBlockSize.x * 1.12f, nominalBlockSize.y * 1.10f, depth * 0.96f),
                    0.050f, seed + 811, palette).Transform.localRotation = Quaternion.Euler(0f, -2f, -7f);
            }

            arch.AddSocket("opening", new Vector3(0f, springY + innerRadius * 0.35f, 0f));
            arch.AddSocket("crown", new Vector3(0f, springY + outerRadius, 0f));
            arch.AddSocket("keystone", new Vector3(0f, springY + outerRadius, -depth * 0.5f));
            arch.AddSocket("left-base", new Vector3(-pierX, -courseH * 0.5f, 0f));
            arch.AddSocket("right-base", new Vector3(pierX, -courseH * 0.5f, 0f));
            arch.AddSocket("left-pier", new Vector3(-pierX, springY * 0.55f, -depth * 0.5f));
            arch.AddSocket("right-pier", new Vector3(pierX, springY * 0.55f, -depth * 0.5f));
            arch.AddSocket("wall-left", new Vector3(-pierX - pierWidth * 0.55f,
                courseH * 1.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
            arch.AddSocket("wall-right", new Vector3(pierX + pierWidth * 0.55f,
                courseH * 1.5f, 0f), Quaternion.Euler(0f, -90f, 0f));
            return arch;
        }

        private static Mesh CreateCutAshlarMesh(Vector3 size, float bevel, int seed)
        {
            Vector3 h = size * 0.5f;
            float chip = Mathf.Clamp(bevel * 0.22f, 0.0025f,
                Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.022f);

            Vector3[] c = new Vector3[8];
            c[0] = Corner(-h.x, -h.y, -h.z, seed + 1, chip);
            c[1] = Corner( h.x, -h.y, -h.z, seed + 2, chip);
            c[2] = Corner( h.x,  h.y, -h.z, seed + 3, chip);
            c[3] = Corner(-h.x,  h.y, -h.z, seed + 4, chip);
            c[4] = Corner(-h.x, -h.y,  h.z, seed + 5, chip);
            c[5] = Corner( h.x, -h.y,  h.z, seed + 6, chip);
            c[6] = Corner( h.x,  h.y,  h.z, seed + 7, chip);
            c[7] = Corner(-h.x,  h.y,  h.z, seed + 8, chip);

            // Duplicate vertices per face so RecalculateNormals preserves crisp cut planes.
            Vector3[] v =
            {
                c[0], c[3], c[2], c[1], // front
                c[5], c[6], c[7], c[4], // back
                c[4], c[7], c[3], c[0], // left
                c[1], c[2], c[6], c[5], // right
                c[3], c[7], c[6], c[2], // top
                c[4], c[0], c[1], c[5]  // bottom
            };
            int[] tris =
            {
                 0, 1, 2,  0, 2, 3,
                 4, 5, 6,  4, 6, 7,
                 8, 9,10,  8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
            };

            Mesh mesh = new Mesh { name = "WorldArt planar cut ashlar" };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Corner(float x, float y, float z, int seed, float amount)
        {
            // Perturb inward/outward only slightly. Large noise belongs in damage composition, not
            // in the base stone vocabulary, otherwise every block becomes a potato.
            return new Vector3(
                x + (Hash(seed * 3 + 1) - 0.5f) * amount,
                y + (Hash(seed * 3 + 2) - 0.5f) * amount,
                z + (Hash(seed * 3 + 3) - 0.5f) * amount);
        }

        private static Mesh CreateVoussoirMesh(float innerRadius, float outerRadius,
            float a0, float a1, float depth, int seed)
        {
            float front = -depth * 0.5f;
            float back = depth * 0.5f;
            float frontJitter = (Hash(seed + 5) - 0.5f) * depth * 0.020f;
            float backJitter = (Hash(seed + 9) - 0.5f) * depth * 0.014f;
            float radialChip = (outerRadius - innerRadius) * 0.018f;

            float i0 = innerRadius + (Hash(seed + 13) - 0.5f) * radialChip;
            float i1 = innerRadius + (Hash(seed + 17) - 0.5f) * radialChip;
            float o0 = outerRadius + (Hash(seed + 19) - 0.5f) * radialChip;
            float o1 = outerRadius + (Hash(seed + 23) - 0.5f) * radialChip;

            Vector3[] c =
            {
                P(i0, a0, front + frontJitter), P(i1, a1, front),
                P(o1, a1, front - frontJitter), P(o0, a0, front),
                P(i0, a0, back), P(i1, a1, back + backJitter),
                P(o1, a1, back), P(o0, a0, back - backJitter)
            };

            // Duplicate per face to keep the intrados, extrados and radial joints visually crisp.
            Vector3[] v =
            {
                c[0], c[3], c[2], c[1],
                c[4], c[5], c[6], c[7],
                c[0], c[1], c[5], c[4],
                c[3], c[7], c[6], c[2],
                c[0], c[4], c[7], c[3],
                c[1], c[2], c[6], c[5]
            };
            int[] tris =
            {
                 0, 1, 2,  0, 2, 3,
                 4, 5, 6,  4, 6, 7,
                 8, 9,10,  8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
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
