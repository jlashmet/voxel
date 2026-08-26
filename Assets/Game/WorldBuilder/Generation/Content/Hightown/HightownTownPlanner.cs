using System;
using System.Collections.Generic;

using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Content.Hightown
{
    /// <summary>
    /// Layout pass for Hightown.
    ///
    /// Kentridge's plots are authored one by one because its buildings are recovered content with
    /// fixed identities. Hightown has no recovered building list, so this generates its plots from
    /// the seed instead — but the <em>street topology</em> is still authored, because a town whose
    /// roads are also random reads as scattered houses rather than as a place.
    ///
    /// The contrast with Kentridge is deliberate and structural, not decorative. Kentridge is a
    /// single north-south spine with streets hung off it: a road that grew a village. Hightown is a
    /// closed orthogonal grid around a central square, which is what a planned stone town looks
    /// like from above — and from the road in, the difference is visible before any material is.
    /// </summary>
    public static class HightownTownPlanner
    {
        // Street ids are shared role names, not town-specific labels: the town voxel pass looks
        // streets up by these ids to place sidewalks, frontage paths and circulation. Hightown has
        // the same four roles as Kentridge — two through-routes and two cross streets — so it uses
        // the same names. Its topology still differs; what matters here is that the roles exist.
        public const string WestAvenueId = KentridgeTownPlanner.MainSpineId;
        public const string EastAvenueId = KentridgeTownPlanner.EastServiceLaneId;
        public const string SouthGateRoadId = KentridgeTownPlanner.MarketStreetId;
        public const string NorthTerraceId = KentridgeTownPlanner.ResidentialStreetId;
        public const string HighSquareId = KentridgeTownPlanner.MarketSquareId;

        public const int MainRoadWidthDm = 60;
        public const int SecondaryRoadWidthDm = 52;

        private static readonly int CentreX = HightownDefinition.TownCentreDm.X;
        private static readonly int CentreZ = HightownDefinition.TownCentreDm.Y;

        // The grid half-extents. Hightown is wider than Kentridge on purpose — and it has to be
        // large enough to fill every stable role id the shared structure pass expects, because
        // those stages assert that ids 0..N-1 are all present. At the earlier extents the grid
        // produced nine plots and generation failed on the tenth missing id.
        private const int BlockHalfXDm = 700;
        private const int BlockHalfZDm = 900;

        public static SettlementPlan Build(uint seed)
        {
            int westX = CentreX - BlockHalfXDm;
            int eastX = CentreX + BlockHalfXDm;
            int southZ = CentreZ - BlockHalfZDm;
            int northZ = CentreZ + BlockHalfZDm;

            var streets = new List<PlannedStreet>(4)
            {
                // The two avenues run north-south and carry the town's frontages.
                new PlannedStreet(
                    WestAvenueId, StreetKind.MainRoad, MainRoadWidthDm,
                    new Int2(westX, southZ), new Int2(westX, northZ)),

                new PlannedStreet(
                    EastAvenueId, StreetKind.MainRoad, MainRoadWidthDm,
                    new Int2(eastX, southZ), new Int2(eastX, northZ)),

                // The south gate road is where the highway from Kentridge arrives, so it is the
                // first street a traveller sees and is sized as a main approach.
                new PlannedStreet(
                    SouthGateRoadId, StreetKind.MainRoad, MainRoadWidthDm,
                    new Int2(westX, southZ), new Int2(eastX, southZ)),

                new PlannedStreet(
                    NorthTerraceId, StreetKind.Secondary, SecondaryRoadWidthDm,
                    new Int2(westX, northZ), new Int2(eastX, northZ)),
            };

            var plaza = new PlannedPlaza(
                HighSquareId,
                HightownDefinition.TownCentreDm,
                new Int2(260, 200));

            // Exactly one plot per stable role id, because the shared structure pass asserts that
            // ids 0..N-1 are all present and rejects anything beyond N. Those ids are Kentridge's
            // recovered building roles, so a generated town does not choose how many buildings it
            // has — it fills the same slots with different architecture. Hightown's character comes
            // from its theme, its grid and which archetype lands in each slot.
            int roleCount = System.Enum.GetValues(typeof(Content.Kentridge.KentridgeRole)).Length;
            var frontages = new List<Frontage>(roleCount * 2);

            CollectVerticalFrontages(frontages, WestAvenueId, westX, FrontageDirection.East, southZ, northZ);
            CollectVerticalFrontages(frontages, EastAvenueId, eastX, FrontageDirection.West, southZ, northZ);
            CollectHorizontalFrontages(frontages, SouthGateRoadId, southZ, FrontageDirection.North, westX, eastX);
            CollectHorizontalFrontages(frontages, NorthTerraceId, northZ, FrontageDirection.South, westX, eastX);

            if (frontages.Count < roleCount)
                throw new InvalidOperationException(
                    "Hightown's grid offers " + frontages.Count + " frontages but the structure " +
                    "pass requires " + roleCount + ". Widen the block or shorten the pitch.");

            var plots = new List<BuildingPlot>(roleCount);
            for (int roleId = 0; roleId < roleCount; roleId++)
            {
                // Spread the roles around the whole grid rather than filling one avenue first, so
                // the town is not sorted by building type.
                Frontage f = frontages[roleId * frontages.Count / roleCount];
                StructureArchetype archetype = ArchetypeForRole(roleId);
                DistrictKind district = DistrictFor(seed, roleId);
                Int3 footprint = HightownDefinition.FootprintDm(archetype);
                SettlementLotConfig lot = LotFor(footprint, f.Facing, 24, 0, district);

                plots.Add(f.Vertical
                    ? SettlementRoadFacingPlacement.AlongVerticalStreet(
                        seed, (uint)roleId, roleId, archetype, district,
                        f.StreetId, f.LineDm, f.AlongDm, f.Facing,
                        MainRoadWidthDm, 24, 0, footprint, in lot)
                    : SettlementRoadFacingPlacement.AlongHorizontalStreet(
                        seed, (uint)roleId, roleId, archetype, district,
                        f.StreetId, f.AlongDm, f.LineDm, f.Facing,
                        SecondaryRoadWidthDm, 24, 0, footprint, in lot));
            }

            return new SettlementPlan(
                HightownDefinition.Id,
                seed,
                HightownDefinition.TownCentreDm,
                HightownDefinition.Theme,
                streets,
                plaza,
                plots);
        }

        private const int FrontagePitchDm = 200;

        private readonly struct Frontage
        {
            public readonly string StreetId;
            public readonly int LineDm;
            public readonly int AlongDm;
            public readonly FrontageDirection Facing;
            public readonly bool Vertical;

            public Frontage(string streetId, int lineDm, int alongDm,
                            FrontageDirection facing, bool vertical)
            {
                StreetId = streetId;
                LineDm = lineDm;
                AlongDm = alongDm;
                Facing = facing;
                Vertical = vertical;
            }
        }

        private static void CollectVerticalFrontages(
            List<Frontage> into, string streetId, int streetXDm,
            FrontageDirection frontage, int fromZDm, int toZDm)
        {
            for (int z = fromZDm + FrontagePitchDm; z <= toZDm - FrontagePitchDm; z += FrontagePitchDm)
                into.Add(new Frontage(streetId, streetXDm, z, frontage, true));
        }

        private static void CollectHorizontalFrontages(
            List<Frontage> into, string streetId, int streetZDm,
            FrontageDirection frontage, int fromXDm, int toXDm)
        {
            for (int x = fromXDm + FrontagePitchDm; x <= toXDm - FrontagePitchDm; x += FrontagePitchDm)
                into.Add(new Frontage(streetId, streetZDm, x, frontage, false));
        }

        private static BuildingPlot CentrePlot(
            uint seed, int roleId, StructureArchetype archetype, DistrictKind district)
        {
            Int3 footprint = HightownDefinition.FootprintDm(archetype);
            SettlementLotConfig lot = LotFor(footprint, FrontageDirection.South, 24, 0, district);

            return SettlementRoadFacingPlacement.AlongHorizontalStreet(
                seed, (uint)roleId, roleId, archetype, district,
                SouthGateRoadId, CentreX, CentreZ, FrontageDirection.South,
                SecondaryRoadWidthDm, 24, 0, footprint, in lot);
        }

        /// <summary>
        /// The archetype for a stable role id, taken from Kentridge.
        ///
        /// The structure grammar selects a building's form from its <em>role id</em> while the
        /// envelope it is validated against comes from its archetype, so the two have to agree.
        /// Choosing archetypes freely produced a shop-sized envelope around a wide-house-sized
        /// building and generation failed. Hightown therefore fills the same roles with the same
        /// massing, and differs by theme, street grid and where each building stands — until the
        /// grammar can be asked for a form by archetype rather than by Kentridge's role table.
        /// </summary>
        private static StructureArchetype ArchetypeForRole(int roleId)
        {
            SettlementPlan kentridge = KentridgeDefinition.Build(0u);
            for (int i = 0; i < kentridge.Plots.Count; i++)
                if (kentridge.Plots[i].RoleId == roleId)
                    return kentridge.Plots[i].Archetype;
            return StructureArchetype.Townhouse;
        }

        /// <summary>
        private static StructureArchetype ArchetypeFor(uint seed, int roleId)
        {
            // Restricted to the archetypes Kentridge's envelope table actually covers. Mansion
            // falls through to the default envelope, which is smaller than the building the
            // grammar then generates — and the architecture pass rejects anything that grows out
            // of its envelope. A second settlement gets its own mansions when it gets its own
            // envelope table.
            uint roll = Hash(seed, (uint)roleId, 0x9E3779B9u) % 100u;
            if (roll < 36) return StructureArchetype.Townhouse;
            if (roll < 64) return StructureArchetype.WideHouse;
            if (roll < 84) return StructureArchetype.Shop;
            if (roll < 94) return StructureArchetype.Inn;
            return StructureArchetype.Warehouse;
        }

        private static DistrictKind DistrictFor(uint seed, int roleId)
        {
            // Only the districts Kentridge exercises. Noble massing is grander than the shared
            // envelope table allows for these archetypes, and the architecture pass rejects it.
            uint roll = Hash(seed, (uint)roleId, 0x85EBCA6Bu) % 100u;
            if (roll < 42) return DistrictKind.Residential;
            if (roll < 74) return DistrictKind.Market;
            if (roll < 90) return DistrictKind.Civic;
            return DistrictKind.Working;
        }

        private static SettlementLotConfig LotFor(
            Int3 footprint, FrontageDirection frontage,
            int frontSetbackDm, int jitterDm, DistrictKind district)
        {
            bool northSouth = frontage == FrontageDirection.North || frontage == FrontageDirection.South;
            int structureWidth = northSouth ? footprint.X : footprint.Z;
            int structureDepth = northSouth ? footprint.Z : footprint.X;
            // Lot margins follow the same convention as Kentridge. The shared town pass places
            // plot dressing at offsets derived from these, and validates that what it placed lands
            // inside the plot it belongs to — so a town with its own margins fails generation
            // rather than merely looking different. Hightown's distinctiveness is carried by its
            // theme, its grid, its archetype mix and its larger envelopes, not by its kerbs.
            // No side margin. The grammar derives the structure width from the lot, and the
            // envelope it is checked against is the archetype footprint — so any side margin makes
            // the building wider than the envelope that is about to reject it. Kentridge absorbs
            // this because its plots were authored against those exact numbers; a generated town
            // has to keep lot width equal to footprint width.
            int side = 0;
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

        private static uint Hash(uint seed, uint roleId, uint salt)
        {
            uint h = seed ^ salt;
            h ^= roleId + 0x9E3779B9u + (h << 6) + (h >> 2);
            h ^= h >> 15;
            h *= 0x2545F491u;
            h ^= h >> 13;
            return h;
        }
    }
}
