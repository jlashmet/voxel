using System;
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

        // Core deliberately treats preset IDs as opaque strings so it never depends on a game or
        // voxel structure assembly. These values follow the shared <archetype>.<variant>.vN convention.
        public const string CompactHousePresetId = "house.compact-cabin.v1";
        public const string FarmhousePresetId = "house.farmhouse.v1";
        public const string TallTownhousePresetId = "house.tall-townhouse.v1";
        public const string ParishChurchPresetId = "church.parish.v1";

        public const int MainSpineXDm = 1170;
        public const int MarketStreetZDm = 520;
        public const int ResidentialStreetZDm = 900;
        public const int EastLaneXDm = 1490;

        public const int MainRoadWidthDm = 56;
        public const int SecondaryRoadWidthDm = 48;
        public const int ResidentialRoadWidthDm = 44;
        public const int ServiceRoadWidthDm = 36;

        private static readonly SettlementCompositionPolicy Policy = BuildCompositionPolicy();
        public static SettlementCompositionPolicy CompositionPolicy => Policy;

        public static SettlementPlan Build(uint seed)
        {
            // Keep the bound check at the production entry point as well as policy construction so a
            // future editable policy cannot silently turn Kentridge into an unbounded global planner.
            Policy.ValidateBounded();

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
                    seed,
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
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            SettlementLotConfig lot = LotFor(footprint, frontage, setbackDm, jitterDm, district);
            return SettlementRoadFacingPlacement.AlongHorizontalStreet(
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
                footprint,
                in lot);
        }

        private static BuildingPlot AlongVerticalStreet(
            uint seed, uint salt, KentridgeRole role, StructureArchetype archetype,
            DistrictKind district, string streetId, int streetXDm, int frontageZDm,
            FrontageDirection frontage, int roadWidthDm, int setbackDm, int jitterDm)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            SettlementLotConfig lot = LotFor(footprint, frontage, setbackDm, jitterDm, district);
            return SettlementRoadFacingPlacement.AlongVerticalStreet(
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
                footprint,
                in lot);
        }

        private static BuildingPlot CentrePlot(
            uint seed,
            KentridgeRole role, StructureArchetype archetype, DistrictKind district,
            string plazaId, Int2 centreDm)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            int width = Math.Max(footprint.X, footprint.Z);
            var plazaLot = new SettlementLotConfig(
                new SettlementIntRange(width, width),
                new SettlementIntRange(width, width),
                0,
                0,
                0,
                0,
                SettlementFrontageMask.Cardinal,
                false,
                100);
            return SettlementRoadFacingPlacement.CentreOnPlaza(
                seed,
                (int)role,
                archetype,
                district,
                plazaId,
                centreDm,
                footprint,
                in plazaLot);
        }

        private static SettlementLotConfig LotFor(
            Int3 footprint,
            FrontageDirection frontage,
            int frontSetbackDm,
            int jitterDm,
            DistrictKind district)
        {
            bool northSouth = frontage == FrontageDirection.North || frontage == FrontageDirection.South;
            int structureWidth = northSouth ? footprint.X : footprint.Z;
            int structureDepth = northSouth ? footprint.Z : footprint.X;
            int side = district == DistrictKind.Market ? 4 : 6;
            int rear = district == DistrictKind.Working ? 6 : 10;
            int variation = Math.Max(0, jitterDm);
            int minSpacing = district == DistrictKind.Market ? 8 : 12;

            return new SettlementLotConfig(
                new SettlementIntRange(
                    structureWidth + 2 * side,
                    structureWidth + 2 * side + 2 * variation),
                new SettlementIntRange(
                    structureDepth + frontSetbackDm + rear,
                    structureDepth + frontSetbackDm + rear + variation),
                frontSetbackDm,
                rear,
                side,
                minSpacing,
                SettlementFrontageMask.Cardinal,
                true,
                100);
        }

        private static SettlementCompositionPolicy BuildCompositionPolicy()
        {
            const SettlementArchetypeMask generatedHouses =
                SettlementArchetypeMask.Townhouse |
                SettlementArchetypeMask.WideHouse |
                SettlementArchetypeMask.Shop |
                SettlementArchetypeMask.Inn;

            // Entry ordering intentionally preserves the historical Kentridge modulus buckets while
            // moving the weights out of voxel geometry code and into settlement composition policy.
            var palette = new SettlementStructurePalette(
                new SettlementPaletteEntry(
                    TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Noble, 1),
                new SettlementPaletteEntry(
                    FarmhousePresetId, generatedHouses, SettlementDistrictMask.Noble, 4),

                new SettlementPaletteEntry(
                    CompactHousePresetId, generatedHouses,
                    SettlementDistrictMask.Market | SettlementDistrictMask.Working, 3),
                new SettlementPaletteEntry(
                    TallTownhousePresetId, generatedHouses,
                    SettlementDistrictMask.Market | SettlementDistrictMask.Working, 2),

                new SettlementPaletteEntry(
                    CompactHousePresetId, generatedHouses, SettlementDistrictMask.Civic, 1),
                new SettlementPaletteEntry(
                    TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Civic, 3),

                new SettlementPaletteEntry(
                    CompactHousePresetId, generatedHouses, SettlementDistrictMask.Residential, 2),
                new SettlementPaletteEntry(
                    FarmhousePresetId, generatedHouses, SettlementDistrictMask.Residential, 3),
                new SettlementPaletteEntry(
                    TallTownhousePresetId, generatedHouses, SettlementDistrictMask.Residential, 3));

            var defaultLot = new SettlementLotConfig(
                new SettlementIntRange(72, 240),
                new SettlementIntRange(72, 240),
                16,
                10,
                6,
                12,
                SettlementFrontageMask.Cardinal,
                true,
                100);

            var density = new SettlementDensityPolicy(
                occupancyPercent: 78,
                minSpacingDm: 12,
                maxCandidatesPerRegion: 256,
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

            return new SettlementCompositionPolicy(
                defaultLot,
                palette,
                density,
                landmarks,
                openSpaces);
        }
    }
}
