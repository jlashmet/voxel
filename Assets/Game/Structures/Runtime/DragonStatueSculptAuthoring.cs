using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Anatomy-first implicit sculpt for the dragon statue. Unlike the original massing pass, this
    /// authoring starts from an articulated seated skeleton and gives the skull, limbs, folded wings,
    /// and tail independent silhouette control before the secondary scale/detail pass is applied.
    /// </summary>
    public static class DragonStatueSculptAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Core seated anatomy. The pelvis is broad and low; the ribcage is narrower and pitched
            // forward so the neck/head project in front of the wings instead of stacking vertically.
            OrientedEllipsoid(a, origin,
                new float3(0, 35, 17), new float3(27, 21, 27),
                new float3(0, 0.18f, -1), Body);
            OrientedEllipsoid(a, origin,
                new float3(0, 55, 0), new float3(22, 28, 23),
                new float3(0, 0.30f, -1), Body);
            OrientedEllipsoid(a, origin,
                new float3(0, 67, -10), new float3(18, 20, 18),
                new float3(0, 0.55f, -1), Body);

            // Shoulder masses make the forelimbs emerge from a believable girdle rather than from
            // the side of one large sphere.
            OrientedEllipsoid(a, origin,
                new float3(-19, 61, -5), new float3(12, 15, 14),
                new float3(-0.18f, -0.15f, -1), Body);
            OrientedEllipsoid(a, origin,
                new float3(19, 61, -5), new float3(12, 15, 14),
                new float3(0.18f, -0.15f, -1), Body);

            // Neck: an S-shaped chain with successively smaller radii. Overlap is intentional so the
            // production surface extractor receives one continuous organic volume.
            Capsule(a, origin, new float3(0, 66, -9), new float3(0, 78, -18), 15f, 13f, Body);
            Capsule(a, origin, new float3(0, 77, -18), new float3(0, 90, -27), 13f, 10.5f, Body);
            Capsule(a, origin, new float3(0, 89, -27), new float3(0, 99, -34), 10.5f, 8.5f, Body);

            AuthorHead(a, origin);
            AuthorRearLeg(a, origin, -1);
            AuthorRearLeg(a, origin, 1);
            AuthorForeleg(a, origin, -1);
            AuthorForeleg(a, origin, 1);
            AuthorFoldedWing(a, origin, -1);
            AuthorFoldedWing(a, origin, 1);
            AuthorTail(a, origin);
            AuthorDorsalCrest(a, origin);
            AuthorPatina(a, origin);
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Cranium is wide across the brow and short front-to-back, with a separate tapered muzzle.
            OrientedEllipsoid(a, o,
                new float3(0, 101, -37), new float3(17, 13, 14),
                new float3(0, -0.08f, -1), Body);
            RoundedBox(a, o,
                new float3(0, 96, -50), new float3(12.5f, 6.5f, 11.5f),
                new float3(0, -0.10f, -1), 3.2f, Body);

            // Broad angular brow shelves and cheek armor create the recognizable dragon head shape.
            Capsule(a, o, new float3(-5, 105, -45), new float3(-15, 106, -40), 5.2f, 2.7f, Shadow);
            Capsule(a, o, new float3(5, 105, -45), new float3(15, 106, -40), 5.2f, 2.7f, Shadow);
            OrientedEllipsoid(a, o,
                new float3(-12, 96, -40), new float3(7.5f, 9f, 9f),
                new float3(-0.35f, -0.15f, -1), Body);
            OrientedEllipsoid(a, o,
                new float3(12, 96, -40), new float3(7.5f, 9f, 9f),
                new float3(0.35f, -0.15f, -1), Body);

            // Carved eye sockets make the gold eyes sit beneath the brow instead of floating on it.
            OrientedEllipsoid(a, o,
                new float3(-7, 101, -49), new float3(4.4f, 3.1f, 3.3f),
                new float3(-0.12f, -0.12f, -1), Empty);
            OrientedEllipsoid(a, o,
                new float3(7, 101, -49), new float3(4.4f, 3.1f, 3.3f),
                new float3(0.12f, -0.12f, -1), Empty);
            OrientedEllipsoid(a, o,
                new float3(-7, 101, -51), new float3(2.3f, 1.9f, 1.4f),
                new float3(0, 0, -1), Eye);
            OrientedEllipsoid(a, o,
                new float3(7, 101, -51), new float3(2.3f, 1.9f, 1.4f),
                new float3(0, 0, -1), Eye);

            // Open mouth: carve a long wedge-like cavity and then add a distinct lower jaw below it.
            RoundedBox(a, o,
                new float3(0, 91.5f, -55), new float3(9.5f, 3.1f, 9.5f),
                new float3(0, -0.03f, -1), 2.1f, Empty);
            RoundedBox(a, o,
                new float3(0, 86.5f, -53), new float3(9.5f, 3.2f, 10f),
                new float3(0, 0.06f, -1), 2.5f, Shadow);

            // Nose bridge and nostril cuts.
            Capsule(a, o, new float3(0, 99, -48), new float3(0, 97, -61), 8.5f, 5.2f, Body);
            OrientedEllipsoid(a, o, new float3(-4.5f, 98, -61), new float3(2.0f, 1.5f, 2.5f), new float3(0, 0, -1), Empty);
            OrientedEllipsoid(a, o, new float3(4.5f, 98, -61), new float3(2.0f, 1.5f, 2.5f), new float3(0, 0, -1), Empty);

            // Teeth are sparse silhouette/detail cues, not a picket fence.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(7.5f * s, 93.5f, -57), new float3(7f * s, 88.5f, -59), 1.35f, 0.3f, Horn);
                Capsule(a, o, new float3(3.5f * s, 93f, -60), new float3(3.2f * s, 89.3f, -62), 1.0f, 0.25f, Horn);
                Capsule(a, o, new float3(8f * s, 87.5f, -55), new float3(7.4f * s, 91f, -57), 1.1f, 0.25f, Horn);
            }

            // Curved crown horns: each is a short chain, producing a swept-back hook instead of a rod.
            CurvedHorn(a, o, -1);
            CurvedHorn(a, o, 1);

            // Smaller cheek horns pull the head silhouette outward.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(14 * s, 100, -40), new float3(22 * s, 102, -35), 3.3f, 1.2f, Horn);
                Capsule(a, o, new float3(22 * s, 102, -35), new float3(26 * s, 99, -31), 1.2f, 0.35f, Horn);
            }
        }

        private static void CurvedHorn(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 p0 = new float3(9 * s, 108, -34);
            float3 p1 = new float3(14 * s, 116, -29);
            float3 p2 = new float3(18 * s, 121, -20);
            float3 p3 = new float3(18 * s, 119, -11);
            Capsule(a, o, p0, p1, 4.4f, 3.2f, Horn);
            Capsule(a, o, p1, p2, 3.2f, 1.7f, Horn);
            Capsule(a, o, p2, p3, 1.7f, 0.35f, Horn);
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(18 * s, 61, -8);
            float3 elbow = new float3(26 * s, 43, -15);
            float3 wrist = new float3(24 * s, 20, -25);
            float3 palm = new float3(23 * s, 8, -31);

            Capsule(a, o, shoulder, elbow, 9.5f, 7.5f, Body);
            OrientedEllipsoid(a, o, elbow, new float3(8, 8, 9), new float3(0.25f * s, -1, -0.2f), Body);
            Capsule(a, o, elbow, wrist, 7.2f, 5.2f, Body);
            Capsule(a, o, wrist, palm, 5.4f, 4.7f, Shadow);
            OrientedEllipsoid(a, o, new float3(23 * s, 6.5f, -34), new float3(9, 4.5f, 11), new float3(0.10f * s, 0, -1), Body);

            // Three long claws establish a hand rather than a rounded foot blob.
            for (int i = -1; i <= 1; i++)
            {
                float x = (23 + i * 3.8f * side) * s;
                float z = -40 - (i == 0 ? 2.5f : 0f);
                Capsule(a, o, new float3(x, 6.8f, -39), new float3(x + 0.7f * s, 4.8f, z - 6), 1.7f, 0.35f, Horn);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(21 * s, 37, 18);
            float3 knee = new float3(34 * s, 24, 5);
            float3 ankle = new float3(31 * s, 9, -9);

            OrientedEllipsoid(a, o, hip, new float3(18, 17, 20), new float3(0.40f * s, -0.35f, -1), Body);
            Capsule(a, o, hip, knee, 12f, 8.5f, Body);
            OrientedEllipsoid(a, o, knee, new float3(10, 9, 11), new float3(-0.2f * s, -1, -0.25f), Shadow);
            Capsule(a, o, knee, ankle, 8f, 5.5f, Body);
            OrientedEllipsoid(a, o, new float3(30 * s, 6, -13), new float3(12, 5.5f, 15), new float3(-0.08f * s, 0, -1), Body);

            for (int i = -1; i <= 1; i++)
            {
                float x = (30 + i * 4.2f * side) * s;
                Capsule(a, o, new float3(x, 6, -20), new float3(x + 0.7f * s, 4.5f, -28 - (i == 0 ? 2 : 0)), 1.8f, 0.35f, Horn);
            }
        }

        private static void AuthorFoldedWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(15 * s, 70, 7);
            float3 elbow = new float3(38 * s, 94, 14);
            float3 wrist = new float3(65 * s, 91, 18);
            float3 fingerA = new float3(61 * s, 71, 21);
            float3 fingerB = new float3(55 * s, 53, 24);
            float3 fingerC = new float3(43 * s, 38, 24);
            float3 root = new float3(24 * s, 43, 15);

            // Heavy leading edge with a clearly articulated elbow/wrist.
            Capsule(a, o, shoulder, elbow, 7.2f, 5.3f, Body);
            OrientedEllipsoid(a, o, elbow, new float3(7, 7, 8), new float3(s, 0.2f, 0), Shadow);
            Capsule(a, o, elbow, wrist, 5.2f, 3.2f, Body);
            Capsule(a, o, wrist, fingerA, 3.2f, 1.7f, Body);
            Capsule(a, o, wrist, fingerB, 3f, 1.35f, Body);
            Capsule(a, o, wrist, fingerC, 2.8f, 1.0f, Body);

            // Folded wing sheets overlap like a cloak around the torso. Multiple triangles create a
            // faceted stylized surface while the curved trailing silhouette comes from the fingers.
            Membrane(a, o, shoulder, elbow, root, 2.2f, Shadow);
            Membrane(a, o, elbow, fingerC, root, 2.0f, Shadow);
            Membrane(a, o, elbow, wrist, fingerC, 2.0f, Shadow);
            Membrane(a, o, wrist, fingerA, fingerB, 1.8f, Shadow);
            Membrane(a, o, wrist, fingerB, fingerC, 1.8f, Shadow);

            // Scallops cut into the lower trailing edge so the wing never reads as one flat triangle.
            OrientedEllipsoid(a, o, new float3(55 * s, 63, 23), new float3(7.5f, 7, 4), new float3(0, 1, 0), Empty);
            OrientedEllipsoid(a, o, new float3(48 * s, 47, 24), new float3(6.5f, 6, 4), new float3(0, 1, 0), Empty);

            // Three armor ridges on the folded outer wing reinforce the project's stylized look.
            Capsule(a, o, new float3(31 * s, 72, 15), new float3(48 * s, 83, 19), 3.1f, 1.4f, Plate);
            Capsule(a, o, new float3(29 * s, 61, 18), new float3(49 * s, 69, 22), 2.7f, 1.2f, Plate);
            Capsule(a, o, new float3(27 * s, 51, 19), new float3(43 * s, 54, 23), 2.4f, 1.0f, Plate);
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Tail exits behind the right haunch, wraps around the base, then hooks toward the viewer.
            float3[] p =
            {
                new float3(8, 38, 27),
                new float3(27, 31, 40),
                new float3(48, 23, 43),
                new float3(64, 15, 31),
                new float3(68, 10, 11),
                new float3(59, 8, -9),
                new float3(43, 7, -23),
            };
            float[] r = { 13f, 11f, 8.5f, 6.5f, 4.8f, 3.2f, 0.8f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Tail crest follows the curve at a few meaningful silhouette beats.
            Capsule(a, o, new float3(33, 32, 43), new float3(34, 41, 51), 2.8f, 0.35f, Horn);
            Capsule(a, o, new float3(51, 23, 42), new float3(54, 31, 49), 2.3f, 0.35f, Horn);
            Capsule(a, o, new float3(64, 14, 28), new float3(70, 20, 34), 1.8f, 0.3f, Horn);
        }

        private static void AuthorDorsalCrest(IStructureAuthoringSession a, int3 o)
        {
            float3[] roots =
            {
                new float3(0, 92, -24),
                new float3(0, 82, -16),
                new float3(0, 71, -7),
                new float3(0, 60, 4),
                new float3(0, 50, 14),
                new float3(3, 42, 23),
            };
            float[] heights = { 11f, 12f, 13f, 12f, 10f, 8f };
            for (int i = 0; i < roots.Length; i++)
            {
                float3 root = roots[i];
                float3 tip = root + new float3(0, heights[i], 5f + i * 0.4f);
                Capsule(a, o, root, tip, 3.4f - i * 0.28f, 0.35f, Horn);
            }
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            // Restrained asymmetric patches keep the statue stylized without disguising anatomy.
            OrientedEllipsoid(a, o, new float3(-12, 69, 4), new float3(8, 3, 9), new float3(0, 1, 0), Moss);
            OrientedEllipsoid(a, o, new float3(22, 45, 19), new float3(8, 2.8f, 9), new float3(0, 1, 0), Moss);
            OrientedEllipsoid(a, o, new float3(-36, 79, 16), new float3(7, 2.4f, 8), new float3(0, 1, 0), Moss);
            OrientedEllipsoid(a, o, new float3(48, 22, 42), new float3(7, 2.2f, 6), new float3(0, 1, 0), Moss);
        }

        private static void OrientedEllipsoid(
            IStructureAuthoringSession a, int3 o, float3 centre, float3 radius, float3 forward, byte material)
        {
            BuildBasis(forward, out float3 right, out float3 up, out float3 fwd);
            float maxRadius = math.cmax(radius);
            int3 min = (int3)math.floor(centre - maxRadius - 1f);
            int3 max = (int3)math.ceil(centre + maxRadius + 1f);
            float3 safeRadius = math.max(radius, new float3(0.5f));

            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 d = new float3(x + 0.5f, y + 0.5f, z + 0.5f) - centre;
                float3 q = new float3(math.dot(d, right), math.dot(d, up), math.dot(d, fwd)) / safeRadius;
                if (math.dot(q, q) <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void RoundedBox(
            IStructureAuthoringSession a, int3 o, float3 centre, float3 halfSize, float3 forward,
            float cornerRadius, byte material)
        {
            BuildBasis(forward, out float3 right, out float3 up, out float3 fwd);
            float extent = math.cmax(halfSize) + cornerRadius + 1f;
            int3 min = (int3)math.floor(centre - extent);
            int3 max = (int3)math.ceil(centre + extent);
            float3 inner = math.max(halfSize - cornerRadius, new float3(0.1f));

            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 d = new float3(x + 0.5f, y + 0.5f, z + 0.5f) - centre;
                float3 local = new float3(math.dot(d, right), math.dot(d, up), math.dot(d, fwd));
                float3 q = math.abs(local) - inner;
                float outside = math.length(math.max(q, 0f));
                float inside = math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
                if (outside + inside - cornerRadius <= 0f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void BuildBasis(float3 forward, out float3 right, out float3 up, out float3 fwd)
        {
            fwd = math.normalizesafe(forward, new float3(0, 0, -1));
            float3 helper = math.abs(fwd.y) > 0.92f ? new float3(1, 0, 0) : new float3(0, 1, 0);
            right = math.normalizesafe(math.cross(helper, fwd), new float3(1, 0, 0));
            up = math.normalizesafe(math.cross(fwd, right), new float3(0, 1, 0));
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
            float3 minF = math.min(va, math.min(vb, vc)) - halfThickness - 1f;
            float3 maxF = math.max(va, math.max(vb, vc)) + halfThickness + 1f;
            int3 min = (int3)math.floor(minF);
            int3 max = (int3)math.ceil(maxF);
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
