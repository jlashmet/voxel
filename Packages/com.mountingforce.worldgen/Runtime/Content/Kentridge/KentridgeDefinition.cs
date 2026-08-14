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
    /// Authored identity and high-level structural constraints for Kentridge. Spatial planning lives
    /// in <see cref="KentridgeTownPlanner"/>; local architectural detail is compiled by the lower
    /// MountingForce.WorldGen.Architecture assembly.
    /// </summary>
    public static class KentridgeDefinition
    {
        public const string Id = "kentridge";
        public const int AnonymousFabricEnvelopeDm = 72;
        public static readonly Int2 TownCentreDm = new Int2(1170, 520);

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

        public static SettlementPlan Build(uint seed) => KentridgeTownPlanner.Build(seed);

        /// <summary>
        /// Complete handoff from Kentridge's settlement layer to the lower architecture layer.
        /// No roof/window/facade decisions cross this boundary.
        /// </summary>
        public static StructureIntent StructureIntent(BuildingPlot plot)
        {
            return new StructureIntent(plot, Id, FootprintDm(plot.Archetype));
        }

        /// <summary>
        /// Anonymous frontage handoff. The band is carried only as a deterministic variation context;
        /// it does not prescribe any roof, window, facade, awning, annex, or chimney choice.
        /// </summary>
        public static UrbanFabricIntent UrbanFabricIntent(KentridgeFrontageRun run)
        {
            return new UrbanFabricIntent(
                Id,
                run.District,
                run.MinStoreys,
                run.MaxStoreys,
                AnonymousFabricEnvelopeDm,
                (int)run.Band);
        }

        /// <summary>Maximum authored structure envelope for an archetype, in decimetres.</summary>
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
    }
}
