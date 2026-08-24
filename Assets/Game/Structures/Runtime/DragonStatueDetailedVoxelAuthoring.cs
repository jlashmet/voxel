using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// High-detail, plain-voxel dragon sculpt for visual-reference matching. The authoring deliberately
    /// favors readable silhouette and many small authored features over smooth implicit massing: tall
    /// neck, open wings, long muzzle, layered chest plates, articulated claws, dorsal spines and a
    /// sweeping foreground tail. Every occupied sample is still written through the canonical voxel
    /// authoring session and rendered by the normal production surface path.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;
        private const byte Wing = GameMaterialIds.Wood;

        public static readonly int3 LocalMin = new int3(-106, 0, -92);
        public static readonly int3 LocalSize = new int3(212, 174, 198);

        public static void Author(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            AuthorBody(a, origin);
            AuthorNeckAndHead(a, origin);
            AuthorForeleg(a, origin, -1);
            AuthorForeleg(a, origin, 1);
            AuthorRearLeg(a, origin, -1);
            AuthorRearLeg(a, origin, 1);
            AuthorWing(a, origin, -1);
            AuthorWing(a, origin, 1);
            AuthorTail(a, origin);
            AuthorChestArmor(a, origin);
            AuthorDorsalSpines(a, origin);
            AuthorPatina(a, origin);
        }

        private static void AuthorBody(IStructureAuthoringSession a, int3 o)
        {
            // Upright seated body: narrow waist, deep chest, broad haunches.
            Ellipsoid(a, o, new float3(0, 37, 20), new float3(29, 24, 31), Body);
            Ellipsoid(a, o, new float3(0, 60, 5), new float3(24, 31, 25), Body);
            Ellipsoid(a, o, new float3(0, 79, -7), new float3(21, 24, 21), Body);
            Ellipsoid(a, o, new float3(-19, 68, -3), new float3(12, 17, 15), Body);
            Ellipsoid(a, o, new float3(19, 68, -3), new float3(12, 17, 15), Body);

            // Belly shadow removes the toy-like spherical read from the current version.
            Ellipsoid(a, o, new float3(0, 48, -19), new float3(15, 20, 7), Shadow);
        }

        private static void AuthorNeckAndHead(IStructureAuthoringSession a, int3 o)
        {
            // Long, rising S-neck like the reference rather than a short forward stalk.
            Capsule(a, o, new float3(0, 77, -8), new float3(-2, 96, -22), 16f, 13f, Body);
            Capsule(a, o, new float3(-2, 95, -22), new float3(1, 116, -32), 13f, 10f, Body);
            Capsule(a, o, new float3(1, 115, -32), new float3(0, 132, -43), 10f, 8.5f, Body);

            // Narrow wedge-like skull with projecting snout.
            Ellipsoid(a, o, new float3(0, 137, -48), new float3(17, 12, 13), Body);
            Box(a, o, new int3(-12, 130, -67), new int3(25, 10, 20), Body);
            Box(a, o, new int3(-10, 126, -69), new int3(21, 4, 20), Shadow);

            // Carve a clearly open mouth, then rebuild lower jaw rails and chin.
            Box(a, o, new int3(-9, 128, -70), new int3(19, 5, 18), Empty);
            Box(a, o, new int3(-10, 121, -68), new int3(21, 5, 19), Shadow);
            Capsule(a, o, new float3(-10, 124, -55), new float3(-10, 124, -69), 2.4f, 1.5f, Body);
            Capsule(a, o, new float3(10, 124, -55), new float3(10, 124, -69), 2.4f, 1.5f, Body);

            // Brow shelves, cheek bones and nose ridge sharpen the face.
            Capsule(a, o, new float3(-5, 141, -57), new float3(-16, 143, -50), 4.2f, 2f, Shadow);
            Capsule(a, o, new float3(5, 141, -57), new float3(16, 143, -50), 4.2f, 2f, Shadow);
            Capsule(a, o, new float3(0, 140, -54), new float3(0, 137, -70), 7f, 4.2f, Body);
            Ellipsoid(a, o, new float3(-12, 133, -53), new float3(6, 7, 8), Body);
            Ellipsoid(a, o, new float3(12, 133, -53), new float3(6, 7, 8), Body);

            // Eye sockets and tiny bright eyes under the brow.
            Ellipsoid(a, o, new float3(-7, 138, -61), new float3(4, 3, 3), Empty);
            Ellipsoid(a, o, new float3(7, 138, -61), new float3(4, 3, 3), Empty);
            Box(a, o, new int3(-8, 138, -64), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(6, 138, -64), new int3(3, 2, 2), Eye);

            // Nostrils.
            Box(a, o, new int3(-6, 136, -71), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(4, 136, -71), new int3(3, 2, 3), Empty);

            // Teeth along both jaws, sparse but unmistakable at normal camera distance.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 3; i++)
                {
                    float x = (3.2f + i * 3.2f) * s;
                    Capsule(a, o, new float3(x, 132, -61 - i * 2), new float3(x, 127, -62 - i * 2), 1.1f, 0.25f, Horn);
                    Capsule(a, o, new float3(x, 124, -59 - i * 2), new float3(x, 128, -60 - i * 2), 1.0f, 0.25f, Horn);
                }
            }

            // Long swept crown horns; multiple short tapered pieces give a stair-stepped voxel curve.
            AuthorHorn(a, o, -1);
            AuthorHorn(a, o, 1);

            // Side spikes frame the head without merging into the crown horns.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(15 * s, 136, -47), new float3(25 * s, 140, -39), 3f, 0.4f, Horn);
                Capsule(a, o, new float3(14 * s, 131, -45), new float3(23 * s, 130, -36), 2.5f, 0.35f, Horn);
            }
        }

        private static void AuthorHorn(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3[] p =
            {
                new float3(9 * s, 146, -43),
                new float3(14 * s, 154, -37),
                new float3(20 * s, 161, -28),
                new float3(25 * s, 165, -17),
                new float3(27 * s, 162, -7),
            };
            float[] r = { 4.5f, 3.7f, 2.8f, 1.7f, 0.35f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Horn);
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(18 * s, 71, -9);
            float3 elbow = new float3(28 * s, 51, -18);
            float3 wrist = new float3(27 * s, 25, -31);
            float3 palm = new float3(27 * s, 9, -43);

            Capsule(a, o, shoulder, elbow, 9f, 7f, Body);
            Ellipsoid(a, o, elbow, new float3(8, 8, 9), Body);
            Capsule(a, o, elbow, wrist, 7f, 5f, Body);
            Capsule(a, o, wrist, palm, 5f, 4f, Shadow);
            Ellipsoid(a, o, new float3(27 * s, 7, -46), new float3(9, 4, 12), Body);

            // Four separated fingers with long light claws.
            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4f;
                float x = 27 * s + lateral;
                float z = -52 - (i == 1 || i == 2 ? 3 : 0);
                Capsule(a, o, new float3(x, 7, -49), new float3(x + 1.2f * s, 4, z - 8), 1.8f, 0.25f, Horn);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(22 * s, 37, 21);
            float3 knee = new float3(38 * s, 27, 7);
            float3 ankle = new float3(36 * s, 10, -14);

            Ellipsoid(a, o, hip, new float3(18, 17, 21), Body);
            Capsule(a, o, hip, knee, 12f, 8f, Body);
            Ellipsoid(a, o, knee, new float3(10, 9, 11), Shadow);
            Capsule(a, o, knee, ankle, 8f, 5f, Body);
            Ellipsoid(a, o, new float3(35 * s, 7, -19), new float3(12, 5, 16), Body);

            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.5f;
                float x = 35 * s + lateral;
                Capsule(a, o, new float3(x, 7, -25), new float3(x + 1.2f * s, 4, -35 - (i == 1 || i == 2 ? 3 : 0)), 1.9f, 0.25f, Horn);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(16 * s, 82, 7);
            float3 elbow = new float3(47 * s, 113, 16);
            float3 wrist = new float3(87 * s, 123, 23);
            float3 tip = new float3(98 * s, 105, 29);

            Capsule(a, o, root, elbow, 7f, 5f, Body);
            Capsule(a, o, elbow, wrist, 5f, 3.2f, Body);
            Capsule(a, o, wrist, tip, 3.2f, 1.2f, Body);

            // Long finger rays radiate down from the wrist. Their separated tips create the reference's
            // scalloped wing edge instead of the old folded cloak silhouette.
            float3 f0 = new float3(96 * s, 87, 29);
            float3 f1 = new float3(91 * s, 68, 28);
            float3 f2 = new float3(80 * s, 50, 25);
            float3 f3 = new float3(64 * s, 39, 20);
            float3 inner = new float3(30 * s, 48, 12);
            Capsule(a, o, wrist, f0, 3f, 1.0f, Body);
            Capsule(a, o, wrist, f1, 2.8f, 0.9f, Body);
            Capsule(a, o, wrist, f2, 2.6f, 0.8f, Body);
            Capsule(a, o, wrist, f3, 2.4f, 0.7f, Body);

            // Warm membrane panels, each bounded by a visible dark structural ray.
            Membrane(a, o, root, elbow, inner, 1.6f, Shadow);
            Membrane(a, o, elbow, wrist, inner, 1.6f, Wing);
            Membrane(a, o, wrist, f0, f1, 1.4f, Wing);
            Membrane(a, o, wrist, f1, f2, 1.4f, Wing);
            Membrane(a, o, wrist, f2, f3, 1.4f, Wing);
            Membrane(a, o, wrist, f3, inner, 1.4f, Wing);

            // A few plate blocks on the leading edge give a scale/armor cadence at voxel resolution.
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                float3 p = math.lerp(root, wrist, t);
                Box(a, o, (int3)math.round(p + new float3(-2 * s, 2, -2)), new int3(5, 3, 5), Plate);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Large tail wraps from the right haunch across the foreground and finishes left of center.
            float3[] p =
            {
                new float3(14, 39, 31),
                new float3(38, 34, 45),
                new float3(64, 27, 48),
                new float3(84, 19, 35),
                new float3(89, 12, 12),
                new float3(80, 8, -16),
                new float3(59, 7, -39),
                new float3(33, 6, -56),
                new float3(7, 5, -66),
            };
            float[] r = { 14f, 12f, 10f, 8f, 6f, 4.8f, 3.4f, 2.2f, 0.5f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            for (int i = 1; i < p.Length - 2; i++)
            {
                float3 root = p[i] + new float3(0, r[i] * 0.65f, 0);
                Capsule(a, o, root, root + new float3(0, 7 - i * 0.5f, 5), 2.2f, 0.25f, Horn);
            }
        }

        private static void AuthorChestArmor(IStructureAuthoringSession a, int3 o)
        {
            // Layered light chest plates run from throat to belly, a major visual cue in the reference.
            for (int i = 0; i < 9; i++)
            {
                float y = 112 - i * 7.5f;
                float z = -30 + i * 2.6f;
                float halfWidth = 8f + i * 1.15f;
                Box(a, o,
                    new int3((int)-halfWidth, (int)y - 2, (int)z - 5),
                    new int3((int)(halfWidth * 2f) + 1, 4, 7),
                    Plate);
            }
        }

        private static void AuthorDorsalSpines(IStructureAuthoringSession a, int3 o)
        {
            float3[] roots =
            {
                new float3(0, 124, -32),
                new float3(0, 113, -25),
                new float3(0, 101, -17),
                new float3(0, 89, -8),
                new float3(0, 76, 2),
                new float3(0, 64, 13),
                new float3(0, 52, 24),
            };
            for (int i = 0; i < roots.Length; i++)
            {
                float h = 12f - i * 0.8f;
                Capsule(a, o, roots[i], roots[i] + new float3(0, h, 6), 3f - i * 0.2f, 0.25f, Horn);
            }
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            // Small moss accents only; anatomy remains readable.
            Box(a, o, new int3(-12, 82, 9), new int3(8, 3, 8), Moss);
            Box(a, o, new int3(16, 45, 29), new int3(9, 3, 8), Moss);
            Box(a, o, new int3(-49, 109, 15), new int3(8, 3, 7), Moss);
            Box(a, o, new int3(52, 26, 44), new int3(8, 3, 7), Moss);
            Box(a, o, new int3(5, 132, -44), new int3(5, 2, 5), Moss);
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(o.x + x, o.y + y, o.z + z, material);
        }

        private static void Ellipsoid(IStructureAuthoringSession a, int3 o, float3 centre, float3 radius, byte material)
        {
            int3 min = (int3)math.floor(centre - radius - 1f);
            int3 max = (int3)math.ceil(centre + radius + 1f);
            float3 safe = math.max(radius, new float3(0.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + 0.5f, y + 0.5f, z + 0.5f) - centre) / safe;
                if (math.dot(q, q) <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Capsule(
            IStructureAuthoringSession a, int3 o, float3 start, float3 end,
            float startRadius, float endRadius, byte material)
        {
            float maxRadius = math.max(startRadius, endRadius);
            int3 min = (int3)math.floor(math.min(start, end) - maxRadius - 1f);
            int3 max = (int3)math.ceil(math.max(start, end) + maxRadius + 1f);
            float3 axis = end - start;
            float axisLength2 = math.max(0.0001f, math.dot(axis, axis));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float t = math.saturate(math.dot(p - start, axis) / axisLength2);
                float3 closest = start + axis * t;
                float radius = math.lerp(startRadius, endRadius, t);
                float3 d = p - closest;
                if (math.dot(d, d) <= radius * radius)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Membrane(
            IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc,
            float halfThickness, byte material)
        {
            float3 normal = math.normalizesafe(math.cross(vb - va, vc - va), new float3(0, 0, 1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb, vc)) - halfThickness - 1f);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb, vc)) + halfThickness + 1f);
            float3 e0 = vb - va;
            float3 e1 = vc - va;
            float d00 = math.dot(e0, e0);
            float d01 = math.dot(e0, e1);
            float d11 = math.dot(e1, e1);
            float denom = math.max(0.0001f, d00 * d11 - d01 * d01);
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float signed = math.dot(p - va, normal);
                if (math.abs(signed) > halfThickness) continue;
                float3 projected = p - normal * signed;
                float3 v2 = projected - va;
                float d20 = math.dot(v2, e0);
                float d21 = math.dot(v2, e1);
                float v = (d11 * d20 - d01 * d21) / denom;
                float w = (d00 * d21 - d01 * d20) / denom;
                float u = 1f - v - w;
                if (u >= -0.02f && v >= -0.02f && w >= -0.02f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }
    }
}
