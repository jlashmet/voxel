using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion340Kind : ushort
    {
        StonePressurePlate = 321, RunePressurePlate = 322, DartSlit = 323, SpikeFloorPanel = 324,
        FlameJetNozzle = 325, PoisonVent = 326, SwingingBladePivot = 327, PendulumAxeMount = 328,
        FallingBlockTrigger = 329, PortcullisWinch = 330, ChainWinch = 331, PuzzleLeverPedestal = 332,
        RotatingStatuePedestal = 333, RuneDial = 334, GemSocketPuzzle = 335, FloorTilePuzzle = 336,
        MirrorPuzzleStand = 337, MagicSealDoor = 338, WardEmitterPillar = 339, TreasureTrapChest = 340,
    }

    public struct DecorationExpansion340Recipe
    {
        public DecorationExpansion340Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;

        public bool IsWellFormed =>
            (ushort)Kind >= 321 && (ushort)Kind <= 340 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion340Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion340Kind kind, uint variation) =>
            Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion340(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 321 && id <= 340;
        }
        public static DecorationExpansion340Kind KindOf(uint variant) =>
            IsExpansion340(variant) ? (DecorationExpansion340Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion340Catalog
    {
        public const int Count = 20;

        public static DecorationExpansion340Recipe Recipe(DecorationExpansion340Kind kind)
        {
            int i = (int)kind - 321;
            if (i < 0 || i >= Count) return default;

            DecorationContentShape[] shapes =
            {
                DecorationContentShape.Sign, DecorationContentShape.Sign, DecorationContentShape.WallRack,
                DecorationContentShape.Sign, DecorationContentShape.Post, DecorationContentShape.Post,
                DecorationContentShape.Hanging, DecorationContentShape.Hanging, DecorationContentShape.Monument,
                DecorationContentShape.Machine, DecorationContentShape.Machine, DecorationContentShape.Pedestal,
                DecorationContentShape.Pedestal, DecorationContentShape.Machine, DecorationContentShape.Pedestal,
                DecorationContentShape.Sign, DecorationContentShape.Pedestal, DecorationContentShape.Monument,
                DecorationContentShape.Post, DecorationContentShape.Coffin,
            };

            bool wall = i == 2 || i == 4 || i == 5 || i == 17;
            bool ceiling = i == 6 || i == 7 || i == 8;
            bool thin = i == 0 || i == 1 || i == 3 || i == 15;
            bool mesh = i == 6 || i == 7 || i == 13 || i == 14 || i == 16;
            bool magic = i == 1 || i == 13 || i == 14 || i == 16 || i == 17 || i == 18;
            bool container = i == 19;

            DecorationSocketKind socket = ceiling ? DecorationSocketKind.Ceiling :
                wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor;
            DecorationMountMode mount = ceiling ? DecorationMountMode.Ceiling :
                wall ? DecorationMountMode.Wall : DecorationMountMode.Floor;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface :
                mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;

            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!thin && !wall && !ceiling) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (magic) flags |= DecorationInteractionFlags.EmitsLight;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;

            return new DecorationExpansion340Recipe
            {
                Kind = kind,
                Shape = shapes[i],
                ProxyFamily = container ? DecorationPropFamily.Chest :
                    wall ? DecorationPropFamily.WeaponRack :
                    ceiling ? DecorationPropFamily.Chandelier : DecorationPropFamily.Altar,
                Sockets = socket,
                Mount = mount,
                Backend = backend,
                Interaction = flags,
                Size = Size(i),
                Clearance = wall ? new int3(1, 1, 1) : ceiling ? new int3(2, 1, 2) : new int3(2, 0, 2),
            };
        }

        public static DecorationPropDescriptor Describe(
            in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion340Kind kind)
        {
            DecorationExpansion340Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed)
                return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            uint variation = DecorationSeed.Derive(seed,
                context.StyleId ^ (uint)kind ^ ((uint)context.Condition << 7));
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily,
                AcceptedSockets = recipe.Sockets,
                MountMode = recipe.Mount,
                Backend = recipe.Backend,
                Interaction = recipe.Interaction,
                Size = recipe.Size,
                Clearance = recipe.Clearance,
                Variant = DecorationExpansion340Variants.Encode(kind, variation),
            };
        }

        private static int3 Size(int i)
        {
            if (i == 0 || i == 1 || i == 3 || i == 15) return new int3(18, 1, 18);
            if (i == 17) return new int3(28, 34, 4);
            if (i == 18) return new int3(12, 28, 12);
            if (i == 19) return new int3(18, 14, 14);
            if (i == 6 || i == 7) return new int3(16, 18, 16);
            return new int3(10 + (i % 4) * 4, 8 + (i % 5) * 3, 10 + (i % 3) * 4);
        }
    }
}
