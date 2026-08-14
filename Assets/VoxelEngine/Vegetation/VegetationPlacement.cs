using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Core.Vegetation
{
    /// <summary>
    /// Pure deterministic placement policy for low vegetation. World generation is responsible
    /// for discovering candidate surfaces; this class decides what grows on those surfaces.
    /// </summary>
    public static class VegetationPlacement
    {
        private static readonly VegetationKind[] Kinds =
        {
            VegetationKind.Grass,
            VegetationKind.Flower,
            VegetationKind.Fern,
            VegetationKind.Bush,
            VegetationKind.Moss,
            VegetationKind.Vine,
        };

        public static void Generate(
            IReadOnlyList<VegetationSurfaceSample> samples,
            in VegetationPlacementSettings settings,
            List<VegetationInstance> output)
        {
            if (samples == null || output == null)
            {
                return;
            }

            float density = math.saturate(settings.Density);
            for (int i = 0; i < samples.Count; i++)
            {
                VegetationSurfaceSample sample = samples[i];
                float3 normal = math.normalizesafe(sample.Normal, new float3(0f, 1f, 0f));
                uint seed = Hash(settings.WorldSeed, (uint)i, QuantizedHash(sample.PositionMetres));

                VegetationKind selected = SelectKind(sample, normal, seed, settings, out float suitability);
                if (suitability <= 0f)
                {
                    continue;
                }

                float roll = Random01(seed ^ 0xA341316Cu);
                if (roll > density * math.saturate(suitability))
                {
                    continue;
                }

                float scaleT = Random01(seed ^ 0xC8013EA4u);
                float minScale = math.max(0.01f, settings.MinScale);
                float maxScale = math.max(minScale, settings.MaxScale);

                output.Add(new VegetationInstance
                {
                    PositionMetres = sample.PositionMetres,
                    SurfaceNormal = normal,
                    Kind = selected,
                    Seed = seed,
                    Scale = math.lerp(minScale, maxScale, scaleT),
                });
            }
        }

        private static VegetationKind SelectKind(
            in VegetationSurfaceSample sample,
            float3 normal,
            uint seed,
            in VegetationPlacementSettings settings,
            out float suitability)
        {
            float total = 0f;
            float strongest = 0f;
            for (int i = 0; i < Kinds.Length; i++)
            {
                float score = Score(sample, normal, VegetationProfiles.Get(Kinds[i]), settings);
                total += score;
                strongest = math.max(strongest, score);
            }

            if (total <= 0f)
            {
                suitability = 0f;
                return VegetationKind.Grass;
            }

            float target = Random01(seed ^ 0x9E3779B9u) * total;
            float cumulative = 0f;
            for (int i = 0; i < Kinds.Length; i++)
            {
                VegetationProfile profile = VegetationProfiles.Get(Kinds[i]);
                cumulative += Score(sample, normal, profile, settings);
                if (target <= cumulative)
                {
                    suitability = strongest;
                    return profile.Kind;
                }
            }

            suitability = strongest;
            return Kinds[Kinds.Length - 1];
        }

        private static float Score(
            in VegetationSurfaceSample sample,
            float3 normal,
            in VegetationProfile profile,
            in VegetationPlacementSettings settings)
        {
            float surfaceWeight = VegetationProfiles.SurfaceWeight(profile, sample.Surface);
            if (surfaceWeight <= 0f)
            {
                return 0f;
            }

            float upDot = math.clamp(normal.y, -1f, 1f);
            float slopeDegrees = math.degrees(math.acos(upDot));
            if (sample.Surface == VegetationSurface.Ground && slopeDegrees > settings.MaxGroundSlopeDegrees)
            {
                return 0f;
            }

            float slope01 = math.saturate(slopeDegrees / 90f);
            float slopeFactor = math.lerp(1f, profile.SlopeTolerance, slope01);
            float moisture = math.saturate(sample.Moisture);
            float shade = math.saturate(sample.Shade);
            float environment = 1f
                + (moisture - 0.5f) * profile.MoistureAffinity * (1f + settings.MoistureBias)
                + (shade - 0.5f) * profile.ShadeAffinity * (1f + settings.ShadeBias);

            // Vines should originate from near-vertical support surfaces, not floors.
            if (profile.Kind == VegetationKind.Vine)
            {
                float verticality = 1f - math.abs(normal.y);
                environment *= math.smoothstep(0.45f, 0.90f, verticality);
            }

            // Flowers, grass, ferns and bushes are upright growth forms.
            if (profile.Kind != VegetationKind.Moss && profile.Kind != VegetationKind.Vine)
            {
                environment *= math.smoothstep(0.25f, 0.75f, math.max(0f, normal.y));
            }

            return math.max(0f, surfaceWeight * slopeFactor * environment);
        }

        private static uint QuantizedHash(float3 position)
        {
            int3 q = (int3)math.round(position * 16f);
            uint h = 2166136261u;
            h = (h ^ (uint)q.x) * 16777619u;
            h = (h ^ (uint)q.y) * 16777619u;
            h = (h ^ (uint)q.z) * 16777619u;
            return h;
        }

        internal static uint Hash(uint a, uint b, uint c)
        {
            uint h = a ^ 0x9E3779B9u;
            h ^= b + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= c + 0xC2B2AE35u + (h << 6) + (h >> 2);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h == 0u ? 1u : h;
        }

        internal static float Random01(uint state)
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return (x & 0x00FFFFFFu) / 16777216f;
        }
    }
}
