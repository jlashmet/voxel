using System;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum GuildExteriorZoneKind : byte
    {
        FrontApproach = 1,
        ActivityYard = 2,
        Garden = 3,
        StableYard = 4,
    }

    public readonly struct GuildExteriorZone
    {
        public readonly GuildExteriorZoneKind Kind;
        public readonly DecorationSpace Space;
        public readonly DecorationContext Context;

        public GuildExteriorZone(GuildExteriorZoneKind kind, DecorationSpace space, DecorationContext context)
        { Kind = kind; Space = space; Context = context; }
    }

    /// <summary>
    /// Creates furnishable exterior strips around a guild shell without owning settlement lot geometry.
    /// The production settlement planner remains authoritative for whether those strips fit a real lot.
    /// </summary>
    public static class GuildHouseExteriorSpacePlanner
    {
        public static GuildExteriorZone[] Plan(in GuildHousePrototype prototype)
        {
            if (!prototype.IsWellFormed) return Array.Empty<GuildExteriorZone>();
            GuildHouseSpatialPlan p = prototype.SpatialPlan;
            var kinds = ZoneKinds(p.Kind);
            var result = new GuildExteriorZone[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                DecorationBounds bounds = BoundsFor(kinds[i], in p);
                uint spaceId = 0x70000000u | ((uint)p.Kind << 16) | (uint)(i + 1);
                var space = new DecorationSpace
                {
                    SpaceId = spaceId,
                    Kind = DecorationSpaceKind.ExteriorYard,
                    Bounds = bounds,
                };
                var context = new DecorationContext
                {
                    WorldSeed = prototype.Rooms[0].Context.WorldSeed,
                    StructureId = prototype.Rooms[0].Context.StructureId,
                    SpaceId = spaceId,
                    StructureKind = DecorationStructureKind.House,
                    SpaceKind = space.Kind,
                    Wealth = prototype.Rooms[0].Context.Wealth,
                    Condition = prototype.Rooms[0].Context.Condition,
                    Environment = DecorationEnvironmentTags.Exterior,
                };
                context = DecorationRegionProfiles.ApplyDefaults(in context, prototype.Region, (uint)(0xE000 + i));
                result[i] = new GuildExteriorZone(kinds[i], space, context);
            }
            return result;
        }

        private static GuildExteriorZoneKind[] ZoneKinds(GuildHouseKind guild)
        {
            switch (guild)
            {
                case GuildHouseKind.Druids:
                    return new[] { GuildExteriorZoneKind.FrontApproach, GuildExteriorZoneKind.Garden, GuildExteriorZoneKind.ActivityYard };
                case GuildHouseKind.Rangers:
                case GuildHouseKind.Knights:
                    return new[] { GuildExteriorZoneKind.FrontApproach, GuildExteriorZoneKind.StableYard, GuildExteriorZoneKind.ActivityYard };
                case GuildHouseKind.Adventurers:
                    return new[] { GuildExteriorZoneKind.FrontApproach, GuildExteriorZoneKind.ActivityYard, GuildExteriorZoneKind.StableYard };
                case GuildHouseKind.Clerics:
                    return new[] { GuildExteriorZoneKind.FrontApproach, GuildExteriorZoneKind.Garden };
                default:
                    return new[] { GuildExteriorZoneKind.FrontApproach, GuildExteriorZoneKind.ActivityYard };
            }
        }

        private static DecorationBounds BoundsFor(GuildExteriorZoneKind kind, in GuildHouseSpatialPlan p)
        {
            const int margin = 6;
            const int depth = 28;
            switch (kind)
            {
                case GuildExteriorZoneKind.FrontApproach:
                    return B(new int3(p.Origin.x + margin, p.Origin.y, p.Origin.z - depth),
                        new int3(p.Width - margin * 2, 18, depth - 2));
                case GuildExteriorZoneKind.StableYard:
                    return B(new int3(p.Origin.x + p.Width + 4, p.Origin.y, p.Origin.z + margin),
                        new int3(34, 18, math.max(30, p.Depth - margin * 2)));
                case GuildExteriorZoneKind.Garden:
                    return B(new int3(p.Origin.x - 38, p.Origin.y, p.Origin.z + margin),
                        new int3(34, 18, math.max(30, p.Depth - margin * 2)));
                default:
                    return B(new int3(p.Origin.x + margin, p.Origin.y, p.Origin.z + p.Depth + 4),
                        new int3(p.Width - margin * 2, 18, 30));
            }
        }

        private static DecorationBounds B(int3 min, int3 size) =>
            new DecorationBounds { Min = min, MaxExclusive = min + size };
    }
}
