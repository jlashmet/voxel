using System;

namespace MountingForce.WorldGen
{
    /// <summary>One recomputed author/debug row for a settlement plot.</summary>
    public readonly struct SettlementPlacementInspection
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly Int2 PositionDm;
        public readonly FrontageDirection Frontage;
        public readonly PlannedSiteAccess Access;
        public readonly string PresetId;
        public readonly string SelectionSource;

        public SettlementPlacementInspection(
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            Int2 positionDm,
            FrontageDirection frontage,
            PlannedSiteAccess access,
            string presetId,
            string selectionSource)
        {
            RoleId = roleId;
            Archetype = archetype;
            District = district;
            PositionDm = positionDm;
            Frontage = frontage;
            Access = access;
            PresetId = presetId ?? string.Empty;
            SelectionSource = selectionSource ?? string.Empty;
        }

        public override string ToString() =>
            "role=" + RoleId + " " + District + "/" + Archetype +
            " pos=" + PositionDm + " facing=" + Frontage +
            " preset=" + PresetId + " via=" + SelectionSource;
    }

    /// <summary>
    /// Stateless settlement inspection. It reruns the same stable palette/landmark choice from the
    /// plan seed and candidate role; it does not create a cache, registry, or second source of truth.
    /// </summary>
    public static class SettlementCompositionInspection
    {
        public static SettlementPlacementInspection[] Build(
            SettlementPlan plan,
            SettlementCompositionPolicy policy)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            policy.ValidateBounded();

            var result = new SettlementPlacementInspection[plan.Plots.Count];
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                string presetId;
                string source;
                if (HasOrdinaryPaletteCandidate(policy.Palette, plot.Archetype, plot.District))
                {
                    presetId = policy.Palette.SelectPreset(
                        plan.Seed,
                        plot.RoleId,
                        plot.Archetype,
                        plot.District);
                    source = "weighted-palette";
                }
                else if (TryLandmarkPreset(plan.Seed, plot, policy, out presetId))
                {
                    source = "landmark-rule";
                }
                else
                {
                    presetId = "bespoke";
                    source = "explicit-archetype";
                }

                result[i] = new SettlementPlacementInspection(
                    plot.RoleId,
                    plot.Archetype,
                    plot.District,
                    plot.PositionDm,
                    plot.Frontage,
                    plot.Access,
                    presetId,
                    source);
            }
            return result;
        }

        private static bool HasOrdinaryPaletteCandidate(
            SettlementStructurePalette palette,
            StructureArchetype archetype,
            DistrictKind district)
        {
            SettlementArchetypeMask archetypeMask = (SettlementArchetypeMask)(1 << (int)archetype);
            SettlementDistrictMask districtMask = (SettlementDistrictMask)(1 << (int)district);
            for (int i = 0; i < palette.Entries.Count; i++)
            {
                SettlementPaletteEntry entry = palette.Entries[i];
                if (!entry.LandmarkOnly &&
                    (entry.Archetypes & archetypeMask) != 0 &&
                    (entry.Districts & districtMask) != 0)
                    return true;
            }
            return false;
        }

        private static bool TryLandmarkPreset(
            uint seed,
            BuildingPlot plot,
            SettlementCompositionPolicy policy,
            out string presetId)
        {
            SettlementLandmarkKind? kind = LandmarkKind(plot.Archetype);
            if (!kind.HasValue)
            {
                presetId = string.Empty;
                return false;
            }

            for (int i = 0; i < policy.Landmarks.Count; i++)
            {
                SettlementLandmarkRule rule = policy.Landmarks[i];
                if (rule.Kind == kind.Value && rule.IsCandidate(seed, plot.RoleId, plot.District))
                {
                    presetId = rule.PresetId;
                    return true;
                }
            }
            presetId = string.Empty;
            return false;
        }

        private static SettlementLandmarkKind? LandmarkKind(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Church:
                    return SettlementLandmarkKind.Church;
                default:
                    return null;
            }
        }
    }
}
