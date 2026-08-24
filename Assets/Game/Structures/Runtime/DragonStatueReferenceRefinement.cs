using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Reference-specific finishing pass for the detailed voxel dragon. This stays separate from the
    /// primary massing authoring so the unrefined detailed version remains easy to reason about while
    /// the acceptance image can chase the concept's sharper head, scalloped wings and warm armor.
    /// </summary>
    public static class DragonStatueReferenceRefinement
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Dirt;
        private const byte Horn = GameMaterialIds.Stone;

        public static void Apply(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));
            RefineHead(a, origin);
            RefineWing(a, origin, -1);
            RefineWing(a, origin, 1);
            RefineChest(a, origin);
            RefineClaws(a, origin, -1);
            RefineClaws(a, origin, 1);
            RefineTail(a, origin);
        }

        private static void RefineHead(IStructureAuthoringSession a, int3 o)
        {
            // Taper the boxy muzzle toward the nose. The base authoring intentionally over-builds the
            // snout so this pass can carve a clean wedge without risking holes at the skull junction.
            for (int z = -70; z <= -52; z++)
            {
                float t = math.saturate((-52f - z) / 18f);
                float halfWidth = math.lerp(10.5f, 6.2f, t);
                for (int y = 128; y <= 141; y++)
                for (int x = -13; x <= 13; x++)
                {
                    if (math.abs(x + 0.5f) > halfWidth)
                        a.Set(o.x + x, o.y + y, o.z + z, Empty);
                }
            }

            // Rebuild a narrow armored nose bridge and pointed chin after the taper cut.
            Capsule(a, o, new float3(0, 139, -53), new float3(0, 136, -72), 6.6f, 3.8f, Body);
            Capsule(a, o, new float3(0, 123, -55), new float3(0, 122, -70), 5.2f, 2.7f, Shadow);

            // Re-open the mouth after rebuilding the bridge/chin, leaving a triangular side profile.
            for (int z = -69; z <= -55; z++)
            {
                int half = 7 - (int)((-55 - z) * 0.18f);
                for (int y = 127; y <= 131; y++)
                for (int x = -half; x <= half; x++)
                    a.Set(o.x + x, o.y + y, o.z + z, Empty);
            }

            // Longer swept cheek spikes and jaw barb seen strongly in the concept silhouette.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(13 * s, 136, -49), new float3(29 * s, 139, -38), 2.8f, 0.25f, Horn);
                Capsule(a, o, new float3(10 * s, 128, -50), new float3(20 * s, 122, -42), 2.2f, 0.25f, Horn);
            }
            Capsule(a, o, new float3(0, 122, -56), new float3(0, 114, -50), 2.5f, 0.25f, Horn);
        }

        private static void RefineWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;

            // Three deep round cuts create the concept's bat-wing scallops. They deliberately cut the
            // membranes first; structural rays are redrawn below so the finger bones remain continuous.
            Ellipsoid(a, o, new float3(91 * s, 81, 28), new float3(10, 10, 7), Empty);
            Ellipsoid(a, o, new float3(83 * s, 62, 27), new float3(11, 10, 7), Empty);
            Ellipsoid(a, o, new float3(71 * s, 46, 23), new float3(11, 9, 7), Empty);

            float3 wrist = new float3(87 * s, 123, 23);
            float3 f0 = new float3(98 * s, 91, 29);
            float3 f1 = new float3(94 * s, 69, 28);
            float3 f2 = new float3(84 * s, 50, 25);
            float3 f3 = new float3(66 * s, 37, 20);
            Capsule(a, o, wrist, f0, 3.1f, 0.8f, Shadow);
            Capsule(a, o, wrist, f1, 2.9f, 0.7f, Shadow);
            Capsule(a, o, wrist, f2, 2.7f, 0.6f, Shadow);
            Capsule(a, o, wrist, f3, 2.5f, 0.5f, Shadow);

            // Pointed outer wing tip.
            Capsule(a, o, new float3(87 * s, 123, 23), new float3(102 * s, 110, 27), 3.0f, 0.25f, Horn);
        }

        private static void RefineChest(IStructureAuthoringSession a, int3 o)
        {
            // Warm overlapping plates from throat to belly. Each lower plate gets wider and projects
            // slightly farther forward, matching the layered gold/stone armor in the concept.
            for (int i = 0; i < 10; i++)
            {
                float y = 116f - i * 7.2f;
                float z = -35f + i * 2.4f;
                int halfWidth = 7 + i;
                int height = i < 4 ? 5 : 6;
                Box(a, o,
                    new int3(-halfWidth, (int)y - height / 2, (int)z - 5),
                    new int3(halfWidth * 2 + 1, height, 8),
                    Plate);
            }
        }

        private static void RefineClaws(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            // Foreclaws sweep toward the viewer/front instead of ending as tiny toe caps.
            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.1f;
                float x = 27 * s + lateral;
                float extra = (i == 1 || i == 2) ? 4f : 1f;
                Capsule(a, o,
                    new float3(x, 7, -51),
                    new float3(x + 1.5f * s, 3.2f, -63 - extra),
                    1.7f, 0.18f, Horn);
            }

            // Rear claws remain visible beneath the haunch instead of disappearing into the floor line.
            for (int i = 0; i < 4; i++)
            {
                float lateral = (i - 1.5f) * 4.6f;
                float x = 35 * s + lateral;
                Capsule(a, o,
                    new float3(x, 7, -25),
                    new float3(x + 1.3f * s, 3.5f, -39 - (i == 1 || i == 2 ? 3 : 0)),
                    1.8f, 0.18f, Horn);
            }
        }

        private static void RefineTail(IStructureAuthoringSession a, int3 o)
        {
            // Stronger alternating tail spines and a split-looking terminal silhouette.
            float3[] roots =
            {
                new float3(76, 13, -13),
                new float3(61, 11, -32),
                new float3(43, 9, -48),
                new float3(24, 8, -59),
            };
            for (int i = 0; i < roots.Length; i++)
                Capsule(a, o, roots[i], roots[i] + new float3(3, 8 - i, 5), 2.2f, 0.2f, Horn);

            Capsule(a, o, new float3(7, 5, -66), new float3(-7, 5, -76), 2.2f, 0.2f, Horn);
            Capsule(a, o, new float3(7, 5, -66), new float3(5, 8, -80), 2.0f, 0.2f, Horn);
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
    }
}
