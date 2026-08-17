using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpandedContentCategory : byte
    {
        Arcane = 0,
        Funerary = 1,
        Settlement = 2,
    }

    /// <summary>Stable content IDs 115-200. Numeric values are persistence/catalog identity.</summary>
    public enum DecorationExpandedContentKind : ushort
    {
        AlchemyTable = 115, AlembicStand = 116, RetortRack = 117, MortarStation = 118,
        IngredientCabinet = 119, HerbariumShelf = 120, CrystalStand = 121, RuneTable = 122,
        ScryingBasin = 123, AstrolabeStand = 124, TelescopeTripod = 125, Orrery = 126,
        SpellbookLectern = 127, ScrollRack = 128, WandRack = 129, StaffStand = 130,
        PotionShelf = 131, ReagentChest = 132, DistillationFurnace = 133, ArcaneBrazier = 134,
        SummoningCircle = 135, RitualPedestal = 136, CandleCluster = 137, SkullReliquary = 138,
        SpecimenJarRack = 139, SpecimenCage = 140, EnchantingAnvil = 141, ManaCrystalCluster = 142,
        ChalkRuneBoard = 143, StarChart = 144,

        TombSlab = 145, GraveStone = 146, GraveCross = 147, GraveFence = 148,
        MausoleumDoor = 149, OssuaryNiche = 150, BonePile = 151, SkullStack = 152,
        BurialUrn = 153, OfferingBowl = 154, MourningBench = 155, FuneralCandleStand = 156,
        IncenseBrazier = 157, ShroudRack = 158, GraveDiggerTools = 159, SoilMound = 160,
        BrokenHeadstone = 161, MemorialPlaque = 162, CryptGate = 163, CorpseCart = 164,
        FlowerOffering = 165, CatacombShelf = 166, BurialChest = 167, ReliquaryCasket = 168,

        FarmFence = 169, FarmGate = 170, Scarecrow = 171, Haystack = 172,
        GrainSilo = 173, FeedBin = 174, ChickenCoop = 175, RabbitHutch = 176,
        Beehive = 177, CompostPile = 178, Wheelbarrow = 179, Plow = 180,
        Harrow = 181, SeedChest = 182, WaterPump = 183, RainBarrel = 184,
        Clothesline = 185, WashTub = 186, GardenBench = 187, FlowerPlanter = 188,
        HedgeSection = 189, Trellis = 190, Arbor = 191, Statue = 192,
        Sundial = 193, StreetBench = 194, Bollard = 195, Signpost = 196,
        Milestone = 197, TrashHeap = 198, FirewoodPile = 199, WateringCanRack = 200,
    }

    public struct DecorationExpandedContentRecipe
    {
        public DecorationExpandedContentCategory Category;
        public DecorationExpandedContentKind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind AcceptedSockets;
        public DecorationMountMode MountMode;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 BaseSize;
        public int3 Clearance;
        public byte WidthJitter;
        public byte DepthJitter;

        public bool IsWellFormed =>
            (ushort)Kind >= 115 && (ushort)Kind <= 200 &&
            ProxyFamily != DecorationPropFamily.Unknown &&
            AcceptedSockets != DecorationSocketKind.None &&
            math.all(BaseSize > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpandedContentVariants
    {
        private const uint Marker = 0xC0000000u;
        private const uint KindMask = 0x3FF00000u;
        private const int KindShift = 20;
        private const uint VariationMask = 0x000FFFFFu;

        public static uint Encode(DecorationExpandedContentKind kind, uint variation) =>
            Marker | ((uint)kind << KindShift) | (variation & VariationMask);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & KindMask) >> KindShift);
        public static DecorationExpandedContentKind KindOf(uint variant)
        {
            ushort id = StableIdOf(variant);
            return id >= 115 && id <= 200 ? (DecorationExpandedContentKind)id : default;
        }
        public static bool IsExpanded(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 115 && id <= 200;
        }
    }

    public static class DecorationExpansion200Catalog
    {
        public const int FirstId = 115;
        public const int LastId = 200;
        public const int Count = LastId - FirstId + 1;

        public static bool IsDefined(DecorationExpandedContentKind kind) =>
            (ushort)kind >= FirstId && (ushort)kind <= LastId;

        public static DecorationExpandedContentRecipe Recipe(DecorationExpandedContentKind kind)
        {
            ushort id = (ushort)kind;
            if (id < FirstId || id > LastId) return default;
            if (id <= 144) return Arcane(kind, id - 115);
            if (id <= 168) return Funerary(kind, id - 145);
            return Settlement(kind, id - 169);
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context, uint sceneId, uint slotId, DecorationExpandedContentKind kind)
        {
            DecorationExpandedContentRecipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int3 size = recipe.BaseSize;
            if (recipe.WidthJitter > 0) size.x += (int)(seed % (uint)(recipe.WidthJitter + 1)) * 2;
            if (recipe.DepthJitter > 0)
                size.z += (int)(DecorationSeed.Derive(seed, 0xE200u) % (uint)(recipe.DepthJitter + 1)) * 2;
            uint variation = DecorationSeed.Derive(seed,
                context.StyleId ^ ((uint)context.Wealth << 12) ^ ((uint)context.Condition << 8) ^ (uint)kind);
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily, AcceptedSockets = recipe.AcceptedSockets, MountMode = recipe.MountMode,
                Backend = recipe.Backend, Interaction = recipe.Interaction, Size = size,
                Clearance = recipe.Clearance, Variant = DecorationExpandedContentVariants.Encode(kind, variation),
            };
        }

        private static DecorationExpandedContentRecipe Arcane(DecorationExpandedContentKind kind, int index)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.WorkSurface, DecorationContentShape.Machine, DecorationContentShape.WallRack,
                DecorationContentShape.Pedestal, DecorationContentShape.Rack, DecorationContentShape.WallRack,
                DecorationContentShape.Pedestal, DecorationContentShape.WorkSurface, DecorationContentShape.Fountain,
                DecorationContentShape.Pedestal, DecorationContentShape.Machine, DecorationContentShape.WheelMachine,
                DecorationContentShape.Pedestal, DecorationContentShape.Rack, DecorationContentShape.WallRack,
                DecorationContentShape.Pedestal, DecorationContentShape.Rack, DecorationContentShape.Coffin,
                DecorationContentShape.Hearth, DecorationContentShape.Hearth, DecorationContentShape.Sign,
                DecorationContentShape.Pedestal, DecorationContentShape.Stack, DecorationContentShape.Pedestal,
                DecorationContentShape.Rack, DecorationContentShape.Cage, DecorationContentShape.Pedestal,
                DecorationContentShape.Stack, DecorationContentShape.Sign, DecorationContentShape.Sign,
            };
            bool wall = index == 2 || index == 4 || index == 5 || index == 12 || index == 13 || index == 14 ||
                        index == 16 || index == 18 || index == 23 || index == 28 || index == 29;
            bool thin = index == 20 || index == 28 || index == 29;
            bool mesh = index == 1 || index == 8 || index == 10 || index == 11 || index == 22 || index == 27;
            bool light = index == 18 || index == 19 || index == 22 || index == 27;
            return R(DecorationExpandedContentCategory.Arcane, kind, shapes[index],
                index == 17 ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Table,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.FloorAgainstWall : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh :
                    (index == 18 || index == 19 ? DecorationRenderBackend.VoxelStamp : DecorationRenderBackend.BoxAssembly),
                Flags(!wall && !thin && !mesh, index == 17, index == 17, light, index == 18 || index == 19),
                ArcaneSize(index), wall ? new int3(2, 1, 3) : new int3(3, 0, 3), 2, 2);
        }

        private static DecorationExpandedContentRecipe Funerary(DecorationExpandedContentKind kind, int index)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Coffin, DecorationContentShape.Monument, DecorationContentShape.Monument,
                DecorationContentShape.Post, DecorationContentShape.Sign, DecorationContentShape.Rack,
                DecorationContentShape.Stack, DecorationContentShape.Stack, DecorationContentShape.Pedestal,
                DecorationContentShape.Pedestal, DecorationContentShape.WorkSurface, DecorationContentShape.LampPost,
                DecorationContentShape.Hearth, DecorationContentShape.WallRack, DecorationContentShape.WallRack,
                DecorationContentShape.Stack, DecorationContentShape.Monument, DecorationContentShape.Sign,
                DecorationContentShape.Cage, DecorationContentShape.Cart, DecorationContentShape.Stack,
                DecorationContentShape.Rack, DecorationContentShape.Coffin, DecorationContentShape.Coffin,
            };
            bool wall = index == 4 || index == 5 || index == 13 || index == 14 || index == 17 || index == 20;
            bool mesh = index == 6 || index == 7 || index == 14 || index == 20;
            bool thin = index == 17;
            bool container = index == 22 || index == 23;
            bool light = index == 11 || index == 12;
            return R(DecorationExpandedContentCategory.Funerary, kind, shapes[index],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Crate,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh :
                    (index == 12 ? DecorationRenderBackend.VoxelStamp : DecorationRenderBackend.BoxAssembly),
                Flags(index == 0 || index == 3 || index == 10 || index == 18 || index == 19 || container,
                    index == 8 || index == 9 || index == 19 || container, container, light, index == 12),
                FunerarySize(index), wall ? new int3(2, 2, 1) : new int3(2, 0, 2), 2, 2);
        }

        private static DecorationExpandedContentRecipe Settlement(DecorationExpandedContentKind kind, int index)
        {
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Post, DecorationContentShape.Post, DecorationContentShape.Post,
                DecorationContentShape.Stack, DecorationContentShape.Monument, DecorationContentShape.Trough,
                DecorationContentShape.Cage, DecorationContentShape.Cage, DecorationContentShape.Stack,
                DecorationContentShape.Stack, DecorationContentShape.Cart, DecorationContentShape.Machine,
                DecorationContentShape.Machine, DecorationContentShape.Coffin, DecorationContentShape.Machine,
                DecorationContentShape.Tub, DecorationContentShape.Hanging, DecorationContentShape.Tub,
                DecorationContentShape.WorkSurface, DecorationContentShape.Stack, DecorationContentShape.WallRack,
                DecorationContentShape.WallRack, DecorationContentShape.Post, DecorationContentShape.Monument,
                DecorationContentShape.Pedestal, DecorationContentShape.WorkSurface, DecorationContentShape.Post,
                DecorationContentShape.Sign, DecorationContentShape.Monument, DecorationContentShape.Stack,
                DecorationContentShape.Stack, DecorationContentShape.WallRack,
            };
            bool wall = index == 20 || index == 21 || index == 31;
            bool mesh = index == 16 || index == 26;
            bool thin = index == 27;
            bool movable = index == 10 || index == 11 || index == 12 || index == 13 || index == 17 || index == 30;
            bool container = index == 13;
            bool voxel = index == 4 || index == 14 || index == 23 || index == 24;
            return R(DecorationExpandedContentCategory.Settlement, kind, shapes[index],
                container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Crate,
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh :
                    voxel ? DecorationRenderBackend.VoxelStamp : DecorationRenderBackend.BoxAssembly,
                Flags(index <= 8 || index == 14 || index == 18 || index == 22 || index == 23 || index == 24 || index == 25 || index == 28,
                    movable, container, false, false),
                SettlementSize(index), wall ? new int3(2, 2, 1) : new int3(2, 0, 2), 2, 2);
        }

        private static int3 ArcaneSize(int i)
        {
            if (i == 18) return new int3(20, 20, 12);
            if (i == 20) return new int3(28, 1, 28);
            if (i == 24) return new int3(20, 20, 8);
            if (i == 28 || i == 29) return new int3(22, 16, 1);
            return new int3(10 + (i % 4) * 3, 9 + (i % 5) * 2, 8 + (i % 3) * 3);
        }

        private static int3 FunerarySize(int i)
        {
            if (i == 0) return new int3(16, 8, 30);
            if (i == 3) return new int3(30, 12, 4);
            if (i == 18) return new int3(18, 24, 6);
            if (i == 19) return new int3(18, 12, 28);
            return new int3(8 + (i % 4) * 3, 8 + (i % 5) * 3, 6 + (i % 3) * 4);
        }

        private static int3 SettlementSize(int i)
        {
            if (i == 0) return new int3(24, 12, 4);
            if (i == 1) return new int3(12, 16, 4);
            if (i == 4) return new int3(18, 28, 18);
            if (i == 6 || i == 7) return new int3(20, 16, 18);
            if (i == 14) return new int3(8, 18, 8);
            if (i == 16) return new int3(28, 10, 3);
            if (i == 20 || i == 21) return new int3(24, 18, 4);
            return new int3(8 + (i % 5) * 4, 7 + (i % 4) * 3, 7 + (i % 4) * 4);
        }

        private static DecorationInteractionFlags Flags(bool blocking, bool movable, bool container, bool light, bool particles)
        {
            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (blocking) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (movable) flags |= DecorationInteractionFlags.Movable;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;
            if (particles) flags |= DecorationInteractionFlags.EmitsParticles;
            return flags;
        }

        private static DecorationExpandedContentRecipe R(
            DecorationExpandedContentCategory category, DecorationExpandedContentKind kind,
            DecorationContentShape shape, DecorationPropFamily proxy, DecorationSocketKind sockets,
            DecorationMountMode mount, DecorationRenderBackend backend, DecorationInteractionFlags interaction,
            int3 size, int3 clearance, byte widthJitter, byte depthJitter) => new DecorationExpandedContentRecipe
        {
            Category = category, Kind = kind, Shape = shape, ProxyFamily = proxy,
            AcceptedSockets = sockets, MountMode = mount, Backend = backend, Interaction = interaction,
            BaseSize = size, Clearance = clearance, WidthJitter = widthJitter, DepthJitter = depthJitter,
        };
    }
}
