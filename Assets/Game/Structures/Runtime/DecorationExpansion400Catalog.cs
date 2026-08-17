using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion400Kind : ushort
    {
        BrokenPortalFrame = 381, CrackedManaCrystal = 382, ArcaneScorchPatch = 383, CorruptionGrowth = 384,
        CursedVineCluster = 385, HauntedMirror = 386, SpectralCandleCluster = 387, FloatingDebrisCluster = 388,
        CursedChainBundle = 389, PetrifiedAdventurer = 390, PetrifiedMonster = 391, AbandonedRitualCircle = 392,
        BrokenRunePillar = 393, ShatteredMagicStatue = 394, CollapsedSpellShelf = 395, PossessedFurniture = 396,
        ShadowNest = 397, EctoplasmPool = 398, SealedCursedChest = 399, AncientMagicSeal = 400,
    }

    public struct DecorationExpansion400Recipe
    {
        public DecorationExpansion400Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;
        public bool IsWellFormed => (ushort)Kind >= 381 && (ushort)Kind <= 400 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion400Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion400Kind kind, uint variation) => Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion400(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 381 && id <= 400;
        }
        public static DecorationExpansion400Kind KindOf(uint variant) => IsExpansion400(variant) ? (DecorationExpansion400Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion400Catalog
    {
        public const int Count = 20;
        public static DecorationExpansion400Recipe Recipe(DecorationExpansion400Kind kind)
        {
            int i = (int)kind - 381;
            if (i < 0 || i >= Count) return default;
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Monument, DecorationContentShape.Stack, DecorationContentShape.Sign,
                DecorationContentShape.Stack, DecorationContentShape.Hanging, DecorationContentShape.Sign,
                DecorationContentShape.LampPost, DecorationContentShape.Hanging, DecorationContentShape.Hanging,
                DecorationContentShape.Monument, DecorationContentShape.Monument, DecorationContentShape.Sign,
                DecorationContentShape.Post, DecorationContentShape.Monument, DecorationContentShape.Rack,
                DecorationContentShape.WorkSurface, DecorationContentShape.Stack, DecorationContentShape.Tub,
                DecorationContentShape.Coffin, DecorationContentShape.Monument,
            };
            bool wall = i == 4 || i == 5 || i == 14;
            bool thin = i == 2 || i == 5 || i == 11 || i == 17 || i == 19;
            bool mesh = i == 1 || i == 3 || i == 4 || i == 6 || i == 7 || i == 8 || i == 9 || i == 10 || i == 16;
            bool container = i == 18;
            bool light = i == 1 || i == 6 || i == 16 || i == 17 || i == 19;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface : mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;
            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!wall && !thin && !mesh) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;
            return new DecorationExpansion400Recipe
            {
                Kind = kind, Shape = shapes[i],
                ProxyFamily = container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Banner : DecorationPropFamily.Altar,
                Sockets = wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                Mount = wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                Backend = backend, Interaction = flags, Size = Size(i),
                Clearance = wall ? new int3(1,1,1) : new int3(2,0,2),
            };
        }

        public static DecorationPropDescriptor Describe(in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion400Kind kind)
        {
            DecorationExpansion400Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily, AcceptedSockets = recipe.Sockets, MountMode = recipe.Mount,
                Backend = recipe.Backend, Interaction = recipe.Interaction, Size = recipe.Size, Clearance = recipe.Clearance,
                Variant = DecorationExpansion400Variants.Encode(kind, DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind ^ ((uint)context.Condition << 8))),
            };
        }

        private static int3 Size(int i)
        {
            if (i == 0) return new int3(28, 30, 8);
            if (i == 17) return new int3(24, 1, 20);
            if (i == 18) return new int3(18, 14, 14);
            if (i == 19) return new int3(30, 1, 30);
            return new int3(10 + (i % 5) * 4, 8 + (i % 4) * 4, 8 + (i % 3) * 4);
        }
    }
}
