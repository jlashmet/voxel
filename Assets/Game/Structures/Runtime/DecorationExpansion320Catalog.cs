using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion320Kind : ushort
    {
        GlowingMushroomCluster = 301, GiantMushroomSeat = 302, ManaBlossom = 303,
        CrystalFlowerPatch = 304, EnchantedVineCluster = 305, LivingRootArch = 306,
        FairyRing = 307, FairyHouseNook = 308, SpiritLanternPlant = 309, WhisperingStone = 310,
        RuneStoneCircle = 311, Moonwell = 312, SunCrystalBloom = 313, FloatingSeedCluster = 314,
        WispNest = 315, EnchantedTreeShrine = 316, DruidStoneAltar = 317, HerbalistWildPatch = 318,
        MagicalPondLilies = 319, PetrifiedMagicTree = 320,
    }

    public struct DecorationExpansion320Recipe
    {
        public DecorationExpansion320Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;

        public bool IsWellFormed =>
            (ushort)Kind >= 301 && (ushort)Kind <= 320 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion320Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion320Kind kind, uint variation) =>
            Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion320(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 301 && id <= 320;
        }
        public static DecorationExpansion320Kind KindOf(uint variant) =>
            IsExpansion320(variant) ? (DecorationExpansion320Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion320Catalog
    {
        public const int Count = 20;

        public static DecorationExpansion320Recipe Recipe(DecorationExpansion320Kind kind)
        {
            int i = (int)kind - 301;
            if (i < 0 || i >= Count) return default;

            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Stack, DecorationContentShape.Pedestal, DecorationContentShape.Stack,
                DecorationContentShape.Stack, DecorationContentShape.Hanging, DecorationContentShape.Monument,
                DecorationContentShape.Sign, DecorationContentShape.Coffin, DecorationContentShape.LampPost,
                DecorationContentShape.Monument, DecorationContentShape.Monument, DecorationContentShape.Well,
                DecorationContentShape.Stack, DecorationContentShape.Hanging, DecorationContentShape.Stack,
                DecorationContentShape.Monument, DecorationContentShape.Pedestal, DecorationContentShape.Stack,
                DecorationContentShape.Tub, DecorationContentShape.Post,
            };

            bool wall = i == 4 || i == 7;
            bool thin = i == 6 || i == 18;
            bool mesh = i == 0 || i == 2 || i == 3 || i == 4 || i == 13 || i == 14 || i == 17;
            bool light = i == 0 || i == 2 || i == 3 || i == 8 || i == 12 || i == 14;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface :
                mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;
            DecorationSocketKind socket = wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor;
            DecorationMountMode mount = wall ? DecorationMountMode.Wall : DecorationMountMode.Floor;

            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!wall && !thin && i != 13) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;
            if (i == 7 || i == 9 || i == 16) flags |= DecorationInteractionFlags.Lootable;

            return new DecorationExpansion320Recipe
            {
                Kind = kind,
                Shape = shapes[i],
                ProxyFamily = wall ? DecorationPropFamily.Banner :
                    (i == 11 ? DecorationPropFamily.Fountain : DecorationPropFamily.Altar),
                Sockets = socket,
                Mount = mount,
                Backend = backend,
                Interaction = flags,
                Size = Size(i),
                Clearance = wall ? new int3(1, 1, 1) : new int3(2, 0, 2),
            };
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion320Kind kind)
        {
            DecorationExpansion320Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed)
                return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            uint variation = DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind);
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily,
                AcceptedSockets = recipe.Sockets,
                MountMode = recipe.Mount,
                Backend = recipe.Backend,
                Interaction = recipe.Interaction,
                Size = recipe.Size,
                Clearance = recipe.Clearance,
                Variant = DecorationExpansion320Variants.Encode(kind, variation),
            };
        }

        private static int3 Size(int i)
        {
            if (i == 5) return new int3(30, 28, 8);
            if (i == 6 || i == 18) return new int3(24, 1, 24);
            if (i == 11) return new int3(24, 10, 24);
            if (i == 19) return new int3(20, 32, 20);
            return new int3(8 + (i % 4) * 4, 6 + (i % 5) * 3, 8 + (i % 3) * 4);
        }
    }
}
