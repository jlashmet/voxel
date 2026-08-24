using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministic implicit-volume authoring for the dragon statue. The model is assembled from
    /// smooth ellipsoid, tapered-capsule, and membrane SDFs, then committed to canonical voxel state.
    /// It intentionally avoids masonry/brick construction: voxel resolution is used as sampling
    /// resolution for one continuous sculpted silhouette.
    /// </summary>
    public static class DragonStatueAuthoring
    {
        public static readonly int3 LocalMin = new int3(-86, 0, -68);
        public static readonly int3 LocalSize = new int3(172, 128, 150);

        private const byte Body = GameMaterialIds.Slate;
        private const byte ShadowScale = GameMaterialIds.DarkStone;
        private const byte Belly = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Accent = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 origin)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            // Main body masses: broad overlapping implicit volumes create a continuous sculpt.
            Ellipsoid(a, origin, new float3(0, 46, 7), new float3(26, 27, 34), Body);
            Ellipsoid(a, origin, new float3(0, 58, -4), new float3(22, 26, 25), Body);
            Ellipsoid(a, origin, new float3(-21, 34, 21), new float3(20, 19, 25), Body);
            Ellipsoid(a, origin, new float3(21, 34, 21), new float3(20, 19, 25), Body);

            // Neck rises in an S curve so the head reads clearly from the default isometric camera.
            TaperedCapsule(a, origin, new float3(0, 59, -5), new float3(0, 76, -15), 17f, 14f, Body);
            TaperedCapsule(a, origin, new float3(0, 74, -15), new float3(0, 90, -25), 14f, 11f, Body);
            Ellipsoid(a, origin, new float3(0, 94, -34), new float3(16, 13, 17), Body);
            Ellipsoid(a, origin, new float3(0, 92, -48), new float3(13, 8, 16), Body);

            // Brow and cheek masses give the head a stylized armored silhouette.
            Ellipsoid(a, origin, new float3(-9, 99, -41), new float3(9, 7, 10), ShadowScale);
            Ellipsoid(a, origin, new float3(9, 99, -41), new float3(9, 7, 10), ShadowScale);
            Ellipsoid(a, origin, new float3(-11, 90, -36), new float3(7, 8, 10), Body);
            Ellipsoid(a, origin, new float3(11, 90, -36), new float3(7, 8, 10), Body);

            // Lower jaw is separated from the muzzle by an air gap rather than brick-like carving.
            TaperedCapsule(a, origin, new float3(0, 85, -40), new float3(0, 86, -57), 8f, 5f, ShadowScale);

            // Underbelly is a continuous material pass over the chest and lower neck.
            Ellipsoid(a, origin, new float3(0, 51, -20), new float3(15, 25, 13), Belly);
            TaperedCapsule(a, origin, new float3(0, 62, -18), new float3(0, 84, -29), 10f, 6f, Belly);

            // Rear legs / haunches.
            Limb(a, origin, new float3(-20, 38, 20), new float3(-30, 22, 10), new float3(-27, 9, -7), 11f, 8f);
            Limb(a, origin, new float3(20, 38, 20), new float3(30, 22, 10), new float3(27, 9, -7), 11f, 8f);
            Foot(a, origin, -27, 7, -13, false);
            Foot(a, origin, 27, 7, -13, false);

            // Front legs are upright and powerful, matching the seated guardian pose.
            Limb(a, origin, new float3(-18, 54, -12), new float3(-24, 31, -19), new float3(-23, 10, -28), 9f, 6f);
            Limb(a, origin, new float3(18, 54, -12), new float3(24, 31, -19), new float3(23, 10, -28), 9f, 6f);
            Foot(a, origin, -23, 7, -35, true);
            Foot(a, origin, 23, 7, -35, true);

            // Wings: thin implicit membranes plus rounded leading/finger bones. These remain voxel
            // volumes, but read as a single sculpted sheet instead of stacked blocks.
            Wing(a, origin, -1);
            Wing(a, origin, 1);

            // Tail sweeps around the right side and curls forward.
            TaperedCapsule(a, origin, new float3(8, 38, 29), new float3(29, 28, 43), 14f, 11f, Body);
            TaperedCapsule(a, origin, new float3(29, 28, 43), new float3(52, 18, 38), 11f, 8f, Body);
            TaperedCapsule(a, origin, new float3(52, 18, 38), new float3(64, 13, 18), 8f, 6f, Body);
            TaperedCapsule(a, origin, new float3(64, 13, 18), new float3(58, 10, -6), 6f, 4f, Body);
            TaperedCapsule(a, origin, new float3(58, 10, -6), new float3(45, 9, -25), 4f, 1.5f, Body);

            // Crown horns and jaw horns.
            HornSegment(a, origin, new float3(-9, 104, -34), new float3(-15, 117, -22), 5.0f, 1.2f);
            HornSegment(a, origin, new float3(9, 104, -34), new float3(15, 117, -22), 5.0f, 1.2f);
            HornSegment(a, origin, new float3(-13, 99, -37), new float3(-22, 108, -34), 3.5f, 0.8f);
            HornSegment(a, origin, new float3(13, 99, -37), new float3(22, 108, -34), 3.5f, 0.8f);
            HornSegment(a, origin, new float3(-12, 88, -45), new float3(-17, 82, -48), 2.7f, 0.7f);
            HornSegment(a, origin, new float3(12, 88, -45), new float3(17, 82, -48), 2.7f, 0.7f);

            // Dorsal spines are sparse silhouette accents, not repeated masonry detail.
            Spine(a, origin, new float3(0, 102, -22), new float3(0, 112, -16), 4.2f);
            Spine(a, origin, new float3(0, 91, -15), new float3(0, 103, -8), 4.5f);
            Spine(a, origin, new float3(0, 78, -5), new float3(0, 91, 1), 4.2f);
            Spine(a, origin, new float3(0, 65, 7), new float3(0, 77, 13), 3.8f);
            Spine(a, origin, new float3(0, 55, 20), new float3(0, 65, 27), 3.3f);
            Spine(a, origin, new float3(15, 40, 35), new float3(15, 48, 44), 3.0f);
            Spine(a, origin, new float3(34, 28, 45), new float3(35, 35, 54), 2.6f);
            Spine(a, origin, new float3(51, 19, 38), new float3(54, 25, 46), 2.2f);

            // Stylized mossy patina in broad organic patches, matching the project's earthy palette.
            Ellipsoid(a, origin, new float3(-15, 68, 8), new float3(8, 4, 10), Accent);
            Ellipsoid(a, origin, new float3(14, 54, 21), new float3(9, 3, 10), Accent);
            Ellipsoid(a, origin, new float3(-27, 75, 9), new float3(8, 3, 7), Accent);
            Ellipsoid(a, origin, new float3(28, 75, 9), new float3(7, 3, 8), Accent);
            Ellipsoid(a, origin, new float3(40, 21, 41), new float3(7, 2, 6), Accent);

            // Eyes are last so they remain readable against the dark brow.
            Ellipsoid(a, origin, new float3(-7, 98, -51), new float3(2.2f, 2.1f, 1.8f), Eye);
            Ellipsoid(a, origin, new float3(7, 98, -51), new float3(2.2f, 2.1f, 1.8f), Eye);
        }

        private static void Limb(IStructureAuthoringSession a, int3 o, float3 hip, float3 knee, float3 ankle, float upper, float lower)
        {
            TaperedCapsule(a, o, hip, knee, upper, lower, Body);
            TaperedCapsule(a, o, knee, ankle, lower, math.max(4f, lower - 2f), Body);
        }

        private static void Foot(IStructureAuthoringSession a, int3 o, int x, int y, int z, bool front)
        {
            Ellipsoid(a, o, new float3(x, y + 2, z), new float3(9, 5, 11), Body);
            float spread = front ? 4.2f : 3.5f;
            for (int i = -1; i <= 1; i++)
            {
                float tx = x + i * spread;
                float3 start = new float3(tx, y + 2, z - 6);
                float3 end = new float3(tx + i * 0.8f, y + 1, z - 16);
                TaperedCapsule(a, o, start, end, 2.3f, 0.6f, Horn);
            }
        }

        private static void Wing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(19f * s, 72, 4);
            float3 elbow = new float3(43f * s, 96, 8);
            float3 tip = new float3(79f * s, 111, 10);
            float3 trailing = new float3(58f * s, 52, 13);
            float3 inner = new float3(25f * s, 50, 10);

            // Two triangular sheets form the membrane with a subtle sweep in depth.
            WingMembrane(a, o, shoulder, elbow, trailing, 2.6f, ShadowScale);
            WingMembrane(a, o, elbow, tip, trailing, 2.4f, ShadowScale);
            WingMembrane(a, o, shoulder, trailing, inner, 2.8f, ShadowScale);

            TaperedCapsule(a, o, shoulder, elbow, 6.5f, 5.2f, Body);
            TaperedCapsule(a, o, elbow, tip, 5.2f, 2.2f, Body);
            TaperedCapsule(a, o, elbow, trailing, 4.0f, 2.0f, Body);
            TaperedCapsule(a, o, shoulder, inner, 5.0f, 3.0f, Body);
            TaperedCapsule(a, o, trailing, inner, 2.8f, 1.5f, Body);

            // Wing-tip hook.
            HornSegment(a, o, tip, new float3(84f * s, 102, 7), 2.5f, 0.5f);
        }

        private static void Spine(IStructureAuthoringSession a, int3 o, float3 root, float3 tip, float radius)
        {
            TaperedCapsule(a, o, root, tip, radius, 0.5f, Horn);
        }

        private static void HornSegment(IStructureAuthoringSession a, int3 o, float3 root, float3 tip, float radius, float tipRadius)
        {
            TaperedCapsule(a, o, root, tip, radius, tipRadius, Horn);
        }

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

        private static void TaperedCapsule(
            IStructureAuthoringSession a,
            int3 o,
            float3 start,
            float3 end,
            float startRadius,
            float endRadius,
            byte material)
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

        private static void WingMembrane(
            IStructureAuthoringSession a,
            int3 o,
            float3 va,
            float3 vb,
            float3 vc,
            float halfThickness,
            byte material)
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
