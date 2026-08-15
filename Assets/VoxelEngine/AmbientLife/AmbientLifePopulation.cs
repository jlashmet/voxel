using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Core.AmbientLife
{
    /// <summary>
    /// Builds deterministic ambient populations from habitat summaries. Individual creatures are
    /// intentionally not authored here; a renderer/simulator reconstructs local agents from each
    /// cluster seed and can activate them according to the species activity window.
    /// </summary>
    public static class AmbientLifePopulation
    {
        public static void Generate(
            IReadOnlyList<AmbientLifeHabitatSample> habitats,
            in AmbientLifePopulationSettings settings,
            List<AmbientLifeCluster> output)
        {
            if (habitats == null || output == null)
            {
                return;
            }

            float density = math.saturate(settings.Density);
            for (int i = 0; i < habitats.Count; i++)
            {
                AmbientLifeHabitatSample habitat = habitats[i];
                uint seed = Hash(settings.WorldSeed, (uint)i, QuantizedHash(habitat.PositionMetres));
                AmbientLifeKind kind = SelectKind(habitat, seed, out float suitability);
                if (suitability <= 0f)
                {
                    continue;
                }

                if (Random01(seed ^ 0xB5297A4Du) > density * math.saturate(suitability))
                {
                    continue;
                }

                AmbientLifeProfile profile = AmbientLifeCatalogue.Get(kind);
                int countRange = math.max(0, profile.MaxCount - profile.MinCount);
                int count = profile.MinCount;
                if (countRange > 0)
                {
                    count += math.min(countRange,
                        (int)math.floor(Random01(seed ^ 0x68E31DA4u) * (countRange + 1)));
                }

                float minRadius = math.max(0.25f, settings.MinRadiusMetres);
                float maxRadius = math.max(minRadius, settings.MaxRadiusMetres);
                float requestedRadius = habitat.RadiusMetres > 0f
                    ? habitat.RadiusMetres
                    : math.lerp(minRadius, maxRadius, Random01(seed ^ 0x1B56C4E9u));

                output.Add(new AmbientLifeCluster
                {
                    PositionMetres = habitat.PositionMetres,
                    Kind = kind,
                    Seed = seed,
                    Count = (ushort)math.clamp(count, 1, ushort.MaxValue),
                    RadiusMetres = math.clamp(requestedRadius, minRadius, maxRadius),
                });
            }
        }

        private static AmbientLifeKind SelectKind(
            in AmbientLifeHabitatSample habitat,
            uint seed,
            out float suitability)
        {
            float total = 0f;
            float strongest = 0f;
            for (int i = 0; i < AmbientLifeCatalogue.Count; i++)
            {
                float score = Score(habitat, AmbientLifeCatalogue.Get(AmbientLifeCatalogue.KindAt(i)));
                total += score;
                strongest = math.max(strongest, score);
            }

            if (total <= 0f)
            {
                suitability = 0f;
                return AmbientLifeKind.Firefly;
            }

            float target = Random01(seed ^ 0x9E3779B9u) * total;
            float cumulative = 0f;
            for (int i = 0; i < AmbientLifeCatalogue.Count; i++)
            {
                AmbientLifeProfile profile = AmbientLifeCatalogue.Get(AmbientLifeCatalogue.KindAt(i));
                cumulative += Score(habitat, profile);
                if (target <= cumulative)
                {
                    // Profiles deliberately have small absolute weights because those weights also
                    // express rarity. Normalize suitability independently for the density roll.
                    suitability = math.saturate(strongest * 4f);
                    return profile.Kind;
                }
            }

            suitability = math.saturate(strongest * 4f);
            return AmbientLifeCatalogue.KindAt(AmbientLifeCatalogue.Count - 1);
        }

        private static float Score(in AmbientLifeHabitatSample habitat, in AmbientLifeProfile profile)
        {
            float arcane = math.saturate(habitat.ArcaneSaturation);
            if (arcane < profile.MinArcaneSaturation)
            {
                return 0f;
            }

            float environment = 1f
                + (math.saturate(habitat.Moisture) - 0.5f) * profile.MoistureAffinity
                + (math.saturate(habitat.Shade) - 0.5f) * profile.ShadeAffinity
                + (math.saturate(habitat.FlowerDensity) - 0.5f) * profile.FlowerAffinity
                + (math.saturate(habitat.WaterPresence) - 0.5f) * profile.WaterAffinity
                + (math.saturate(habitat.FungusDensity) - 0.5f) * profile.FungusAffinity
                + (math.saturate(habitat.DeadwoodDensity) - 0.5f) * profile.DeadwoodAffinity
                + arcane * profile.ArcaneAffinity;

            return math.max(0f, profile.BaseWeight * environment);
        }

        private static uint QuantizedHash(float3 position)
        {
            int3 q = (int3)math.round(position * 8f);
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
