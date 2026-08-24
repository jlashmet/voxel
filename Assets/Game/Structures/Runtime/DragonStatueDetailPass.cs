using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Secondary implicit-detail pass for the dragon statue. It adds negative SDF cuts for the
    /// mouth/nostrils, layered armor-scale masses, wing fingers/scallops, and denser silhouette
    /// spines without changing the stable world-builder object identity.
    /// </summary>
    public static class DragonStatueDetailPass
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Sculpt a readable open mouth into the originally continuous head volume.
            Ellipsoid(a, origin, new float3(0, 90, -52), new float3(10.5f, 4.2f, 10.5f), Empty);
            Capsule(a, origin, new float3(-7, 91, -53), new float3(7, 91, -53), 3f, 3f, Empty);
            Ellipsoid(a, origin, new float3(-4.8f, 96, -61), new float3(2f, 1.6f, 2.4f), Empty);
            Ellipsoid(a, origin, new float3(4.8f, 96, -61), new float3(2f, 1.6f, 2.4f), Empty);

            // Re-establish sharper upper and lower jaw rails around the negative mouth volume.
            Capsule(a, origin, new float3(-10, 93, -50), new float3(-8, 93, -62), 4f, 2.1f, Body);
            Capsule(a, origin, new float3(10, 93, -50), new float3(8, 93, -62), 4f, 2.1f, Body);
            Capsule(a, origin, new float3(-8, 86, -50), new float3(-6, 86, -59), 3f, 1.5f, Shadow);
            Capsule(a, origin, new float3(8, 86, -50), new float3(6, 86, -59), 3f, 1.5f, Shadow);

            for (int i = 0; i < 4; i++)
            {
                float x = -7f + i * (14f / 3f);
                Capsule(a, origin, new float3(x, 91.5f, -56), new float3(x, 87.5f, -57.5f), 1.25f, 0.35f, Horn);
                Capsule(a, origin, new float3(x, 87f, -55.5f), new float3(x, 90f, -56.5f), 1f, 0.3f, Horn);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, origin, new float3(5 * s, 101, -47), new float3(14 * s, 104, -42), 4.5f, 2f, Shadow);
                Capsule(a, origin, new float3(10 * s, 96, -43), new float3(18 * s, 93, -39), 3.8f, 1.2f, Shadow);
                Capsule(a, origin, new float3(11 * s, 89, -40), new float3(17 * s, 85, -38), 3f, 0.8f, Horn);
            }

            // Broad, overlapping breast/neck plates: enough rhythm to read as scales at game distance
            // without turning the statue into an assembly of individual masonry blocks.
            AddPlateRow(a, origin, 84, -30.5f, 3, 6f, 0);
            AddPlateRow(a, origin, 78, -25.5f, 4, 5.5f, 0);
            AddPlateRow(a, origin, 71, -21.5f, 4, 5.7f, 1);
            AddPlateRow(a, origin, 64, -17.5f, 5, 5.4f, 1);
            AddPlateRow(a, origin, 56, -15.5f, 5, 5.8f, 1);
            AddPlateRow(a, origin, 48, -13.5f, 4, 6.2f, 1);

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int j = 0; j < 4; j++)
                {
                    Ellipsoid(a, origin,
                        new float3(18 * s + j * 2.4f * s, 64 - j * 4, -3 + j * 4),
                        new float3(6.5f, 3f, 7f), Shadow);
                    Ellipsoid(a, origin,
                        new float3(20 * s + j * 2f * s, 39 - j * 3, 18 + j * 5),
                        new float3(7f, 2.8f, 7.5f), Shadow);
                }

                // Secondary wing fingers break up the flat membrane and make the silhouette read as
                // bat-like anatomy rather than a pair of triangular sheets.
                float3 shoulder = new float3(19 * s, 72, 4);
                float3 elbow = new float3(43 * s, 96, 8);
                float3 finger1 = new float3(74 * s, 97, 6);
                float3 finger2 = new float3(69 * s, 80, 8);
                float3 finger3 = new float3(61 * s, 63, 11);
                float3 root = new float3(27 * s, 51, 10);
                Capsule(a, origin, elbow, finger1, 3.5f, 1.2f, Body);
                Capsule(a, origin, elbow, finger2, 3.2f, 1.1f, Body);
                Capsule(a, origin, elbow, finger3, 3f, 1f, Body);
                Membrane(a, origin, elbow, finger1, finger2, 1.8f, Shadow);
                Membrane(a, origin, elbow, finger2, finger3, 1.8f, Shadow);
                Membrane(a, origin, shoulder, finger3, root, 1.9f, Shadow);

                // Negative scallops remove the straight trailing-edge read.
                Ellipsoid(a, origin, new float3(68 * s, 72, 10), new float3(8, 7, 4), Empty);
                Ellipsoid(a, origin, new float3(61 * s, 57, 11), new float3(7, 6, 4), Empty);

                for (int i = 0; i < 3; i++)
                {
                    Capsule(a, origin,
                        new float3(s * (6 + i * 3), 87 - i, -46 + i),
                        new float3(s * (10 + i * 4), 78 - i * 2, -44 + i * 2),
                        2f - i * 0.25f, 0.35f, Horn);
                }
            }

            AddSpine(a, origin, new float3(-2, 109, -30), new float3(-2, 119, -24), 3.1f);
            AddSpine(a, origin, new float3(2, 109, -30), new float3(2, 119, -24), 3.1f);
            AddSpine(a, origin, new float3(0, 69, 2), new float3(0, 82, 7), 3.4f);
            AddSpine(a, origin, new float3(0, 59, 13), new float3(0, 70, 19), 3f);
            AddSpine(a, origin, new float3(20, 36, 39), new float3(20, 44, 49), 2.7f);
            AddSpine(a, origin, new float3(44, 24, 43), new float3(46, 31, 51), 2.3f);
            AddSpine(a, origin, new float3(58, 14, 25), new float3(63, 19, 31), 1.9f);
            AddSpine(a, origin, new float3(58, 10, 2), new float3(64, 14, 5), 1.6f);

            // Eyes are last so facial detailing cannot overwrite the emissive-looking accent.
            Ellipsoid(a, origin, new float3(-7, 99, -53), new float3(2.4f, 1.8f, 1.5f), Eye);
            Ellipsoid(a, origin, new float3(7, 99, -53), new float3(2.4f, 1.8f, 1.5f), Eye);
        }

        private static void AddPlateRow(IStructureAuthoringSession a, int3 o, float y, float z, int count, float spread, int pale)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * spread;
                Ellipsoid(a, o, new float3(x, y, z), new float3(3.7f, 2.4f, 4.5f), pale != 0 ? Plate : Shadow);
            }
        }

        private static void AddSpine(IStructureAuthoringSession a, int3 o, float3 root, float3 tip, float radius) =>
            Capsule(a, o, root, tip, radius, 0.35f, Horn);

        private static void Ellipsoid(IStructureAuthoringSession a, int3 o, float3 centre, float3 radius, byte material)
        {
            int3 min = (int3)math.floor(centre - radius - 1f);
            int3 max = (int3)math.ceil(centre + radius + 1f);
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float3 q = (p - centre) / math.max(radius, new float3(0.5f));
                if (math.dot(q, q) <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Capsule(IStructureAuthoringSession a, int3 o, float3 start, float3 end,
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

        private static void Membrane(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc,
            float halfThickness, byte material)
        {
            float3 normal = math.normalize(math.cross(vb - va, vc - va));
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
