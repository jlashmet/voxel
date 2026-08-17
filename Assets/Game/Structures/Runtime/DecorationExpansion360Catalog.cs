using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion360Kind : ushort
    {
        SacredAltar = 341, SideShrine = 342, PrayerBench = 343, Kneeler = 344,
        VotiveCandleStand = 345, HolyWaterFont = 346, OfferingChest = 347, RelicPedestal = 348,
        ReliquaryShrine = 349, SacredLectern = 350, ScriptureStand = 351, IncenseStand = 352,
        RitualBasin = 353, ShrineBell = 354, SacredBannerStand = 355, ProcessionalStaffRack = 356,
        PilgrimTokenBoard = 357, BlessingBrazier = 358, SacredCurtain = 359, DivineCrystalFocus = 360,
    }

    public struct DecorationExpansion360Recipe
    {
        public DecorationExpansion360Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;
        public bool IsWellFormed => (ushort)Kind >= 341 && (ushort)Kind <= 360 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion360Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion360Kind kind, uint variation) => Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion360(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 341 && id <= 360;
        }
        public static DecorationExpansion360Kind KindOf(uint variant) => IsExpansion360(variant) ? (DecorationExpansion360Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion360Catalog
    {
        public const int Count = 20;

        public static DecorationExpansion360Recipe Recipe(DecorationExpansion360Kind kind)
        {
            int i = (int)kind - 341;
            if (i < 0 || i >= Count) return default;
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Pedestal, DecorationContentShape.Pedestal, DecorationContentShape.Bench,
                DecorationContentShape.Bench, DecorationContentShape.LampPost, DecorationContentShape.Well,
                DecorationContentShape.Coffin, DecorationContentShape.Pedestal, DecorationContentShape.Monument,
                DecorationContentShape.Pedestal, DecorationContentShape.Pedestal, DecorationContentShape.LampPost,
                DecorationContentShape.Tub, DecorationContentShape.Hanging, DecorationContentShape.Post,
                DecorationContentShape.WallRack, DecorationContentShape.Sign, DecorationContentShape.Hearth,
                DecorationContentShape.Canopy, DecorationContentShape.Pedestal,
            };
            bool wall = i == 15 || i == 16 || i == 18;
            bool ceiling = i == 13;
            bool thin = i == 16 || i == 18;
            bool mesh = i == 13 || i == 19;
            bool container = i == 6;
            bool light = i == 4 || i == 11 || i == 17 || i == 19;
            DecorationSocketKind socket = ceiling ? DecorationSocketKind.Ceiling : wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor;
            DecorationMountMode mount = ceiling ? DecorationMountMode.Ceiling : wall ? DecorationMountMode.Wall : DecorationMountMode.Floor;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;
            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!wall && !ceiling && !thin) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;
            return new DecorationExpansion360Recipe
            {
                Kind = kind, Shape = shapes[i],
                ProxyFamily = container ? DecorationPropFamily.Chest :
                    wall ? DecorationPropFamily.Banner : i <= 1 || i == 7 || i == 8 || i == 19 ? DecorationPropFamily.Altar : DecorationPropFamily.Table,
                Sockets = socket, Mount = mount, Backend = backend, Interaction = flags,
                Size = Size(i), Clearance = wall ? new int3(1,1,1) : ceiling ? new int3(2,1,2) : new int3(2,0,2),
            };
        }

        public static DecorationPropDescriptor Describe(in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion360Kind kind)
        {
            DecorationExpansion360Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily, AcceptedSockets = recipe.Sockets, MountMode = recipe.Mount,
                Backend = recipe.Backend, Interaction = recipe.Interaction, Size = recipe.Size, Clearance = recipe.Clearance,
                Variant = DecorationExpansion360Variants.Encode(kind, DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind)),
            };
        }

        private static int3 Size(int i)
        {
            if (i == 0) return new int3(28, 18, 18);
            if (i == 8) return new int3(20, 30, 14);
            if (i == 13) return new int3(12, 12, 12);
            if (i == 18) return new int3(26, 24, 1);
            if (i == 19) return new int3(18, 26, 18);
            return new int3(10 + (i % 4) * 4, 8 + (i % 5) * 3, 8 + (i % 3) * 4);
        }
    }
}
