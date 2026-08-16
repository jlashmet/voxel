using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Vegetation.Api
{
    public struct VineGrowthSettings
    {
        public uint Seed;
        public float LengthMetres;
        public int SegmentCount;
        public float SurfaceAttraction;
        public float Droop;
        public float Wander;

        public static VineGrowthSettings Default(uint seed)
        {
            return new VineGrowthSettings
            {
                Seed = seed,
                LengthMetres = 4f,
                SegmentCount = 12,
                SurfaceAttraction = 0.80f,
                Droop = 0.55f,
                Wander = 0.20f,
            };
        }
    }

    /// <summary>
    /// Produces a semantic polyline for a vine attached to a support surface. The algorithm borrows
    /// ez-tree's useful idea of growth being attracted toward a guide/support, but remains engine-
    /// agnostic and deterministic. Rendering can turn the path into voxels, ribbons, or tubes.
    /// </summary>
    public static class VineGrowth
    {
        public static void Generate(
            float3 anchor,
            float3 supportNormal,
            in VineGrowthSettings settings,
            List<float3> output)
        {
            if (output == null)
            {
                return;
            }

            int segments = math.max(1, settings.SegmentCount);
            float length = math.max(0.05f, settings.LengthMetres);
            float step = length / segments;
            float3 normal = math.normalizesafe(supportNormal, new float3(0f, 0f, 1f));

            float3 tangentA = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), normal));
            if (math.lengthsq(tangentA) < 0.001f)
            {
                tangentA = new float3(1f, 0f, 0f);
            }
            float3 tangentB = math.normalizesafe(math.cross(normal, tangentA), new float3(0f, 1f, 0f));

            float3 position = anchor;
            float3 direction = math.normalizesafe(
                tangentB * 0.30f + new float3(0f, -math.max(0.05f, settings.Droop), 0f),
                new float3(0f, -1f, 0f));

            output.Add(position);
            for (int i = 0; i < segments; i++)
            {
                uint seed = VegetationPlacement.Hash(settings.Seed, (uint)i + 1u, 0x51ED270Bu);
                float lateral = (VegetationPlacement.Random01(seed) * 2f - 1f) * settings.Wander;
                float vertical = (VegetationPlacement.Random01(seed ^ 0x68BC21EBu) * 2f - 1f) * settings.Wander * 0.35f;

                float3 wander = tangentA * lateral + tangentB * vertical;
                float3 gravity = new float3(0f, -math.max(0f, settings.Droop), 0f);

                // Keep the path close to its support plane. This is equivalent to a trellis-style
                // attractor without hard-coding an actual trellis object into vegetation semantics.
                float distanceFromPlane = math.dot(position - anchor, normal);
                float3 supportPull = -normal * distanceFromPlane * math.max(0f, settings.SurfaceAttraction);

                float3 desired = direction + wander + gravity + supportPull;
                direction = math.normalizesafe(math.lerp(direction, desired, 0.55f), direction);
                position += direction * step;

                // Project most of any accumulated error back toward the support plane while leaving
                // a tiny stand-off so a rendered vine does not z-fight with rock or masonry.
                float planeError = math.dot(position - anchor, normal);
                position -= normal * planeError * math.saturate(settings.SurfaceAttraction);
                position += normal * 0.015f;
                output.Add(position);
            }
        }
    }
}
