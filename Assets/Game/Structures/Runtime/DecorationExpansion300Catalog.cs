using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion300Category : byte
    {
        MonsterLair = 0,
        AdventurerGuild = 1,
    }

    public enum DecorationExpansion300Kind : ushort
    {
        MonsterNest = 261, EggClutch = 262, CocoonBundle = 263, GiantWebSheet = 264,
        WebbedVictim = 265, BoneTotem = 266, TrophySkullPile = 267, GnawedBonePile = 268,
        ClawMarkedPost = 269, ScentMarkerTotem = 270, SlimePool = 271, SlimeTrailPatch = 272,
        AcidPool = 273, MoltedShellPile = 274, ShedScalePile = 275, BeastBedding = 276,
        BurrowMound = 277, HoardScrapPile = 278, MonsterFoodCache = 279, ChainedPreyCage = 280,

        QuestBoard = 281, BountyBoard = 282, GuildRegistryDesk = 283, AdventurerMapTable = 284,
        ExpeditionSupplyRack = 285, PotionSatchelRack = 286, BedrollRack = 287, RopeGearRack = 288,
        LanternGearRack = 289, GuildTrophyWall = 290, MonsterContractBoard = 291, PartyNoticeBoard = 292,
        GuildStrongbox = 293, MemberLockerBank = 294, TrainingManualShelf = 295, CartographersDesk = 296,
        CaravanSupplyCrate = 297, PackSaddleStand = 298, TravelCharmDisplay = 299, WaystoneAttunementPedestal = 300,
    }

    public struct DecorationExpansion300Recipe
    {
        public DecorationExpansion300Category Category;
        public DecorationExpansion300Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;

        public bool IsWellFormed =>
            (ushort)Kind >= 261 && (ushort)Kind <= 300 &&
            ProxyFamily != DecorationPropFamily.Unknown &&
            Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion300Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion300Kind kind, uint variation) =>
            Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);

        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);

        public static bool IsExpansion300(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 261 && id <= 300;
        }

        public static DecorationExpansion300Kind KindOf(uint variant) =>
            IsExpansion300(variant) ? (DecorationExpansion300Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion300Catalog
    {
        public const int FirstId = 261;
        public const int LastId = 300;
        public const int Count = 40;

        public static DecorationExpansion300Recipe Recipe(DecorationExpansion300Kind kind)
        {
            int id = (int)kind;
            if (id < FirstId || id > LastId)
                return default;
            return id <= 280 ? Monster(kind, id - 261) : Guild(kind, id - 281);
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            DecorationExpansion300Kind kind)
        {
            DecorationExpansion300Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed)
                return default;

            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int3 size = recipe.Size;
            size.x += (int)(seed & 1u) * 2;
            size.z += (int)((seed >> 2) & 1u) * 2;
            uint variation = DecorationSeed.Derive(seed,
                context.StyleId ^ (uint)kind ^ ((uint)context.Condition << 9) ^ ((uint)context.Wealth << 5));

            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily,
                AcceptedSockets = recipe.Sockets,
                MountMode = recipe.Mount,
                Backend = recipe.Backend,
                Interaction = recipe.Interaction,
                Size = size,
                Clearance = recipe.Clearance,
                Variant = DecorationExpansion300Variants.Encode(kind, variation),
            };
        }

        private static DecorationExpansion300Recipe Monster(DecorationExpansion300Kind kind, int i)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Stack, DecorationContentShape.Stack, DecorationContentShape.Hanging,
                DecorationContentShape.Canopy, DecorationContentShape.Hanging, DecorationContentShape.Post,
                DecorationContentShape.Stack, DecorationContentShape.Stack, DecorationContentShape.Post,
                DecorationContentShape.Post, DecorationContentShape.Tub, DecorationContentShape.Sign,
                DecorationContentShape.Tub, DecorationContentShape.Stack, DecorationContentShape.Stack,
                DecorationContentShape.Stack, DecorationContentShape.Monument, DecorationContentShape.Stack,
                DecorationContentShape.Coffin, DecorationContentShape.Cage,
            };

            bool wall = i == 2 || i == 3 || i == 4 || i == 8 || i == 11;
            bool thin = i == 3 || i == 8 || i == 10 || i == 11 || i == 12;
            bool mesh = i == 1 || i == 2 || i == 4 || i == 13 || i == 14 || i == 15;
            bool container = i == 17 || i == 18 || i == 19;
            DecorationSocketKind socket = wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor;
            DecorationMountMode mount = wall ? DecorationMountMode.Wall : DecorationMountMode.Floor;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface :
                mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;

            return R(DecorationExpansion300Category.MonsterLair, kind, shapes[i],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Banner : DecorationPropFamily.Crate,
                socket, mount, backend,
                Flags(blocking: i == 0 || i == 5 || i == 9 || i == 16 || i == 19,
                    movable: i == 1 || i == 6 || i == 7 || i == 13 || i == 14 || i == 17 || i == 18,
                    container: container,
                    lootable: i == 6 || i == 17 || i == 18 || i == 19),
                MonsterSize(i), wall ? new int3(2, 2, 1) : new int3(2, 0, 2));
        }

        private static DecorationExpansion300Recipe Guild(DecorationExpansion300Kind kind, int i)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Sign, DecorationContentShape.Sign, DecorationContentShape.WorkSurface,
                DecorationContentShape.WorkSurface, DecorationContentShape.Rack, DecorationContentShape.WallRack,
                DecorationContentShape.WallRack, DecorationContentShape.WallRack, DecorationContentShape.WallRack,
                DecorationContentShape.WallRack, DecorationContentShape.Sign, DecorationContentShape.Sign,
                DecorationContentShape.Coffin, DecorationContentShape.Rack, DecorationContentShape.Rack,
                DecorationContentShape.WorkSurface, DecorationContentShape.Coffin, DecorationContentShape.Pedestal,
                DecorationContentShape.WallRack, DecorationContentShape.Pedestal,
            };

            bool wall = i == 0 || i == 1 || (i >= 4 && i <= 11) || i == 13 || i == 14 || i == 18;
            bool thin = i == 0 || i == 1 || i == 9 || i == 10 || i == 11 || i == 18;
            bool mesh = i == 17 || i == 19;
            bool container = i == 12 || i == 16;
            bool light = i == 8 || i == 19;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface :
                mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;

            return R(DecorationExpansion300Category.AdventurerGuild, kind, shapes[i],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Table,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                backend,
                Flags(blocking: !wall && !mesh && (i == 2 || i == 3 || i == 12 || i == 15 || i == 16 || i == 19),
                    movable: i == 12 || i == 16 || i == 17,
                    container: container,
                    lootable: container,
                    light: light),
                GuildSize(i), wall ? new int3(2, 2, 1) : new int3(3, 0, 3));
        }

        private static int3 MonsterSize(int i)
        {
            if (i == 0) return new int3(28, 10, 24);
            if (i == 3) return new int3(32, 22, 1);
            if (i == 10 || i == 12) return new int3(22, 1, 18);
            if (i == 19) return new int3(18, 24, 18);
            return new int3(8 + (i % 5) * 4, 6 + (i % 4) * 4, 7 + (i % 3) * 5);
        }

        private static int3 GuildSize(int i)
        {
            if (i == 0 || i == 1 || i == 10 || i == 11) return new int3(24, 18, 1);
            if (i == 3) return new int3(28, 10, 24);
            if (i == 12 || i == 16) return new int3(18, 12, 14);
            if (i == 19) return new int3(14, 22, 14);
            return new int3(10 + (i % 5) * 4, 8 + (i % 5) * 3, 8 + (i % 4) * 3);
        }

        private static DecorationInteractionFlags Flags(
            bool blocking = false,
            bool movable = false,
            bool container = false,
            bool lootable = false,
            bool light = false)
        {
            DecorationInteractionFlags f = DecorationInteractionFlags.Destructible;
            if (blocking) f |= DecorationInteractionFlags.BlocksNavigation;
            if (movable) f |= DecorationInteractionFlags.Movable;
            if (container) f |= DecorationInteractionFlags.Container;
            if (lootable) f |= DecorationInteractionFlags.Lootable;
            if (light) f |= DecorationInteractionFlags.EmitsLight;
            return f;
        }

        private static DecorationExpansion300Recipe R(
            DecorationExpansion300Category category,
            DecorationExpansion300Kind kind,
            DecorationContentShape shape,
            DecorationPropFamily proxy,
            DecorationSocketKind sockets,
            DecorationMountMode mount,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            int3 clearance) => new DecorationExpansion300Recipe
        {
            Category = category,
            Kind = kind,
            Shape = shape,
            ProxyFamily = proxy,
            Sockets = sockets,
            Mount = mount,
            Backend = backend,
            Interaction = interaction,
            Size = size,
            Clearance = clearance,
        };
    }
}
