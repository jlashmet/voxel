using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// Deterministic semantic layout for Kentridge. Named sites are placed from district affinity,
    /// bounded clearances and stable role identity; circulation is inferred afterwards from the
    /// realized public entrances. Legacy street constants remain only for older vertical-profile
    /// diagnostics and are not planning inputs.
    /// </summary>
    public static class KentridgeTownPlanner
    {
        public const string MainSpineId = "north-south-spine";
        public const string MarketStreetId = "market-street";
        public const string ResidentialStreetId = "residential-street";
        public const string EastServiceLaneId = "east-service-lane";
        public const string MarketSquareId = "market-square";

        public const string CompactHousePresetId = "house.compact-cabin.v1";
        public const string FarmhousePresetId = "house.farmhouse.v1";
        public const string TallTownhousePresetId = "house.tall-townhouse.v1";
        public const string ParishChurchPresetId = "church.parish.v1";

        // Compatibility coordinates used by older terrain/diagnostic passes only. Build() does not
        // author streets, street axes, or street-facing site placement from these values.
        public const int MainSpineXDm = 1170;
        public const int MarketStreetZDm = 520;
        public const int ResidentialStreetZDm = 900;
        public const int EastLaneXDm = 1490;
        public const int MainRoadWidthDm = 56;
        public const int SecondaryRoadWidthDm = 48;
        public const int ResidentialRoadWidthDm = 44;
        public const int ServiceRoadWidthDm = 36;

        private const int PlanningMinXDm = 560;
        private const int PlanningMaxXDm = 1780;
        private const int PlanningMinZDm = -40;
        private const int PlanningMaxZDm = 1130;
        private const int SiteClearanceDm = 18;
        private const int MaxCandidatesPerSite = 256;

        private static readonly SettlementCompositionPolicy Policy = BuildCompositionPolicy();
        public static SettlementCompositionPolicy CompositionPolicy => Policy;

        private readonly struct OrganicSiteSpec
        {
            public readonly KentridgeRole Role;
            public readonly StructureArchetype Archetype;
            public readonly DistrictKind District;

            public OrganicSiteSpec(KentridgeRole role, StructureArchetype archetype, DistrictKind district)
            {
                Role = role;
                Archetype = archetype;
                District = district;
            }
        }

        private static readonly OrganicSiteSpec[] SiteSpecs =
        {
            new OrganicSiteSpec(KentridgeRole.RadcliffeMansion, StructureArchetype.Mansion, DistrictKind.Noble),
            new OrganicSiteSpec(KentridgeRole.Church, StructureArchetype.Church, DistrictKind.Civic),
            new OrganicSiteSpec(KentridgeRole.Warehouse, StructureArchetype.Warehouse, DistrictKind.Working),
            new OrganicSiteSpec(KentridgeRole.Inn, StructureArchetype.Inn, DistrictKind.Market),
            new OrganicSiteSpec(KentridgeRole.Pub, StructureArchetype.Inn, DistrictKind.Market),
            new OrganicSiteSpec(KentridgeRole.MayorHouse, StructureArchetype.WideHouse, DistrictKind.Civic),
            new OrganicSiteSpec(KentridgeRole.WeaponShop, StructureArchetype.Shop, DistrictKind.Market),
            new OrganicSiteSpec(KentridgeRole.ArmorShop, StructureArchetype.Shop, DistrictKind.Market),
            new OrganicSiteSpec(KentridgeRole.MagicShop, StructureArchetype.Shop, DistrictKind.Market),
            new OrganicSiteSpec(KentridgeRole.RebeccaHouse, StructureArchetype.Townhouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.LoganHouse, StructureArchetype.Townhouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.SarahHouse, StructureArchetype.WideHouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.KatieHouse, StructureArchetype.Townhouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.MedrareHouse, StructureArchetype.WideHouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.AbandonedHouse, StructureArchetype.Townhouse, DistrictKind.Residential),
            new OrganicSiteSpec(KentridgeRole.AwonHouse, StructureArchetype.WideHouse, DistrictKind.Residential),
        };

        public static SettlementPlan Build(uint seed)
        {
            Policy.ValidateBounded();
            var plaza = new PlannedPlaza(
                MarketSquareId,
                KentridgeDefinition.TownCentreDm,
                new Int2(220, 140));

            List<BuildingPlot> plots = PlaceNamedSites(seed, plaza);
            List<PlannedRoute> routes = InferCirculation(seed, plots, plaza);

            Int3 wellFootprint = KentridgeDefinition.FootprintDm(StructureArchetype.Well);
            Int2 wellPosition = new Int2(
                plaza.CentreDm.X - wellFootprint.X / 2,
                plaza.CentreDm.Y - wellFootprint.Z / 2);
            plots.Add(new BuildingPlot(
                (int)KentridgeRole.Well,
                StructureArchetype.Well,
                DistrictKind.Market,
                wellPosition,
                FrontageDirection.South,
                new PlannedSiteAccess(SiteAccessKind.Plaza, plaza.Id, plaza.CentreDm),
                new PublicAccessDirection(0, 1)));

            return new SettlementPlan(
                KentridgeDefinition.Id,
                seed,
                KentridgeDefinition.TownCentreDm,
                KentridgeDefinition.Theme,
                new List<PlannedStreet>(),
                routes,
                plaza,
                plots);
        }

        private static List<BuildingPlot> PlaceNamedSites(uint seed, PlannedPlaza plaza)
        {
            var plots = new List<BuildingPlot>(17);
            for (int i = 0; i < SiteSpecs.Length; i++)
            {
                OrganicSiteSpec spec = SiteSpecs[i];
                BuildingPlot placed;
                if (!TryPlaceSite(seed, in spec, plaza, plots, out placed))
                    throw new InvalidOperationException(
                        "Kentridge organic planner exhausted its bounded candidate set for " + spec.Role + ".");
                plots.Add(placed);
            }
            return plots;
        }

        private static bool TryPlaceSite(
            uint seed,
            in OrganicSiteSpec spec,
            PlannedPlaza plaza,
            List<BuildingPlot> placed,
            out BuildingPlot result)
        {
            Int2 preferred;
            Int2 radius;
            DistrictEnvelope(spec.District, out preferred, out radius);
            Int3 footprint = KentridgeDefinition.FootprintDm(spec.Archetype);

            for (int attempt = 0; attempt < MaxCandidatesPerSite; attempt++)
            {
                uint h0 = Hash(seed ^ ((uint)spec.Role + 1u) * 0x9E3779B9u ^ (uint)attempt * 0x85EBCA6Bu);
                uint h1 = Hash(h0 ^ 0xC2B2AE35u);
                int x = preferred.X + SignedRange(h0, radius.X);
                int z = preferred.Y + SignedRange(h1, radius.Y);
                x = (x / 5) * 5;
                z = (z / 5) * 5;

                if (x < PlanningMinXDm || z < PlanningMinZDm
                    || x + footprint.X > PlanningMaxXDm
                    || z + footprint.Z > PlanningMaxZDm)
                    continue;
                if (IntersectsPlaza(x, z, footprint, plaza, SiteClearanceDm))
                    continue;
                if (IntersectsPlaced(x, z, footprint, placed, SiteClearanceDm))
                    continue;

                Int2 centre = new Int2(x + footprint.X / 2, z + footprint.Z / 2);
                int dx = centre.X - plaza.CentreDm.X;
                int dz = centre.Y - plaza.CentreDm.Y;
                if (dx == 0 && dz == 0) continue;

                PublicAccessDirection inward = new PublicAccessDirection(dx, dz);
                FrontageDirection frontage = SnapFrontage(inward);
                Int2 accessPoint = PublicNetworkPoint(centre, footprint, inward);
                string routeId = "organic-access-" + (int)spec.Role;

                result = new BuildingPlot(
                    (int)spec.Role,
                    spec.Archetype,
                    spec.District,
                    new Int2(x, z),
                    frontage,
                    new PlannedSiteAccess(SiteAccessKind.Route, routeId, accessPoint),
                    inward);
                return true;
            }

            result = default(BuildingPlot);
            return false;
        }

        private static List<PlannedRoute> InferCirculation(
            uint seed, List<BuildingPlot> plots, PlannedPlaza plaza)
        {
            var routes = new List<PlannedRoute>(plots.Count);
            var terminals = new List<Int2>(plots.Count + 1) { plaza.CentreDm };
            for (int i = 0; i < plots.Count; i++)
            {
                BuildingPlot plot = plots[i];
                Int2 start = plot.Access.NetworkPointDm;
                Int2 target = NearestTerminal(start, terminals);
                Int2 bend = OrganicBend(seed, plot.RoleId, start, target);
                routes.Add(new PlannedRoute(plot.Access.TargetId, RouteWidth(plot.District), start, bend, target));
                terminals.Add(start);
            }
            return routes;
        }

        private static Int2 NearestTerminal(Int2 point, List<Int2> terminals)
        {
            Int2 best = terminals[0];
            long bestDistance = SquaredDistance(point, best);
            for (int i = 1; i < terminals.Count; i++)
            {
                long distance = SquaredDistance(point, terminals[i]);
                if (distance < bestDistance
                    || distance == bestDistance && LexicographicallyBefore(terminals[i], best))
                {
                    best = terminals[i];
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static Int2 OrganicBend(uint seed, int roleId, Int2 a, Int2 b)
        {
            int dx = b.X - a.X;
            int dz = b.Y - a.Y;
            uint h = Hash(seed ^ ((uint)roleId + 17u) * 0x27D4EB2Du);
            int magnitude = 24 + (int)(h % 33u);
            int sign = (h & 0x100u) == 0 ? -1 : 1;
            int px = Math.Sign(-dz);
            int pz = Math.Sign(dx);
            if (px == 0 && pz == 0) px = 1;
            return new Int2(
                (a.X + b.X) / 2 + px * magnitude * sign,
                (a.Y + b.Y) / 2 + pz * magnitude * sign);
        }

        private static Int2 PublicNetworkPoint(Int2 centre, Int3 footprint, PublicAccessDirection inward)
        {
            return new Int2(
                centre.X - inward.X * (footprint.X / 2 + 12),
                centre.Y - inward.Z * (footprint.Z / 2 + 12));
        }

        private static FrontageDirection SnapFrontage(PublicAccessDirection inward)
        {
            if (Math.Abs(inward.X) > Math.Abs(inward.Z))
                return inward.X < 0 ? FrontageDirection.West : FrontageDirection.East;
            return inward.Z < 0 ? FrontageDirection.North : FrontageDirection.South;
        }

        private static void DistrictEnvelope(DistrictKind district, out Int2 centre, out Int2 radius)
        {
            Int2 town = KentridgeDefinition.TownCentreDm;
            switch (district)
            {
                case DistrictKind.Civic:
                    centre = new Int2(town.X - 20, town.Y - 255);
                    radius = new Int2(320, 190);
                    break;
                case DistrictKind.Market:
                    centre = new Int2(town.X - 35, town.Y + 40);
                    radius = new Int2(380, 245);
                    break;
                case DistrictKind.Residential:
                    centre = new Int2(town.X - 90, town.Y + 310);
                    radius = new Int2(500, 300);
                    break;
                case DistrictKind.Working:
                    centre = new Int2(town.X + 330, town.Y + 250);
                    radius = new Int2(245, 210);
                    break;
                default:
                    centre = new Int2(town.X + 310, town.Y - 245);
                    radius = new Int2(280, 210);
                    break;
            }
        }

        private static bool IntersectsPlaced(
            int x, int z, Int3 footprint, List<BuildingPlot> plots, int clearance)
        {
            for (int i = 0; i < plots.Count; i++)
            {
                BuildingPlot other = plots[i];
                Int3 otherFootprint = KentridgeDefinition.FootprintDm(other.Archetype);
                if (RectanglesIntersect(
                    x - clearance, z - clearance,
                    x + footprint.X + clearance, z + footprint.Z + clearance,
                    other.PositionDm.X, other.PositionDm.Y,
                    other.PositionDm.X + otherFootprint.X,
                    other.PositionDm.Y + otherFootprint.Z))
                    return true;
            }
            return false;
        }

        private static bool IntersectsPlaza(
            int x, int z, Int3 footprint, PlannedPlaza plaza, int clearance)
        {
            int minX = plaza.CentreDm.X - plaza.SizeDm.X / 2;
            int maxX = plaza.CentreDm.X + plaza.SizeDm.X / 2;
            int minZ = plaza.CentreDm.Y - plaza.SizeDm.Y / 2;
            int maxZ = plaza.CentreDm.Y + plaza.SizeDm.Y / 2;
            return RectanglesIntersect(
                x - clearance, z - clearance,
                x + footprint.X + clearance, z + footprint.Z + clearance,
                minX, minZ, maxX, maxZ);
        }

        private static bool RectanglesIntersect(
            int minX, int minZ, int maxX, int maxZ,
            int otherMinX, int otherMinZ, int otherMaxX, int otherMaxZ) =>
            maxX > otherMinX && minX < otherMaxX && maxZ > otherMinZ && minZ < otherMaxZ;

        private static long SquaredDistance(Int2 a, Int2 b)
        {
            long dx = (long)a.X - b.X;
            long dz = (long)a.Y - b.Y;
            return dx * dx + dz * dz;
        }

        private static bool LexicographicallyBefore(Int2 a, Int2 b) =>
            a.X < b.X || a.X == b.X && a.Y < b.Y;

        private static int SignedRange(uint hash, int radius)
        {
            int span = radius * 2 + 1;
            return (int)(hash % (uint)span) - radius;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private static int RouteWidth(DistrictKind district)
        {
            switch (district)
            {
                case DistrictKind.Civic: return 28;
                case DistrictKind.Market: return 26;
                case DistrictKind.Working: return 22;
                case DistrictKind.Noble: return 20;
                default: return 18;
            }
        }

        private static SettlementCompositionPolicy BuildCompositionPolicy()
        {
            const SettlementArchetypeMask generatedHouses =
                SettlementArchetypeMask.Townhouse |
                SettlementArchetypeMask.WideHouse |
                SettlementArchetypeMask.Shop |
                SettlementArchetypeMask.Inn;

            var palette = new SettlementStructurePalette(
                new SettlementPaletteEntry(TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Noble, 1),
                new SettlementPaletteEntry(FarmhousePresetId, generatedHouses, SettlementDistrictMask.Noble, 4),
                new SettlementPaletteEntry(CompactHousePresetId, generatedHouses,
                    SettlementDistrictMask.Market | SettlementDistrictMask.Working, 3),
                new SettlementPaletteEntry(TallTownhousePresetId, generatedHouses,
                    SettlementDistrictMask.Market | SettlementDistrictMask.Working, 2),
                new SettlementPaletteEntry(CompactHousePresetId, generatedHouses, SettlementDistrictMask.Civic, 1),
                new SettlementPaletteEntry(TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Civic, 3),
                new SettlementPaletteEntry(CompactHousePresetId, generatedHouses, SettlementDistrictMask.Residential, 2),
                new SettlementPaletteEntry(FarmhousePresetId, generatedHouses, SettlementDistrictMask.Residential, 3),
                new SettlementPaletteEntry(TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Residential, 3));

            var defaultLot = new SettlementLotConfig(
                new SettlementIntRange(72, 240),
                new SettlementIntRange(72, 240),
                16, 10, 6, 12,
                SettlementFrontageMask.Cardinal,
                true,
                100);

            var density = new SettlementDensityPolicy(
                occupancyPercent: 68,
                minSpacingDm: SiteClearanceDm,
                maxCandidatesPerRegion: MaxCandidatesPerSite,
                maxPlanningSpanDm: 2400,
                planningScope: SettlementPlanningScope.RegionLocal);

            var landmarks = new[]
            {
                new SettlementLandmarkRule(
                    SettlementLandmarkKind.Church,
                    ParishChurchPresetId,
                    SettlementDistrictMask.Civic,
                    rarityDenominator: 1,
                    maxPerPlan: 1,
                    minSpacingDm: 300,
                    preferOpenSpace: true),
            };

            var openSpaces = new[]
            {
                new SettlementOpenSpaceRule(
                    MarketSquareId,
                    KentridgeDefinition.TownCentreDm,
                    new Int2(220, 140),
                    clearanceDm: 12),
            };

            return new SettlementCompositionPolicy(defaultLot, palette, density, landmarks, openSpaces);
        }
    }
}