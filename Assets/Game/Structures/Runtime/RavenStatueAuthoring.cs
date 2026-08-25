using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministic high-resolution voxel sculpt of a perched raven. The silhouette is authored
    /// from overlapping implicit volumes and thin tapered feather ribbons so no mesh or baked voxel
    /// asset is required.
    /// </summary>
    public static class RavenStatueAuthoring
    {
        public static readonly int3 LocalMin = new int3(-42, 0, -42);
        public static readonly int3 LocalSize = new int3(84, 80, 88);

        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Highlight = GameMaterialIds.Stone;
        private const byte Branch = GameMaterialIds.Wood;
        private const byte Weathering = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));

            AuthorPerch(authoring, origin);
            AuthorBody(authoring, origin);
            AuthorHead(authoring, origin);
            AuthorWing(authoring, origin, 1);
            AuthorWing(authoring, origin, -1);
            AuthorTail(authoring, origin);
            AuthorLegsAndTalons(authoring, origin);
            AuthorFeatherTexture(authoring, origin);
        }

        private static void AuthorPerch(IStructureAuthoringSession a, int3 o)
        {
            Capsule(a, o, new float3(-39, 11, 10), new float3(37, 13, 2), 5.4f, 4.2f, Branch);
            Capsule(a, o, new float3(-29, 11, 9), new float3(-39, 20, 14), 3.3f, 0.8f, Branch);
            Capsule(a, o, new float3(24, 13, 4), new float3(36, 22, 10), 3.1f, 0.7f, Branch);
            Capsule(a, o, new float3(9, 12, 5), new float3(18, 7, -2), 2.2f, 0.6f, Branch);

            // Sparse lichened patches break up the branch without turning it into a moss prop.
            OrientedEllipsoid(a, o, new float3(-23, 15, 8), new float3(7, 1.5f, 4), new float3(1, 0.05f, -0.1f), Weathering);
            OrientedEllipsoid(a, o, new float3(27, 16, 5), new float3(5, 1.2f, 3), new float3(1, 0.15f, 0.2f), Weathering);
        }

        private static void AuthorBody(IStructureAuthoringSession a, int3 o)
        {
            // Pear-shaped breast and back, pitched forward like the reference bird rather than a
            // vertical ornamental statue.
            OrientedEllipsoid(a, o, new float3(0, 36, 4), new float3(15.5f, 21.5f, 15.5f), new float3(0.08f, 0.20f, -1), Body);
            OrientedEllipsoid(a, o, new float3(1, 48, -2), new float3(13.5f, 16.5f, 13.5f), new float3(0.10f, 0.28f, -1), Body);
            OrientedEllipsoid(a, o, new float3(-2, 31, 11), new float3(13, 15, 13), new float3(-0.05f, 0.05f, 1), Shadow);

            // Chest highlight is deliberately narrow: it reads as blue-black feather sheen in the
            // material renderer instead of a contrasting belly patch.
            OrientedEllipsoid(a, o, new float3(7, 40, -8), new float3(3.2f, 11, 7), new float3(0.25f, 0.15f, -1), Highlight);
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            OrientedEllipsoid(a, o, new float3(3, 59, -10), new float3(11.5f, 10.5f, 11), new float3(0.20f, -0.03f, -1), Body);
            OrientedEllipsoid(a, o, new float3(5, 63, -13), new float3(10.5f, 6.5f, 9.5f), new float3(0.20f, -0.10f, -1), Shadow);

            // Heavy supraorbital brow is a key raven cue.
            Capsule(a, o, new float3(-2, 64, -18), new float3(10, 64, -20), 3.0f, 2.2f, Shadow);

            // Long, deep, gently hooked beak. Two tapered sections avoid the blunt cone look.
            TaperedRibbon(a, o, new float3(4, 59, -18), new float3(9, 57, -32), 6.2f, 0.8f, 4.0f, 0.8f, Shadow);
            TaperedRibbon(a, o, new float3(7, 57, -28), new float3(10, 53, -36), 3.4f, 0.35f, 2.4f, 0.35f, Shadow);
            TaperedRibbon(a, o, new float3(4, 55.5f, -19), new float3(9, 53.5f, -31), 4.5f, 0.5f, 2.0f, 0.4f, Highlight);

            // Bright pinprick eye sits under the brow on the camera-facing side; a smaller far eye
            // keeps the head readable when the object is rotated in World Builder.
            OrientedEllipsoid(a, o, new float3(10, 61.5f, -19), new float3(1.7f, 1.7f, 1.4f), new float3(0.25f, 0, -1), Eye);
            OrientedEllipsoid(a, o, new float3(-3.5f, 61.5f, -19), new float3(1.2f, 1.4f, 1.2f), new float3(-0.20f, 0, -1), Eye);

            // Ragged throat hackles create the characteristic shaggy raven neck silhouette.
            for (int i = 0; i < 5; i++)
            {
                float x = -7f + i * 3.5f;
                float z = -15f + math.abs(i - 2) * 1.2f;
                TaperedRibbon(a, o, new float3(x, 53, z), new float3(x + 0.5f, 44 - i % 2, z - 4),
                    2.3f, 0.35f, 2.0f, 0.35f, Shadow);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            byte baseMaterial = side > 0 ? Shadow : Body;

            OrientedEllipsoid(a, o,
                new float3(11 * s, 39, 7), new float3(5.5f, 17.5f, 13),
                new float3(0.18f * s, -0.48f, 1), baseMaterial);

            // Layered coverts: short broad blades overlap from shoulder toward the primaries.
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = (8.5f + row * 1.7f) * s;
                    float3 root = new float3(x, 49 - row * 4 - i * 2.1f, 0 + i * 1.8f);
                    float3 tip = new float3((12 + i * 0.5f) * s, 39 - row * 4 - i * 2.6f, 10 + i * 2.4f);
                    byte material = ((row + i) & 1) == 0 ? Body : Shadow;
                    TaperedRibbon(a, o, root, tip, 3.6f, 0.45f, 2.1f, 0.35f, material);
                }
            }

            // Long folded primaries form a pointed cloak along the flank.
            for (int i = 0; i < 7; i++)
            {
                float x = (10.5f + (i - 3) * 0.65f) * s;
                float3 root = new float3(x, 43 - i * 1.15f, 7 + i * 1.4f);
                float3 tip = new float3((11.5f + (i - 3) * 0.9f) * s, 20 + i * 0.65f, 23 + i * 2.0f);
                byte material = i % 3 == 1 ? Highlight : (i % 2 == 0 ? Shadow : Body);
                TaperedRibbon(a, o, root, tip, 3.2f, 0.35f, 1.8f, 0.3f, material);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Seven independently tapered rectrices make a real fanned tail instead of one wedge.
            for (int i = -3; i <= 3; i++)
            {
                float spread = i * 2.4f;
                float3 root = new float3(i * 1.1f, 27, 13);
                float3 tip = new float3(spread, 15 + math.abs(i) * 0.6f, 37 - math.abs(i) * 1.2f);
                byte material = (i & 1) == 0 ? Shadow : Body;
                TaperedRibbon(a, o, root, tip, 3.2f, 0.55f, 2.0f, 0.45f, material);
            }
        }

        private static void AuthorLegsAndTalons(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Capsule(a, o, new float3(6 * s, 28, 2), new float3(7 * s, 17, 3), 2.4f, 1.8f, Shadow);
                Capsule(a, o, new float3(7 * s, 17, 3), new float3(7 * s, 14, 4), 2.0f, 1.7f, Highlight);

                // Three forward toes curl around the perch, plus a rear toe. The split claws are
                // intentionally visible in silhouette at the chosen capture resolution.
                for (int toe = -1; toe <= 1; toe++)
                {
                    float3 knuckle = new float3((7 + toe * 1.6f) * s, 14, 2.5f);
                    float3 front = new float3((8 + toe * 2.1f) * s, 11.5f, -2.5f - math.abs(toe));
                    float3 claw = new float3((8.5f + toe * 2.2f) * s, 10.5f, -5.5f - math.abs(toe));
                    Capsule(a, o, knuckle, front, 1.35f, 0.9f, Highlight);
                    Capsule(a, o, front, claw, 0.9f, 0.25f, Highlight);
                }

                Capsule(a, o, new float3(6 * s, 14, 5), new float3(5.5f * s, 11, 9), 1.25f, 0.35f, Highlight);
            }
        }

        private static void AuthorFeatherTexture(IStructureAuthoringSession a, int3 o)
        {
            // Small raised contour strokes on breast/back survive the deterministic isometric
            // rasterizer and provide material texture without introducing a texture asset.
            for (int band = 0; band < 4; band++)
            {
                int count = 5 + band;
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0f : i / (float)(count - 1);
                    float x = math.lerp(-8f - band, 9f + band, t);
                    float y = 48f - band * 5.2f;
                    float z = -10f + band * 1.8f + math.abs(x) * 0.10f;
                    TaperedRibbon(a, o, new float3(x, y, z), new float3(x + 0.5f, y - 5.5f, z - 1.5f),
                        1.35f, 0.25f, 1.1f, 0.25f, (i + band) % 3 == 0 ? Highlight : Shadow);
                }
            }
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

        private static void TaperedRibbon(
            IStructureAuthoringSession a, int3 o, float3 start, float3 end,
            float startWidth, float endWidth, float startThickness, float endThickness, byte material)
        {
            float3 axis = end - start;
            float axisLength2 = math.max(0.0001f, math.dot(axis, axis));
            float maxRadius = math.max(math.max(startWidth, endWidth), math.max(startThickness, endThickness));
            int3 min = (int3)math.floor(math.min(start, end) - maxRadius - 1f);
            int3 max = (int3)math.ceil(math.max(start, end) + maxRadius + 1f);

            float3 dir = math.normalizesafe(axis, new float3(0, 0, 1));
            float3 helper = math.abs(dir.y) > 0.92f ? new float3(1, 0, 0) : new float3(0, 1, 0);
            float3 side = math.normalizesafe(math.cross(helper, dir), new float3(1, 0, 0));
            float3 normal = math.normalizesafe(math.cross(dir, side), new float3(0, 1, 0));

            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                float t = math.saturate(math.dot(p - start, axis) / axisLength2);
                float3 closest = start + axis * t;
                float3 d = p - closest;
                float width = math.max(0.15f, math.lerp(startWidth, endWidth, t));
                float thickness = math.max(0.15f, math.lerp(startThickness, endThickness, t));
                float u = math.dot(d, side) / width;
                float v = math.dot(d, normal) / thickness;
                if (u * u + v * v <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }
    }
}
