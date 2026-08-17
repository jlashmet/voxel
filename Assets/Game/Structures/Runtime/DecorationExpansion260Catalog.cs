using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion260Category : byte { Commerce, Military, Household }

    public enum DecorationExpansion260Kind : ushort
    {
        JewelerBench = 201, GemDisplayCase = 202, CoinScale = 203, Lockbox = 204,
        ClothMerchantTable = 205, ShoeDisplayRack = 206, WeaponMerchantRack = 207, ArmorMerchantStand = 208,
        BookMerchantShelf = 209, ScrollDisplay = 210, ApothecaryCounter = 211, HerbDrawerCabinet = 212,
        PotionDisplayCase = 213, GeneralStoreCounter = 214, SackDisplay = 215, ProduceBasketStand = 216,
        ButcherDisplay = 217, FishmongerSlab = 218, ShopSignHanging = 219, AwningStriped = 220,

        TrainingDummy = 221, ArcheryTarget = 222, WeaponStand = 223, ShieldRack = 224,
        ArmorStand = 225, SpearRack = 226, SwordRack = 227, BowRack = 228,
        ArrowBarrel = 229, PracticeRingMarker = 230, SandTable = 231, WarMapBoard = 232,
        CommandDesk = 233, DrumStand = 234, SignalHornRack = 235, Barricade = 236,
        SpikeBarrier = 237, GuardBell = 238, WatchBrazier = 239, SiegeToolRack = 240,

        Wardrobe = 241, VanityTable = 242, WashBasinStand = 243, ChamberPot = 244,
        FoldingScreen = 245, WritingDesk = 246, SideTable = 247, Footstool = 248,
        Settee = 249, Chaise = 250, GrandMirror = 251, Candelabra = 252,
        MusicStand = 253, LuteRack = 254, Harp = 255, Harpsichord = 256,
        TrophyCase = 257, WineCabinet = 258, JewelryCasket = 259, PerfumeTray = 260,
    }

    public struct DecorationExpansion260Recipe
    {
        public DecorationExpansion260Category Category;
        public DecorationExpansion260Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;
        public bool IsWellFormed => (ushort)Kind >= 201 && (ushort)Kind <= 260 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion260Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion260Kind kind, uint variation) =>
            Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion260(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 201 && id <= 260;
        }
        public static DecorationExpansion260Kind KindOf(uint variant) =>
            IsExpansion260(variant) ? (DecorationExpansion260Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion260Catalog
    {
        public const int Count = 60;

        public static DecorationExpansion260Recipe Recipe(DecorationExpansion260Kind kind)
        {
            int id = (int)kind;
            if (id < 201 || id > 260) return default;
            if (id <= 220) return Commerce(kind, id - 201);
            if (id <= 240) return Military(kind, id - 221);
            return Household(kind, id - 241);
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion260Kind kind)
        {
            DecorationExpansion260Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || !recipe.IsWellFormed || sceneId == 0 || slotId == 0) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int3 size = recipe.Size;
            size.x += (int)(seed & 1u) * 2;
            size.z += (int)((seed >> 2) & 1u) * 2;
            uint variation = DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind ^ ((uint)context.Wealth << 8));
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily, AcceptedSockets = recipe.Sockets, MountMode = recipe.Mount,
                Backend = recipe.Backend, Interaction = recipe.Interaction, Size = size,
                Clearance = recipe.Clearance, Variant = DecorationExpansion260Variants.Encode(kind, variation),
            };
        }

        private static DecorationExpansion260Recipe Commerce(DecorationExpansion260Kind kind, int i)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.WorkSurface, DecorationContentShape.Counter, DecorationContentShape.Machine,
                DecorationContentShape.Coffin, DecorationContentShape.WorkSurface, DecorationContentShape.Rack,
                DecorationContentShape.WallRack, DecorationContentShape.Pedestal, DecorationContentShape.Rack,
                DecorationContentShape.Rack, DecorationContentShape.Counter, DecorationContentShape.Rack,
                DecorationContentShape.Counter, DecorationContentShape.Counter, DecorationContentShape.Stack,
                DecorationContentShape.Stall, DecorationContentShape.Stall, DecorationContentShape.WorkSurface,
                DecorationContentShape.Hanging, DecorationContentShape.Canopy,
            };
            bool wall = i == 5 || i == 6 || i == 8 || i == 9 || i == 11 || i == 18;
            bool thin = i == 18 || i == 19;
            bool mesh = i == 2;
            bool container = i == 3 || i == 11 || i == 12 || i == 13;
            return R(DecorationExpansion260Category.Commerce, kind, shapes[i],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Table,
                wall ? DecorationSocketKind.Wall : i == 19 ? DecorationSocketKind.Ceiling : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.Wall : i == 19 ? DecorationMountMode.Ceiling : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly,
                F(blocking: !wall && !thin && !mesh, movable: i == 3, container: container),
                new int3(10 + (i % 4) * 4, 8 + (i % 5) * 2, 8 + (i % 3) * 4),
                wall ? new int3(2, 2, 1) : new int3(3, 0, 3));
        }

        private static DecorationExpansion260Recipe Military(DecorationExpansion260Kind kind, int i)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Post, DecorationContentShape.Monument, DecorationContentShape.Pedestal,
                DecorationContentShape.WallRack, DecorationContentShape.Pedestal, DecorationContentShape.WallRack,
                DecorationContentShape.WallRack, DecorationContentShape.WallRack, DecorationContentShape.Tub,
                DecorationContentShape.Sign, DecorationContentShape.WorkSurface, DecorationContentShape.Sign,
                DecorationContentShape.WorkSurface, DecorationContentShape.Pedestal, DecorationContentShape.WallRack,
                DecorationContentShape.Post, DecorationContentShape.Post, DecorationContentShape.Hanging,
                DecorationContentShape.Hearth, DecorationContentShape.Rack,
            };
            bool wall = i == 3 || i == 5 || i == 6 || i == 7 || i == 11 || i == 14 || i == 17 || i == 19;
            bool thin = i == 9 || i == 11;
            bool mesh = i == 17;
            bool light = i == 18;
            return R(DecorationExpansion260Category.Military, kind, shapes[i],
                wall ? DecorationPropFamily.WeaponRack : DecorationPropFamily.Table,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh :
                    (i == 18 ? DecorationRenderBackend.VoxelStamp : DecorationRenderBackend.BoxAssembly),
                F(blocking: !wall && !thin && !mesh, movable: i == 8 || i == 13, light: light, particles: light),
                new int3(9 + (i % 5) * 4, 10 + (i % 4) * 4, 7 + (i % 3) * 4),
                wall ? new int3(2, 2, 1) : new int3(4, 0, 4));
        }

        private static DecorationExpansion260Recipe Household(DecorationExpansion260Kind kind, int i)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Rack, DecorationContentShape.WorkSurface, DecorationContentShape.Pedestal,
                DecorationContentShape.Tub, DecorationContentShape.Sign, DecorationContentShape.WorkSurface,
                DecorationContentShape.WorkSurface, DecorationContentShape.Pedestal, DecorationContentShape.WorkSurface,
                DecorationContentShape.WorkSurface, DecorationContentShape.Sign, DecorationContentShape.LampPost,
                DecorationContentShape.Pedestal, DecorationContentShape.WallRack, DecorationContentShape.Machine,
                DecorationContentShape.Machine, DecorationContentShape.Rack, DecorationContentShape.Rack,
                DecorationContentShape.Coffin, DecorationContentShape.Pedestal,
            };
            bool wall = i == 0 || i == 4 || i == 10 || i == 13 || i == 16 || i == 17;
            bool thin = i == 4 || i == 10;
            bool mesh = i == 14;
            bool container = i == 0 || i == 16 || i == 17 || i == 18;
            bool light = i == 11;
            return R(DecorationExpansion260Category.Household, kind, shapes[i],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Table,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.FloorAgainstWall : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly,
                F(blocking: !thin && !mesh && (i <= 2 || i == 5 || i == 8 || i == 9 || i == 15 || container),
                    movable: i == 3 || i == 6 || i == 7 || i == 11 || i == 12 || i == 18 || i == 19,
                    container: container, light: light),
                new int3(8 + (i % 5) * 4, 7 + (i % 6) * 3, 7 + (i % 4) * 3),
                wall ? new int3(2, 1, 2) : new int3(2, 0, 2));
        }

        private static DecorationInteractionFlags F(
            bool blocking = false, bool movable = false, bool container = false, bool light = false, bool particles = false)
        {
            DecorationInteractionFlags f = DecorationInteractionFlags.Destructible;
            if (blocking) f |= DecorationInteractionFlags.BlocksNavigation;
            if (movable) f |= DecorationInteractionFlags.Movable;
            if (container) f |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) f |= DecorationInteractionFlags.EmitsLight;
            if (particles) f |= DecorationInteractionFlags.EmitsParticles;
            return f;
        }

        private static DecorationExpansion260Recipe R(
            DecorationExpansion260Category category, DecorationExpansion260Kind kind, DecorationContentShape shape,
            DecorationPropFamily proxy, DecorationSocketKind sockets, DecorationMountMode mount,
            DecorationRenderBackend backend, DecorationInteractionFlags interaction, int3 size, int3 clearance) =>
            new DecorationExpansion260Recipe
            {
                Category = category, Kind = kind, Shape = shape, ProxyFamily = proxy, Sockets = sockets,
                Mount = mount, Backend = backend, Interaction = interaction, Size = size, Clearance = clearance,
            };
    }
}
