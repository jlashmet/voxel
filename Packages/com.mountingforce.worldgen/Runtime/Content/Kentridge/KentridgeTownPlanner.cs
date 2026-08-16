using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// First semantic town-layout pass for Kentridge.
    ///
    /// The topology is authored as relationships (main spine, market crossing, residential street,
    /// east service lane) while individual plots are derived from reusable Core frontage placement.
    /// Ordinary houses may slide a few decimetres along their street from a stable per-role hash;
    /// they never drift away from their district or rotate away from their frontage.
    /// </summary>
    public static class KentridgeTownPlanner
    {
        public const string MainSpineId = "north-south-spine";
        public const string MarketStreetId = "market-street";
        public const string ResidentialStreetId = "residential-street";
        public const string EastServiceLaneId = "east-service-lane";
        public const string MarketSquareId = "market-square";

        public const int MainSpineXDm = 1170;
        public const int MarketStreetZDm = 520;
        public const int ResidentialStreetZDm = 900;
        public const int EastLaneXDm = 1490;

        public const int MainRoadWidthDm = 56;
        public const int SecondaryRoadWidthDm = 48;
        public const int ResidentialRoadWidthDm = 44;
        public const int ServiceRoadWidthDm = 36;

        public static SettlementPlan Build(uint seed)
        {
            var streets = new List<PlannedStreet>(4)
            {
                new PlannedStreet(
                    MainSpineId,
                    StreetKind.MainRoad,
                    MainRoadWidthDm,
                    new Int2(MainSpineXDm, -80),
                    new Int2(MainSpineXDm, 1080)),

                new PlannedStreet(
                    MarketStreetId,
                    StreetKind.Secondary,
                    SecondaryRoadWidthDm,
                    new Int2(700, MarketStreetZDm),
                    new Int2(1660, MarketStreetZDm)),

                new PlannedStreet(
                    ResidentialStreetId,
                    StreetKind.Secondary,
                    ResidentialRoadWidthDm,
                    new Int2(650, ResidentialStreetZDm),
                    new Int2(1630, ResidentialStreetZDm)),

                new PlannedStreet(
                    EastServiceLaneId,
                    StreetKind.Service,
                    ServiceRoadWidthDm,
                    new Int2(EastLaneXDm, 60),
                    new Int2(EastLaneXDm, 1080)),
            };

            var plaza = new PlannedPlaza(
                MarketSquareId,
                KentridgeDefinition.TownCentreDm,
                new Int2(220, 140));

            var plots = new List<BuildingPlot>(17)
            {
                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Church, StructureArchetype.Church, DistrictKind.Civic,
                    MainSpineId, MainSpineXDm, 150, FrontageDirection.East, MainRoadWidthDm, 24, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.MayorHouse, StructureArchetype.WideHouse, DistrictKind.Civic,
                    MainSpineId, MainSpineXDm, 150, FrontageDirection.West, MainRoadWidthDm, 24, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Inn, StructureArchetype.Inn, DistrictKind.Market,
                    MainSpineId, MainSpineXDm, 340, FrontageDirection.East, MainRoadWidthDm, 24, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.WeaponShop, StructureArchetype.Shop, DistrictKind.Market,
                    MarketStreetId, 770, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.ArmorShop, StructureArchetype.Shop, DistrictKind.Market,
                    MarketStreetId, 910, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 0, KentridgeRole.MagicShop, StructureArchetype.Shop, DistrictKind.Market,
                    MarketStreetId, 1050, MarketStreetZDm, FrontageDirection.South, SecondaryRoadWidthDm, 18, 0),

                AlongHorizontalStreet(
                    seed, 31, KentridgeRole.RebeccaHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    MarketStreetId, 1320, MarketStreetZDm, FrontageDirection.North, SecondaryRoadWidthDm, 18, 6),

                AlongVerticalStreet(
                    seed, 32, KentridgeRole.LoganHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    MainSpineId, MainSpineXDm, 760, FrontageDirection.East, MainRoadWidthDm, 24, 8),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Pub, StructureArchetype.Inn, DistrictKind.Market,
                    MainSpineId, MainSpineXDm, 760, FrontageDirection.West, MainRoadWidthDm, 24, 0),

                AlongHorizontalStreet(
                    seed, 41, KentridgeRole.SarahHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    ResidentialStreetId, 720, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 42, KentridgeRole.KatieHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    ResidentialStreetId, 880, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 43, KentridgeRole.MedrareHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    ResidentialStreetId, 1030, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongHorizontalStreet(
                    seed, 44, KentridgeRole.AbandonedHouse, StructureArchetype.Townhouse, DistrictKind.Residential,
                    ResidentialStreetId, 1300, ResidentialStreetZDm, FrontageDirection.South, ResidentialRoadWidthDm, 16, 8),

                AlongVerticalStreet(
                    seed, 45, KentridgeRole.AwonHouse, StructureArchetype.WideHouse, DistrictKind.Residential,
                    EastServiceLaneId, EastLaneXDm, 950, FrontageDirection.West, ServiceRoadWidthDm, 22, 8),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.Warehouse, StructureArchetype.Warehouse, DistrictKind.Working,
                    EastServiceLaneId, EastLaneXDm, 700, FrontageDirection.West, ServiceRoadWidthDm, 22, 0),

                AlongVerticalStreet(
                    seed, 0, KentridgeRole.RadcliffeMansion, StructureArchetype.Mansion, DistrictKind.Noble,
                    EastServiceLaneId, EastLaneXDm, 250, FrontageDirection.West, ServiceRoadWidthDm, 22, 0),

                CentrePlot(
                    KentridgeRole.Well,
                    StructureArchetype.Well,
                    DistrictKind.Market,
                    MarketSquareId,
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
            DistrictKind district, string streetId, int frontageXDm, int streetZDm,
            FrontageDirection frontage, int roadWidthDm, int setbackDm, int jitterDm)
        {
            return SettlementPlotLayout.AlongHorizontalStreet(
                seed,
                salt,
                (int)role,
                archetype,
                district,
                streetId,
                frontageXDm,
                streetZDm,
                frontage,
                roadWidthDm,
                setbackDm,
                jitterDm,
                KentridgeDefinition.FootprintDm(archetype));
        }

        private static BuildingPlot AlongVerticalStreet(
            uint seed, uint salt, KentridgeRole role, StructureArchetype archetype,
            DistrictKind district, string streetId, int streetXDm, int frontageZDm,
            FrontageDirection frontage, int roadWidthDm, int setbackDm, int jitterDm)
        {
            return SettlementPlotLayout.AlongVerticalStreet(
                seed,
                salt,
                (int)role,
                archetype,
                district,
                streetId,
                streetXDm,
                frontageZDm,
                frontage,
                roadWidthDm,
                setbackDm,
                jitterDm,
                KentridgeDefinition.FootprintDm(archetype));
        }

        private static BuildingPlot CentrePlot(
            KentridgeRole role, StructureArchetype archetype, DistrictKind district,
            string plazaId, Int2 centreDm)
        {
            return SettlementPlotLayout.CentreOnPlaza(
                (int)role,
                archetype,
                district,
                plazaId,
                centreDm,
                KentridgeDefinition.FootprintDm(archetype));
        }
    }
}
