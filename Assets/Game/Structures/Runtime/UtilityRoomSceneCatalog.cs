using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum UtilityRoomSceneKind : byte
    {
        GuardPost = 0,
        Kitchen = 1,
        LibraryStudy = 2,
        ChapelShrine = 3,
        Barracks = 4,
        ThroneRoom = 5,
        Cellar = 6,
        Storage = 7,
    }

    public static class UtilityRoomSceneCatalog
    {
        public static uint SceneId(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost: return 0x47524431u; // GRD1
                case UtilityRoomSceneKind.Kitchen: return 0x4B495431u; // KIT1
                case UtilityRoomSceneKind.LibraryStudy: return 0x4C494231u; // LIB1
                case UtilityRoomSceneKind.ChapelShrine: return 0x43484131u; // CHA1
                case UtilityRoomSceneKind.Barracks: return 0x42415231u; // BAR1
                case UtilityRoomSceneKind.ThroneRoom: return 0x54485231u; // THR1
                case UtilityRoomSceneKind.Cellar: return 0x43454C31u; // CEL1
                default: return 0x53544F31u; // STO1
            }
        }

        public static DecorationSceneSlot[] CreateSlots(UtilityRoomSceneKind kind)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, true),
                        Slot(2, DecorationPropFamily.Bench, DecorationSocketKind.Floor, true),
                        Slot(3, DecorationPropFamily.WeaponRack, DecorationSocketKind.Floor, false),
                        Slot(4, DecorationPropFamily.WallTorch, DecorationSocketKind.Wall, false),
                        Slot(5, DecorationPropFamily.Crate, DecorationSocketKind.Floor, false),
                    };
                case UtilityRoomSceneKind.Kitchen:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Table, DecorationSocketKind.Floor, true),
                        Slot(2, DecorationPropFamily.Shelf, DecorationSocketKind.Wall, true),
                        Slot(3, DecorationPropFamily.Barrel, DecorationSocketKind.Floor, false),
                        Slot(4, DecorationPropFamily.Crate, DecorationSocketKind.Floor, false),
                        Slot(5, DecorationPropFamily.Candle, DecorationSocketKind.Floor, false),
                    };
                case UtilityRoomSceneKind.LibraryStudy:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Bookcase, DecorationSocketKind.Wall, true),
                        Slot(2, DecorationPropFamily.Bookcase, DecorationSocketKind.Wall, true),
                        Slot(3, DecorationPropFamily.Table, DecorationSocketKind.Floor, true),
                        Slot(4, DecorationPropFamily.Chair, DecorationSocketKind.Floor, false),
                        Slot(5, DecorationPropFamily.Lantern, DecorationSocketKind.Floor, false),
                        Slot(6, DecorationPropFamily.Painting, DecorationSocketKind.Wall, false),
                    };
                case UtilityRoomSceneKind.ChapelShrine:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Altar, DecorationSocketKind.Wall, true),
                        Slot(2, DecorationPropFamily.Candle, DecorationSocketKind.Floor, false),
                        Slot(3, DecorationPropFamily.Candle, DecorationSocketKind.Floor, false),
                        Slot(4, DecorationPropFamily.Banner, DecorationSocketKind.Wall, false),
                        Slot(5, DecorationPropFamily.WallTorch, DecorationSocketKind.Wall, false),
                    };
                case UtilityRoomSceneKind.Barracks:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Bed, DecorationSocketKind.Wall, true),
                        Slot(2, DecorationPropFamily.Bed, DecorationSocketKind.Wall, true),
                        Slot(3, DecorationPropFamily.Chest, DecorationSocketKind.Wall, false),
                        Slot(4, DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, false),
                        Slot(5, DecorationPropFamily.Bench, DecorationSocketKind.Floor, false),
                    };
                case UtilityRoomSceneKind.ThroneRoom:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Chair, DecorationSocketKind.Floor, true),
                        Slot(2, DecorationPropFamily.Banner, DecorationSocketKind.Wall, true),
                        Slot(3, DecorationPropFamily.Banner, DecorationSocketKind.Wall, false),
                        Slot(4, DecorationPropFamily.Chandelier, DecorationSocketKind.Ceiling, false),
                        Slot(5, DecorationPropFamily.WeaponRack, DecorationSocketKind.Floor, false),
                    };
                case UtilityRoomSceneKind.Cellar:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Barrel, DecorationSocketKind.Floor, true),
                        Slot(2, DecorationPropFamily.Barrel, DecorationSocketKind.Floor, true),
                        Slot(3, DecorationPropFamily.Crate, DecorationSocketKind.Floor, true),
                        Slot(4, DecorationPropFamily.Shelf, DecorationSocketKind.Wall, false),
                        Slot(5, DecorationPropFamily.Candle, DecorationSocketKind.Floor, false),
                    };
                default:
                    return new[]
                    {
                        Slot(1, DecorationPropFamily.Chest, DecorationSocketKind.Wall, true),
                        Slot(2, DecorationPropFamily.Crate, DecorationSocketKind.Floor, true),
                        Slot(3, DecorationPropFamily.Barrel, DecorationSocketKind.Floor, false),
                        Slot(4, DecorationPropFamily.Shelf, DecorationSocketKind.Wall, false),
                        Slot(5, DecorationPropFamily.Bookcase, DecorationSocketKind.Wall, false),
                    };
            }
        }

        public static int OptionalBudget(
            UtilityRoomSceneKind kind,
            in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined)
                return 0;
            int budget = context.Condition == DecorationConditionTier.Abandoned
                ? 1
                : 1 + (int)context.Wealth / 2;
            if (kind == UtilityRoomSceneKind.LibraryStudy || kind == UtilityRoomSceneKind.ThroneRoom)
                budget++;
            return budget;
        }

        public static bool IsCompatible(
            UtilityRoomSceneKind kind,
            in DecorationSpace space,
            in DecorationContext context)
        {
            if (!space.IsWellFormed || !context.IsWellFormed || space.Kind != context.SpaceKind)
                return false;
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost:
                    return space.Kind == DecorationSpaceKind.GuardPost;
                case UtilityRoomSceneKind.Kitchen:
                case UtilityRoomSceneKind.Cellar:
                case UtilityRoomSceneKind.Storage:
                    return space.Kind == DecorationSpaceKind.Storage;
                case UtilityRoomSceneKind.LibraryStudy:
                    return space.Kind == DecorationSpaceKind.Study;
                case UtilityRoomSceneKind.ChapelShrine:
                    return space.Kind == DecorationSpaceKind.Chapel || space.Kind == DecorationSpaceKind.Shrine;
                case UtilityRoomSceneKind.Barracks:
                    return space.Kind == DecorationSpaceKind.Bedroom;
                case UtilityRoomSceneKind.ThroneRoom:
                    return space.Kind == DecorationSpaceKind.DiningRoom;
                default:
                    return false;
            }
        }

        private static DecorationSceneSlot Slot(
            uint id,
            DecorationPropFamily family,
            DecorationSocketKind socket,
            bool required) => new DecorationSceneSlot
            {
                SlotId = id,
                Family = family,
                RequestedSocket = socket,
                Weight = 1,
                Required = required,
            };
    }
}
