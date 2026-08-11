using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// Stable semantic roles recovered from the original MountingForce Kentridge TMX.
    /// The numeric value is content identity and must not be derived from generation order.
    /// </summary>
    public enum KentridgeRole : byte
    {
        Inn = 0,
        Pub = 1,
        Church = 2,
        MayorHouse = 3,
        WeaponShop = 4,
        ArmorShop = 5,
        MagicShop = 6,
        LoganHouse = 7,
        RebeccaHouse = 8,
        SarahHouse = 9,
        KatieHouse = 10,
        AwonHouse = 11,
        AbandonedHouse = 12,
        MedrareHouse = 13,
        Warehouse = 14,
        RadcliffeMansion = 15,
        Well = 16,
    }

    /// <summary>
    /// The authored intent for Kentridge. This is the source consumed by planners and can later be
    /// loaded from data rather than code. Nothing here knows that the first backend happens to be
    /// a 10 cm voxel world.
    /// </summary>
    public static class KentridgeDefinition
    {
        public const string Id = "kentridge";
        public static readonly Int2 TownCentreDm = new Int2(1050, 520);

        public static ArchitectureTheme Theme => new ArchitectureTheme(
            id: Id,
            foundation: MaterialRole.FoundationStone,
            wall: MaterialRole.Masonry,
            frame: MaterialRole.Timber,
            window: MaterialRole.Glass,
            roof: MaterialRole.RoofTile,
            accentStone: MaterialRole.DarkMasonry,
            foundationHeightDm: 7,
            wallThicknessDm: 4,
            floorHeightDm: 34,
            doorHeightDm: 24,
            windowBaseDm: 20,
            windowHeightDm: 12,
            beamWidthDm: 3,
            roofOverhangDm: 4,
            typicalRoofHeightDm: 24,
            grandRoofHeightDm: 32,
            upperStoreyOverhangDm: 5);

        /// <summary>
        /// Builds the first town plan. Named landmarks retain deliberate district relationships;
        /// ordinary story houses receive small independent deterministic offsets. A later road and
        /// plot solver can replace this method while preserving every role id and archetype contract.
        ///
        /// The current coordinates form a deliberately readable prototype: civic buildings north,
        /// market and shops through the middle, residences south/east, warehouse at the edge, and
        /// Radcliffe's mansion terminating the northeast route. They are not copied TMX coordinates.
        /// </summary>
        public static SettlementPlan Build(uint seed)
        {
            var sites = new List<PlannedSite>(17)
            {
                Jittered(seed, 11, KentridgeRole.MayorHouse,     StructureArchetype.WideHouse, 1240, 180, 2),
                Jittered(seed, 12, KentridgeRole.LoganHouse,     StructureArchetype.Townhouse, 1120, 650, 3),
                Jittered(seed, 13, KentridgeRole.RebeccaHouse,   StructureArchetype.Townhouse,  900, 600, 0),
                Jittered(seed, 14, KentridgeRole.SarahHouse,     StructureArchetype.WideHouse,  900, 980, 0),
                Jittered(seed, 15, KentridgeRole.KatieHouse,     StructureArchetype.Townhouse, 1100, 980, 2),
                Jittered(seed, 16, KentridgeRole.AwonHouse,      StructureArchetype.WideHouse, 1120, 800, 1),
                Jittered(seed, 17, KentridgeRole.AbandonedHouse, StructureArchetype.Townhouse, 1260, 940, 2),
                Jittered(seed, 18, KentridgeRole.MedrareHouse,   StructureArchetype.WideHouse,  700, 980, 0),

                new PlannedSite((int)KentridgeRole.WeaponShop, StructureArchetype.Shop, new Int2(700, 500), 1),
                new PlannedSite((int)KentridgeRole.ArmorShop,  StructureArchetype.Shop, new Int2(700, 660), 1),
                new PlannedSite((int)KentridgeRole.MagicShop,  StructureArchetype.Shop, new Int2(700, 820), 1),

                new PlannedSite((int)KentridgeRole.Inn,    StructureArchetype.Inn,    new Int2(700, 250), 0),
                new PlannedSite((int)KentridgeRole.Pub,    StructureArchetype.Inn,    new Int2(900, 760), 3),
                new PlannedSite((int)KentridgeRole.Church, StructureArchetype.Church, new Int2(1000, 100), 0),

                new PlannedSite((int)KentridgeRole.Warehouse,         StructureArchetype.Warehouse, new Int2(1300, 700), 2),
                new PlannedSite((int)KentridgeRole.RadcliffeMansion, StructureArchetype.Mansion,   new Int2(1300, 330), 2),
                new PlannedSite((int)KentridgeRole.Well,              StructureArchetype.Well,      TownCentreDm, 0),
            };

            return new SettlementPlan(Id, seed, TownCentreDm, Theme, sites);
        }

        /// <summary>Maximum authored envelope for an archetype, in decimetres.</summary>
        public static Int3 FootprintDm(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Townhouse: return new Int3(104, 120, 104);
                case StructureArchetype.WideHouse: return new Int3(132, 120, 132);
                case StructureArchetype.Shop:      return new Int3(124, 120, 124);
                case StructureArchetype.Inn:       return new Int3(184, 120, 184);
                case StructureArchetype.Warehouse: return new Int3(196, 104, 196);
                case StructureArchetype.Mansion:   return new Int3(268, 156, 268);
                case StructureArchetype.Church:    return new Int3(164, 180, 164);
                case StructureArchetype.Well:      return new Int3(56, 70, 56);
                default:                           return new Int3(128, 128, 128);
            }
        }

        private static PlannedSite Jittered(uint seed, uint salt, KentridgeRole role,
                                            StructureArchetype archetype,
                                            int xDm, int zDm, byte orientation)
        {
            int dx = SignedJitter(seed, salt * 2, 12);
            int dz = SignedJitter(seed, salt * 2 + 1, 12);
            return new PlannedSite((int)role, archetype,
                                   new Int2(xDm + dx, zDm + dz), orientation);
        }

        /// <summary>
        /// Independent hash draw rather than a shared RNG stream: adding a barrel generator later
        /// cannot move Rebecca's house or alter the street plan.
        /// </summary>
        private static int SignedJitter(uint seed, uint salt, int magnitude)
        {
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
