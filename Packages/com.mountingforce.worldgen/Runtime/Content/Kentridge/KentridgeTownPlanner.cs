using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// First semantic town-layout pass for Kentridge.
    ///
    /// The topology is authored as relationships (main spine, market crossing, residential street,
    /// east service lane) while individual plots are derived from street frontage. Ordinary houses
    /// may slide a few decimetres along their street from a stable per-role hash; they never drift
    /// away from their district or rotate away from their frontage.
    /// </summary>
    public static class KentridgeTownPlanner
    {
        public const int MainSpineXDm = 1050;
        public const int MarketStreetZDm = 520;
        public const int ResidentialStreetZDm = 900;
        public const int EastLaneXDm = 1370;

        public const int MainRoadWidthDm = 56;
        public const int SecondaryRoadWidthDm = 48;
        public const int ResidentialRoadWidthDm = 44;
        public const int ServiceRoadWidthDm = 36;

        public static SettlementPlan Build(uint seed)
        {
            var streets = new List<PlannedStreet>(4)
            {
                new PlannedStreet(
                    "north-south-spine",
                    StreetKind.MainRoad,
                    MainRoadWidthDm,
                    new Int2(MainSpineXDm, -80),
                    new Int2(MainSpineXDm, 1080)),

                new PlannedStreet(
                    "market-street",
                    StreetKind.Secondary,
                    SecondaryRoadWidthDm,
                    new Int2(520, MarketStreetZDm),
                    new Int2(1540, MarketStreetZDm)),

                new PlannedStreet(
                    "residential-street",
                    StreetKind.Secondary,
                    ResidentialRoadWidthDm,
                    new Int2(500, ResidentialStreetZDm),
                    new Int2(1510, ResidentialStreetZDm)),

                new PlannedStreet(
                    "east-service-lane",
                    StreetKind.Service,
                    ServiceRoadWidthDm,
                    new Int2(EastLaneXDm, 60),
                    new Int2(EastLaneXDm, 1080)),
            };

            var plaza = new PlannedPlaza(
                "market-square",
                KentridgeDefinition.TownCentreDm,
                new Int2(220, 140));

            var plots = new List<BuildingPlot>(17)
            {
                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Church, StructureArchetype.Church, DistrictKind.Civic,
                    MainSpineXDm, 150, FrontageDirection.East, MainRoadWidthDm, 24, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.MayorHouse, StructureArchetype.WideHouse, DistrictKind.Civic,
                    MainSpineXDm, 150, FrontageDirection.West, MainRoadWidthDm, 24, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Inn, StructureArchetype.Inn, DistrictKind.Market,
                    MainSpineXDm, 340, FrontageDirection.East, MainRoadWidthDm, 24, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.WeaponShop, StructureArchetype.Shop, DistrictKind.Market,
                    650, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.ArmorShop, StructureArchetype.Shop, DistrictKind.Market,
                    790, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.MagicShop, StructureArchetype.Shop, DistrictKind.Market,
                    930, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 31, KentridgeRole.RebeccaHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    1200, MarketStreetZDm, FrontageDirection.North, SecondaryRoadWidthDm, 18, 6),

                AlongVerticalStreet(
                    seed, 32, KentridgeRole.LoganHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    MainSpineXDm, 760, FrontageDirection.East, MainRoadWidthDm, 24, 8),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Pub, StructureArchetype.Inn, DistrictKind.Market,
                    MainSpineXDm, 760, FrontageDirection.West, MainRoadWidthDm, 24, 0),

                AlongHorizontalStreet(
                    seed, 41, KentridgeRole.SarahHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    600, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 42, KentridgeRole.KatieHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    760, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 43, KentridgeRole.MedrareHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    910, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 44, KentridgeRole.AbandonedHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    1180, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongVerticalStreet(
                    seed, 45, KentridgeRole.AwonHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    EastLaneXDm, 950, FrontageDirection.West, ServiceRoadWidthDm, 22, 8),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Warehouse, StructureArchetype.Warehouse, DistrictKind.Working,
                    EastLaneXDm, 700, FrontageDirection.West, ServiceRoadWidthDm, 22, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.RadcliffeMansion, StructureArchetype.Mansion, DistrictKind.Noble,
                    EastLaneXDm, 250, FrontageDirection.West, ServiceRoadWidthDm, 22, 0),

                CentrePlot(
                    KentridgeRole.Well,
                    StructureArchetype.Well,
                    DistrictKind.Market,
                    KentridgeDefinition.TownCentreDm),
            };

            return new SettlementPlan(
                KentridgeDefinition.Id,
                seed,
                KentridgeDefinition.TownCentreDm,
                KentridgeDefinition.Theme,
                streets,
                plaza,
                plots);
        }

        private static BuildingPlot AlongHorizontalStreet(
            uint seed, uint salt, KentridgeRole role, StructureArchetype archetype,
            DistrictKind district, int frontageXDm, int streetZDm,
            FrontageDirection frontage, int roadWidthDm, int setbackDm, int jitterDm)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            int along = frontageXDm + SignedJitter(seed, salt, jitterDm);
            int x = along - footprint.X / 2;
            int z;

            switch (frontage)
            {
                case FrontageDirection.South:
                    z = streetZDm + roadWidthDm / 2 + setbackDm;
                    break;
                case FrontageDirection.North:
                    z = streetZDm - roadWidthDm / 2 - setbackDm - footprint.Z;
                    break;
                default:
                    throw new System.ArgumentException(
                        "Horizontal street plots must face north or south.", nameof(frontage));
            }

            return new BuildingPlot((int)role, archetype, district, new Int2(x, z), frontage);
        }

        private static BuildingPlot AlongVerticalStreet(
            uint seed, uint salt, KentridgeRole role, StructureArchetype archetype,
            DistrictKind district, int streetXDm, int frontageZDm,
            FrontageDirection frontage, int roadWidthDm, int setbackDm, int jitterDm)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            int along = frontageZDm + SignedJitter(seed, salt, jitterDm);
            int z = along - footprint.Z / 2;
            int x;

            switch (frontage)
            {
                case FrontageDirection.West:
                    x = streetXDm + roadWidthDm / 2 + setbackDm;
                    break;
                case FrontageDirection.East:
                    x = streetXDm - roadWidthDm / 2 - setbackDm - footprint.X;
                    break;
                default:
                    throw new System.ArgumentException(
                        "Vertical street plots must face east or west.", nameof(frontage));
            }

            return new BuildingPlot((int)role, archetype, district, new Int2(x, z), frontage);
        }

        private static BuildingPlot CentrePlot(
            KentridgeRole role, StructureArchetype archetype, DistrictKind district, Int2 centreDm)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            return new BuildingPlot(
                (int)role,
                archetype,
                district,
                new Int2(centreDm.X - footprint.X / 2, centreDm.Y - footprint.Z / 2),
                FrontageDirection.South);
        }

        private static int SignedJitter(uint seed, uint salt, int magnitude)
        {
            if (magnitude <= 0) return 0;

            uint x = seed ^ (salt * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;

            int span = magnitude * 2 + 1;
            return (int)(x % (uint)span) - magnitude;
        }
    }
}
