using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Chooses a deterministic, route-compatible anomaly motif by asking which deviation will read
    /// most strongly against the supplied local context. Nearby motif usage is penalized so secrets
    /// do not collapse into one repeated global marker language.
    /// </summary>
    public static class SecretClueAnomalyPlanner
    {
        private static readonly SecretClueMotifFamily[] BreakableMotifs =
        {
            SecretClueMotifFamily.StructuralFracture,
            SecretClueMotifFamily.MaterialSeam,
            SecretClueMotifFamily.DebrisAlignment,
            SecretClueMotifFamily.SurfaceWear,
        };

        private static readonly SecretClueMotifFamily[] MechanismMotifs =
        {
            SecretClueMotifFamily.MechanicalTrace,
            SecretClueMotifFamily.SurfaceWear,
            SecretClueMotifFamily.MaterialSeam,
            SecretClueMotifFamily.DebrisAlignment,
        };

        private static readonly SecretClueMotifFamily[] TraversalMotifs =
        {
            SecretClueMotifFamily.VegetationDiscontinuity,
            SecretClueMotifFamily.ErosionTrail,
            SecretClueMotifFamily.SightlineGap,
            SecretClueMotifFamily.DisturbedGround,
            SecretClueMotifFamily.DebrisAlignment,
        };

        public static SecretClueAnomalyPlan Resolve(
            int worldSeed,
            string stableClueId,
            SecretRouteKind routeKind,
            SecretClueChannel channel,
            in SecretClueLocalContext context,
            IReadOnlyList<SecretClueMotifFamily> nearbyMotifs = null)
        {
            if (string.IsNullOrWhiteSpace(stableClueId))
                throw new ArgumentException("Anomaly planning requires a stable clue id.", nameof(stableClueId));

            SecretClueMotifFamily[] candidates = Candidates(routeKind);
            SecretClueMotifFamily selected = candidates[0];
            int selectedScore = int.MinValue;
            uint selectedTie = uint.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                SecretClueMotifFamily motif = candidates[i];
                int score = ContextScore(motif, in context) + ChannelBonus(motif, channel);
                score -= NearbyPenalty(motif, nearbyMotifs);
                uint tie = StableHash(worldSeed, stableClueId, routeKind, channel, motif);
                if (score > selectedScore || (score == selectedScore && tie < selectedTie))
                {
                    selected = motif;
                    selectedScore = score;
                    selectedTie = tie;
                }
            }

            Contrast(selected, out SecretClueContrastAxis primary, out SecretClueContrastAxis secondary);
            int strength = Math.Max(35, Math.Min(90, 45 + selectedScore / 3));
            return new SecretClueAnomalyPlan(
                selected,
                primary,
                secondary,
                ActionIntent(routeKind),
                strength);
        }

        public static bool IsCompatible(SecretRouteKind routeKind, SecretClueMotifFamily motif)
        {
            SecretClueMotifFamily[] candidates = Candidates(routeKind);
            for (int i = 0; i < candidates.Length; i++)
                if (candidates[i] == motif)
                    return true;
            return false;
        }

        private static SecretClueMotifFamily[] Candidates(SecretRouteKind routeKind)
        {
            switch (routeKind)
            {
                case SecretRouteKind.BreakableBarrier:
                    return BreakableMotifs;
                case SecretRouteKind.Door:
                case SecretRouteKind.Trapdoor:
                case SecretRouteKind.Pushable:
                case SecretRouteKind.PressurePlateMechanism:
                case SecretRouteKind.ScriptedMechanism:
                    return MechanismMotifs;
                case SecretRouteKind.Climb:
                case SecretRouteKind.Swim:
                case SecretRouteKind.NaturalTraversal:
                    return TraversalMotifs;
                default:
                    throw new ArgumentOutOfRangeException(nameof(routeKind), routeKind, null);
            }
        }

        private static SecretClueActionIntent ActionIntent(SecretRouteKind routeKind)
        {
            switch (routeKind)
            {
                case SecretRouteKind.BreakableBarrier:
                    return SecretClueActionIntent.BreakBarrier;
                case SecretRouteKind.Door:
                case SecretRouteKind.Trapdoor:
                case SecretRouteKind.Pushable:
                case SecretRouteKind.PressurePlateMechanism:
                case SecretRouteKind.ScriptedMechanism:
                    return SecretClueActionIntent.OperateMechanism;
                case SecretRouteKind.Climb:
                case SecretRouteKind.Swim:
                case SecretRouteKind.NaturalTraversal:
                    return SecretClueActionIntent.TraverseTerrain;
                default:
                    return SecretClueActionIntent.Investigate;
            }
        }

        private static int ContextScore(SecretClueMotifFamily motif, in SecretClueLocalContext context)
        {
            switch (motif)
            {
                case SecretClueMotifFamily.StructuralFracture:
                    return context.SurfaceUniformityPercent + context.StructuralRegularityPercent / 2;
                case SecretClueMotifFamily.MaterialSeam:
                    return context.SurfaceUniformityPercent;
                case SecretClueMotifFamily.SurfaceWear:
                    return context.StructuralRegularityPercent + (100 - context.RecentDisturbancePercent) / 3;
                case SecretClueMotifFamily.MechanicalTrace:
                    return context.StructuralRegularityPercent + context.SurfaceUniformityPercent / 3;
                case SecretClueMotifFamily.DebrisAlignment:
                    return context.SurfaceUniformityPercent / 2 + context.StructuralRegularityPercent / 2;
                case SecretClueMotifFamily.VegetationDiscontinuity:
                    return context.VegetationDensityPercent + context.SurfaceUniformityPercent / 3;
                case SecretClueMotifFamily.ErosionTrail:
                    return context.SurfaceUniformityPercent + (100 - context.RecentDisturbancePercent) / 4;
                case SecretClueMotifFamily.SightlineGap:
                    return context.OcclusionPercent + context.VegetationDensityPercent / 3;
                case SecretClueMotifFamily.DisturbedGround:
                    return context.VegetationDensityPercent / 2 + context.SurfaceUniformityPercent / 2 +
                           (100 - context.RecentDisturbancePercent) / 3;
                default:
                    return 0;
            }
        }

        private static int ChannelBonus(SecretClueMotifFamily motif, SecretClueChannel channel)
        {
            switch (channel)
            {
                case SecretClueChannel.Mechanical:
                    return motif == SecretClueMotifFamily.MechanicalTrace ||
                           motif == SecretClueMotifFamily.SurfaceWear ? 45 : 0;
                case SecretClueChannel.Environmental:
                    return motif == SecretClueMotifFamily.VegetationDiscontinuity ||
                           motif == SecretClueMotifFamily.ErosionTrail ||
                           motif == SecretClueMotifFamily.DisturbedGround ? 35 : 0;
                case SecretClueChannel.Navigation:
                case SecretClueChannel.Spatial:
                    return motif == SecretClueMotifFamily.SightlineGap ||
                           motif == SecretClueMotifFamily.DebrisAlignment ? 35 : 0;
                case SecretClueChannel.Visual:
                    return motif == SecretClueMotifFamily.StructuralFracture ||
                           motif == SecretClueMotifFamily.MaterialSeam ||
                           motif == SecretClueMotifFamily.SightlineGap ? 30 : 0;
                default:
                    return 0;
            }
        }

        private static int NearbyPenalty(
            SecretClueMotifFamily motif,
            IReadOnlyList<SecretClueMotifFamily> nearbyMotifs)
        {
            if (nearbyMotifs == null) return 0;
            int repeated = 0;
            for (int i = 0; i < nearbyMotifs.Count; i++)
                if (nearbyMotifs[i] == motif)
                    repeated++;
            return repeated * 85;
        }

        private static void Contrast(
            SecretClueMotifFamily motif,
            out SecretClueContrastAxis primary,
            out SecretClueContrastAxis secondary)
        {
            switch (motif)
            {
                case SecretClueMotifFamily.StructuralFracture:
                    primary = SecretClueContrastAxis.Silhouette;
                    secondary = SecretClueContrastAxis.Material;
                    return;
                case SecretClueMotifFamily.MaterialSeam:
                    primary = SecretClueContrastAxis.Material;
                    secondary = SecretClueContrastAxis.Alignment;
                    return;
                case SecretClueMotifFamily.SurfaceWear:
                    primary = SecretClueContrastAxis.Material;
                    secondary = SecretClueContrastAxis.Repetition;
                    return;
                case SecretClueMotifFamily.MechanicalTrace:
                    primary = SecretClueContrastAxis.Alignment;
                    secondary = SecretClueContrastAxis.Repetition;
                    return;
                case SecretClueMotifFamily.DebrisAlignment:
                    primary = SecretClueContrastAxis.Alignment;
                    secondary = SecretClueContrastAxis.Silhouette;
                    return;
                case SecretClueMotifFamily.VegetationDiscontinuity:
                    primary = SecretClueContrastAxis.Density;
                    secondary = SecretClueContrastAxis.Alignment;
                    return;
                case SecretClueMotifFamily.ErosionTrail:
                    primary = SecretClueContrastAxis.Material;
                    secondary = SecretClueContrastAxis.Alignment;
                    return;
                case SecretClueMotifFamily.SightlineGap:
                    primary = SecretClueContrastAxis.NegativeSpace;
                    secondary = SecretClueContrastAxis.Silhouette;
                    return;
                case SecretClueMotifFamily.DisturbedGround:
                    primary = SecretClueContrastAxis.Material;
                    secondary = SecretClueContrastAxis.Density;
                    return;
                default:
                    primary = SecretClueContrastAxis.Material;
                    secondary = SecretClueContrastAxis.Silhouette;
                    return;
            }
        }

        private static uint StableHash(
            int worldSeed,
            string clueId,
            SecretRouteKind routeKind,
            SecretClueChannel channel,
            SecretClueMotifFamily motif)
        {
            unchecked
            {
                uint hash = 2166136261u;
                MixInt(ref hash, worldSeed);
                for (int i = 0; i < clueId.Length; i++)
                {
                    char c = clueId[i];
                    hash ^= (byte)c; hash *= 16777619u;
                    hash ^= (byte)(c >> 8); hash *= 16777619u;
                }
                MixInt(ref hash, (int)routeKind);
                MixInt(ref hash, (int)channel);
                MixInt(ref hash, (int)motif);
                return hash;
            }
        }

        private static void MixInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value; hash *= 16777619u;
                hash ^= (byte)(value >> 8); hash *= 16777619u;
                hash ^= (byte)(value >> 16); hash *= 16777619u;
                hash ^= (byte)(value >> 24); hash *= 16777619u;
            }
        }
    }
}
