using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationContentCategory : byte
    {
        Smithy = 0,
        Tavern = 1,
        Crypt = 2,
        Market = 3,
        Stable = 4,
        Prison = 5,
        Civic = 6,
        Carpentry = 7,
        Craft = 8,
        FoodProduction = 9,
    }

    /// <summary>
    /// Stable game-content identity layered over the deliberately coarse DecorationPropFamily API.
    /// Values participate in authored/catalog identity and must never be silently reassigned.
    /// </summary>
    public enum DecorationContentKind : ushort
    {
        Unknown = 0,

        Anvil = 1,
        Bellows = 2,
        ForgeHearth = 3,
        Grindstone = 4,
        QuenchTub = 5,
        SmithToolBoard = 6,

        BarCounter = 7,
        KegRack = 8,
        MugRack = 9,
        ServingShelf = 10,
        FirewoodStack = 11,
        GameTable = 12,

        Sarcophagus = 13,
        Coffin = 14,
        OssuaryShelf = 15,
        FuneralBier = 16,
        UrnStand = 17,
        GraveMarker = 18,

        MarketStall = 19,
        HangingScale = 20,
        BasketStack = 21,
        MerchantSign = 22,
        ProduceStand = 23,
        FabricCanopy = 24,

        Manger = 25,
        HayBale = 26,
        SaddleRack = 27,
        WaterTrough = 28,
        HitchingPost = 29,
        TackHooks = 30,

        Shackles = 31,
        Stocks = 32,
        IronCage = 33,
        KeyBoard = 34,
        PrisonBucket = 35,
        RestraintBench = 36,

        NoticeBoard = 37,
        Well = 38,
        Fountain = 39,
        LampPost = 40,
        PublicTrough = 41,
        Handcart = 42,

        CarpenterBench = 43,
        SawHorse = 44,
        LumberStack = 45,
        PlankRack = 46,
        ToolChest = 47,
        ChiselBoard = 48,
        PlaneRack = 49,
        ClampRack = 50,
        WoodScrapBasket = 51,
        Lathe = 52,
        WheelwrightJig = 53,
        WheelStack = 54,
        RepairTrestle = 55,
        MeasuringBoard = 56,
        GluePotStation = 57,
        MalletShelf = 58,
        DowelBin = 59,
        ShavingPile = 60,

        Loom = 61,
        SpinningWheel = 62,
        YarnBasket = 63,
        SpindleRack = 64,
        DyeVat = 65,
        DryingLine = 66,
        FoldedClothStack = 67,
        BoltRack = 68,
        CuttingTable = 69,
        DressForm = 70,
        LeatherStretchingFrame = 71,
        HideRack = 72,
        TanningTub = 73,
        BootmakerBench = 74,
        PotteryWheel = 75,
        Kiln = 76,
        ClayBin = 77,
        DryingShelf = 78,
        AmphoraRack = 79,
        GlazeJarRack = 80,
        BasketWeavingFrame = 81,
        WickerStack = 82,
        SewingStool = 83,
        LeatherToolBoard = 84,

        PrepTable = 85,
        ButcherBlock = 86,
        HangingPotRack = 87,
        PanRack = 88,
        CauldronStand = 89,
        BreadOven = 90,
        RoastingSpit = 91,
        WashSink = 92,
        WaterBarrel = 93,
        FlourBin = 94,
        GrainSackStack = 95,
        SpiceShelf = 96,
        HerbDryingRack = 97,
        MeatHookRail = 98,
        CheeseShelf = 99,
        BreadCoolingRack = 100,
        PantryCabinet = 101,
        VegetableBasket = 102,
        FishCrate = 103,
        BreweryVat = 104,
        MashTun = 105,
        Fermenter = 106,
        WinePress = 107,
        BottleRack = 108,
        CaskStand = 109,
        PieRack = 110,
        SausageRack = 111,
        FoodPrepShelf = 112,
        KettleStand = 113,
        CellarCaskStack = 114,
    }

    /// <summary>
    /// Small authoring grammar shared by many archetypes. Hundreds of content kinds should map to a
    /// modest number of shapes rather than each requiring a dedicated geometry method.
    /// </summary>
    public enum DecorationContentShape : byte
    {
        WorkSurface = 0,
        Machine = 1,
        Hearth = 2,
        WheelMachine = 3,
        Tub = 4,
        WallRack = 5,
        Counter = 6,
        Rack = 7,
        Stack = 8,
        Coffin = 9,
        Pedestal = 10,
        Monument = 11,
        Stall = 12,
        Hanging = 13,
        Sign = 14,
        Canopy = 15,
        Trough = 16,
        Post = 17,
        Restraint = 18,
        Cage = 19,
        Well = 20,
        Fountain = 21,
        LampPost = 22,
        Cart = 23,
    }

    public struct DecorationContentRecipe
    {
        public DecorationContentCategory Category;
        public DecorationContentKind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind AcceptedSockets;
        public DecorationMountMode MountMode;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 BaseSize;
        public int3 Clearance;
        public byte WidthJitterSteps;
        public byte DepthJitterSteps;

        public bool IsWellFormed =>
            Kind != DecorationContentKind.Unknown &&
            ProxyFamily != DecorationPropFamily.Unknown &&
            AcceptedSockets != DecorationSocketKind.None &&
            math.all(BaseSize > 0) &&
            math.all(Clearance >= 0);
    }

    public static class DecorationContentVariants
    {
        // 11xxxx... marker, then ten stable kind bits, then twenty deterministic variation bits.
        // This leaves room for 1,023 stable archetype kinds before an encoding version change.
        private const uint Marker = 0xC0000000u;
        private const uint MarkerMask = 0xC0000000u;
        private const uint KindMask = 0x3FF00000u;
        private const uint VariationMask = 0x000FFFFFu;
        private const int KindShift = 20;

        public static uint Encode(DecorationContentKind kind, uint variation)
        {
            uint kindValue = (uint)kind;
            if (kindValue == 0 || kindValue > 1023u)
                return 0u;
            return Marker | (kindValue << KindShift) | (variation & VariationMask);
        }

        public static bool IsContent(uint variant) =>
            (variant & MarkerMask) == Marker && KindOf(variant) != DecorationContentKind.Unknown;

        public static DecorationContentKind KindOf(uint variant)
        {
            if ((variant & MarkerMask) != Marker)
                return DecorationContentKind.Unknown;
            uint raw = (variant & KindMask) >> KindShift;
            return raw == 0 || raw > (uint)DecorationContentKind.CellarCaskStack
                ? DecorationContentKind.Unknown
                : (DecorationContentKind)raw;
        }

        public static uint VariationOf(uint variant) => variant & VariationMask;
    }

    public static class DecorationContentCatalog
    {
        public const int KindCount = 114;

        public static bool IsDefined(DecorationContentKind kind) =>
            kind >= DecorationContentKind.Anvil && kind <= DecorationContentKind.CellarCaskStack;

        public static DecorationContentRecipe Recipe(DecorationContentKind kind)
        {
            switch (kind)
            {
                // Smithy
                case DecorationContentKind.Anvil:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.Pedestal,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(12, 10, 7), new int3(3, 0, 3), 1, 1);
                case DecorationContentKind.Bellows:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.Machine,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(14, 8, 11), new int3(3, 0, 3), 1, 1);
                case DecorationContentKind.ForgeHearth:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.Hearth,
                        DecorationPropFamily.Fireplace, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.VoxelStamp,
                        Blocking() | DecorationInteractionFlags.EmitsLight | DecorationInteractionFlags.EmitsParticles,
                        new int3(24, 20, 12), new int3(5, 0, 8), 2, 1);
                case DecorationContentKind.Grindstone:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.WheelMachine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(12, 12, 8), new int3(3, 0, 3), 1, 0);
                case DecorationContentKind.QuenchTub:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(11, 8, 11), new int3(2, 0, 2), 1, 1);
                case DecorationContentKind.SmithToolBoard:
                    return R(DecorationContentCategory.Smithy, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(20, 16, 2), new int3(3, 2, 1), 2, 0);

                // Tavern
                case DecorationContentKind.BarCounter:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.Counter,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(34, 11, 10), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.KegRack:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 18, 9), new int3(3, 0, 4), 2, 1);
                case DecorationContentKind.MugRack:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(18, 12, 2), new int3(2, 2, 1), 2, 0);
                case DecorationContentKind.ServingShelf:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(24, 10, 3), new int3(2, 2, 1), 2, 0);
                case DecorationContentKind.FirewoodStack:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(16, 8, 10), new int3(2, 0, 2), 2, 1);
                case DecorationContentKind.GameTable:
                    return R(DecorationContentCategory.Tavern, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking() | DecorationInteractionFlags.Movable,
                        new int3(16, 9, 16), new int3(4, 0, 4), 1, 1);

                // Crypt
                case DecorationContentKind.Sarcophagus:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.Coffin,
                        DecorationPropFamily.Chest, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, ContainerBlocking(), new int3(16, 12, 30), new int3(4, 0, 5), 2, 2);
                case DecorationContentKind.Coffin:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.Coffin,
                        DecorationPropFamily.Chest, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, ContainerBlocking(), new int3(13, 8, 28), new int3(3, 0, 4), 1, 2);
                case DecorationContentKind.OssuaryShelf:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 22, 7), new int3(3, 0, 4), 2, 0);
                case DecorationContentKind.FuneralBier:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Bed, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(15, 8, 28), new int3(4, 0, 5), 1, 2);
                case DecorationContentKind.UrnStand:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.Pedestal,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(8, 14, 8), new int3(2, 0, 2), 1, 1);
                case DecorationContentKind.GraveMarker:
                    return R(DecorationContentCategory.Crypt, kind, DecorationContentShape.Monument,
                        DecorationPropFamily.Altar, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(12, 20, 5), new int3(3, 0, 3), 2, 0);

                // Market
                case DecorationContentKind.MarketStall:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Stall,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(30, 22, 18), new int3(5, 0, 5), 3, 2);
                case DecorationContentKind.HangingScale:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Hanging,
                        DecorationPropFamily.Lantern, DecorationSocketKind.Ceiling, DecorationMountMode.Ceiling,
                        DecorationRenderBackend.ProceduralMesh, DecorationInteractionFlags.Destructible,
                        new int3(8, 12, 8), new int3(2, 2, 2), 1, 1);
                case DecorationContentKind.BasketStack:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(12, 10, 12), new int3(2, 0, 2), 2, 2);
                case DecorationContentKind.MerchantSign:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Sign,
                        DecorationPropFamily.Painting, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.ThinSurface, DecorationInteractionFlags.Destructible,
                        new int3(16, 12, 1), new int3(2, 2, 0), 3, 0);
                case DecorationContentKind.ProduceStand:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Stall,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 14, 15), new int3(4, 0, 4), 3, 2);
                case DecorationContentKind.FabricCanopy:
                    return R(DecorationContentCategory.Market, kind, DecorationContentShape.Canopy,
                        DecorationPropFamily.Banner, DecorationSocketKind.Ceiling, DecorationMountMode.Ceiling,
                        DecorationRenderBackend.ThinSurface, DecorationInteractionFlags.Destructible,
                        new int3(30, 1, 20), int3.zero, 3, 2);

                // Stable
                case DecorationContentKind.Manger:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.Trough,
                        DecorationPropFamily.Table, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 9, 9), new int3(3, 0, 4), 2, 1);
                case DecorationContentKind.HayBale:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(14, 9, 10), new int3(1, 0, 1), 2, 2);
                case DecorationContentKind.SaddleRack:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(18, 12, 5), new int3(2, 2, 2), 2, 0);
                case DecorationContentKind.WaterTrough:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.Trough,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(26, 8, 10), new int3(3, 0, 3), 3, 1);
                case DecorationContentKind.HitchingPost:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.Post,
                        DecorationPropFamily.Bench, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(18, 12, 5), new int3(3, 0, 3), 2, 0);
                case DecorationContentKind.TackHooks:
                    return R(DecorationContentCategory.Stable, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(20, 10, 3), new int3(2, 2, 1), 2, 0);

                // Prison
                case DecorationContentKind.Shackles:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.Hanging,
                        DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.ProceduralMesh, DecorationInteractionFlags.Destructible,
                        new int3(8, 12, 3), new int3(2, 2, 1), 1, 0);
                case DecorationContentKind.Stocks:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.Restraint,
                        DecorationPropFamily.Bench, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(20, 13, 8), new int3(4, 0, 4), 2, 1);
                case DecorationContentKind.IronCage:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.Cage,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(18, 24, 18), new int3(5, 0, 5), 2, 2);
                case DecorationContentKind.KeyBoard:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.WallRack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(14, 12, 2), new int3(2, 2, 1), 2, 0);
                case DecorationContentKind.PrisonBucket:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(7, 7, 7), new int3(1, 0, 1), 1, 1);
                case DecorationContentKind.RestraintBench:
                    return R(DecorationContentCategory.Prison, kind, DecorationContentShape.Restraint,
                        DecorationPropFamily.Bench, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 10, 9), new int3(4, 0, 4), 2, 1);

                // Civic
                case DecorationContentKind.NoticeBoard:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.Sign,
                        DecorationPropFamily.Painting, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.ThinSurface, DecorationInteractionFlags.Destructible,
                        new int3(22, 16, 1), new int3(2, 2, 0), 3, 0);
                case DecorationContentKind.Well:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.Well,
                        DecorationPropFamily.Fireplace, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.VoxelStamp, Blocking(), new int3(20, 15, 20), new int3(5, 0, 5), 2, 2);
                case DecorationContentKind.Fountain:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.Fountain,
                        DecorationPropFamily.Fireplace, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.VoxelStamp, Blocking(), new int3(24, 18, 24), new int3(6, 0, 6), 3, 3);
                case DecorationContentKind.LampPost:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.LampPost,
                        DecorationPropFamily.Lantern, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.EmitsLight,
                        new int3(5, 24, 5), new int3(3, 0, 3), 1, 1);
                case DecorationContentKind.PublicTrough:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.Trough,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(28, 8, 10), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.Handcart:
                    return R(DecorationContentCategory.Civic, kind, DecorationContentShape.Cart,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        Blocking() | DecorationInteractionFlags.Container | DecorationInteractionFlags.Movable,
                        new int3(18, 12, 28), new int3(4, 0, 5), 2, 3);
                default:
                    return DecorationContentExpansionRegistry.Recipe(kind);
            }
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            DecorationContentKind kind)
        {
            DecorationContentRecipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed)
                return default;

            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int3 size = recipe.BaseSize;
            if (recipe.WidthJitterSteps > 0)
                size.x += (int)(seed % (uint)(recipe.WidthJitterSteps + 1)) * 2;
            if (recipe.DepthJitterSteps > 0)
                size.z += (int)(DecorationSeed.Derive(seed, 0xC071E17u) % (uint)(recipe.DepthJitterSteps + 1)) * 2;

            uint variation = DecorationSeed.Derive(seed,
                context.StyleId ^ ((uint)context.Wealth << 12) ^ ((uint)context.Condition << 8) ^ (uint)kind);

            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily,
                AcceptedSockets = recipe.AcceptedSockets,
                MountMode = recipe.MountMode,
                Backend = recipe.Backend,
                Interaction = recipe.Interaction,
                Size = size,
                Clearance = recipe.Clearance,
                Variant = DecorationContentVariants.Encode(kind, variation),
            };
        }

        public static DecorationContentCategory CategoryOf(DecorationContentKind kind) => Recipe(kind).Category;
        public static DecorationContentShape ShapeOf(DecorationContentKind kind) => Recipe(kind).Shape;
        public static DecorationPropFamily ProxyFamilyOf(DecorationContentKind kind) => Recipe(kind).ProxyFamily;

        private static DecorationInteractionFlags Blocking() =>
            DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible;

        private static DecorationInteractionFlags ContainerBlocking() =>
            Blocking() | DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;

        private static DecorationContentRecipe R(
            DecorationContentCategory category,
            DecorationContentKind kind,
            DecorationContentShape shape,
            DecorationPropFamily proxyFamily,
            DecorationSocketKind sockets,
            DecorationMountMode mount,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            int3 clearance,
            byte widthJitter,
            byte depthJitter) => new DecorationContentRecipe
        {
            Category = category,
            Kind = kind,
            Shape = shape,
            ProxyFamily = proxyFamily,
            AcceptedSockets = sockets,
            MountMode = mount,
            Backend = backend,
            Interaction = interaction,
            BaseSize = size,
            Clearance = clearance,
            WidthJitterSteps = widthJitter,
            DepthJitterSteps = depthJitter,
        };
    }
}
