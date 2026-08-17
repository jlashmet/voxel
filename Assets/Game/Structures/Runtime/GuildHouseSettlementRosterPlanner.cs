using System;

namespace Game.Structures.Runtime
{
    public enum GuildSettlementScale : byte
    {
        Hamlet = 1,
        Village = 2,
        Town = 3,
        City = 4,
        Capital = 5,
    }

    public readonly struct GuildSettlementRoster
    {
        public readonly DecorationRegionTheme Region;
        public readonly GuildSettlementScale Scale;
        public readonly GuildHouseKind[] Guilds;

        public GuildSettlementRoster(DecorationRegionTheme region, GuildSettlementScale scale, GuildHouseKind[] guilds)
        {
            Region = region;
            Scale = scale;
            Guilds = guilds ?? Array.Empty<GuildHouseKind>();
        }
    }

    /// <summary>
    /// Deterministically proposes which guild institutions belong in a settlement. It is deliberately
    /// separate from settlement geometry: the production world planner can consume this roster and
    /// place guild sites using its existing site/lot machinery rather than invoking a parallel city planner.
    /// </summary>
    public static class GuildHouseSettlementRosterPlanner
    {
        private static readonly GuildHouseKind[] All =
        {
            GuildHouseKind.Adventurers,
            GuildHouseKind.Wizards,
            GuildHouseKind.Knights,
            GuildHouseKind.Assassins,
            GuildHouseKind.Druids,
            GuildHouseKind.Thieves,
            GuildHouseKind.Clerics,
            GuildHouseKind.Rangers,
            GuildHouseKind.Bards,
            GuildHouseKind.Alchemists,
        };

        public static GuildSettlementRoster Plan(DecorationRegionTheme region, GuildSettlementScale scale, uint seed)
        {
            int count = TargetCount(scale);
            if (count == 0 || region == DecorationRegionTheme.Unknown)
                return new GuildSettlementRoster(region, scale, Array.Empty<GuildHouseKind>());

            var candidates = new Candidate[All.Length];
            for (int i = 0; i < All.Length; i++)
            {
                GuildHouseKind kind = All[i];
                int preference = GuildHouseRegionPolicy.Preference(kind, region);
                uint tie = Mix(seed ^ ((uint)kind * 0x9E3779B9u) ^ ((uint)region << 24));
                candidates[i] = new Candidate(kind, preference, tie);
            }

            Array.Sort(candidates, Compare);
            count = Math.Min(count, candidates.Length);
            var result = new GuildHouseKind[count];
            for (int i = 0; i < count; i++)
                result[i] = candidates[i].Kind;

            if (scale >= GuildSettlementScale.City)
                EnsureCoreInstitutions(result, GuildHouseKind.Adventurers, GuildHouseKind.Clerics);
            else
                EnsureCoreInstitutions(result, GuildHouseKind.Adventurers);

            return new GuildSettlementRoster(region, scale, result);
        }

        public static int TargetCount(GuildSettlementScale scale)
        {
            switch (scale)
            {
                case GuildSettlementScale.Hamlet: return 0;
                case GuildSettlementScale.Village: return 1;
                case GuildSettlementScale.Town: return 3;
                case GuildSettlementScale.City: return 6;
                case GuildSettlementScale.Capital: return 9;
                default: return 0;
            }
        }

        private static void EnsureCoreInstitutions(GuildHouseKind[] guilds, params GuildHouseKind[] required)
        {
            if (guilds == null || guilds.Length == 0 || required == null) return;

            int replace = guilds.Length - 1;
            for (int r = 0; r < required.Length && r < guilds.Length; r++)
            {
                GuildHouseKind needed = required[r];
                bool present = false;
                for (int i = 0; i < guilds.Length; i++)
                {
                    if (guilds[i] != needed) continue;
                    present = true;
                    break;
                }
                if (present) continue;

                while (replace >= 0 && IsRequired(guilds[replace], required))
                    replace--;
                if (replace < 0) return;
                guilds[replace--] = needed;
            }
        }

        private static bool IsRequired(GuildHouseKind kind, GuildHouseKind[] required)
        {
            for (int i = 0; i < required.Length; i++)
                if (required[i] == kind) return true;
            return false;
        }

        private static int Compare(Candidate a, Candidate b)
        {
            int p = b.Preference.CompareTo(a.Preference);
            if (p != 0) return p;
            int t = a.TieBreaker.CompareTo(b.TieBreaker);
            return t != 0 ? t : a.Kind.CompareTo(b.Kind);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private readonly struct Candidate
        {
            public readonly GuildHouseKind Kind;
            public readonly int Preference;
            public readonly uint TieBreaker;
            public Candidate(GuildHouseKind kind, int preference, uint tieBreaker)
            { Kind = kind; Preference = preference; TieBreaker = tieBreaker; }
        }
    }
}
