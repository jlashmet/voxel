using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion380Kind : ushort
    {
        FloatingBookshelf = 361, EnchantedLectern = 362, StudentSpellDesk = 363, ApprenticeAlchemyDesk = 364,
        RunePracticeBoard = 365, SpellTargetDummy = 366, WandPracticeRack = 367, FamiliarStudyPerch = 368,
        MagicalGlobe = 369, ConstellationProjector = 370, AnimatedMapTable = 371, ForbiddenBookCage = 372,
        ChainedTomeStand = 373, ScrollSortingRack = 374, QuillAndInkStation = 375, ScriptoriumDesk = 376,
        ArcaneArchiveChest = 377, MagicalSpecimenCabinet = 378, PortalLessonFrame = 379, FacultyResearchDesk = 380,
    }

    public struct DecorationExpansion380Recipe
    {
        public DecorationExpansion380Kind Kind;
        public DecorationContentShape Shape;
        public DecorationPropFamily ProxyFamily;
        public DecorationSocketKind Sockets;
        public DecorationMountMode Mount;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;
        public bool IsWellFormed => (ushort)Kind >= 361 && (ushort)Kind <= 380 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class DecorationExpansion380Variants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(DecorationExpansion380Kind kind, uint variation) => Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsExpansion380(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 361 && id <= 380;
        }
        public static DecorationExpansion380Kind KindOf(uint variant) => IsExpansion380(variant) ? (DecorationExpansion380Kind)StableIdOf(variant) : default;
    }

    public static class DecorationExpansion380Catalog
    {
        public const int Count = 20;
        public static DecorationExpansion380Recipe Recipe(DecorationExpansion380Kind kind)
        {
            int i = (int)kind - 361;
            if (i < 0 || i >= Count) return default;
            DecorationContentShape[] shapes =
            {
                DecorationContentShape.WallRack, DecorationContentShape.Pedestal, DecorationContentShape.WorkSurface,
                DecorationContentShape.WorkSurface, DecorationContentShape.Sign, DecorationContentShape.Post,
                DecorationContentShape.WallRack, DecorationContentShape.Post, DecorationContentShape.Pedestal,
                DecorationContentShape.Machine, DecorationContentShape.WorkSurface, DecorationContentShape.Cage,
                DecorationContentShape.Pedestal, DecorationContentShape.Rack, DecorationContentShape.WorkSurface,
                DecorationContentShape.WorkSurface, DecorationContentShape.Coffin, DecorationContentShape.Rack,
                DecorationContentShape.Monument, DecorationContentShape.WorkSurface,
            };
            bool wall = i == 0 || i == 4 || i == 6;
            bool mesh = i == 0 || i == 8 || i == 9 || i == 10 || i == 12 || i == 18;
            bool container = i == 11 || i == 16 || i == 17;
            bool light = i == 0 || i == 1 || i == 8 || i == 9 || i == 10 || i == 18;
            DecorationRenderBackend backend = mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;
            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!wall && !mesh) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;
            return new DecorationExpansion380Recipe
            {
                Kind = kind, Shape = shapes[i],
                ProxyFamily = container ? DecorationPropFamily.Chest : wall ? DecorationPropFamily.Shelf : DecorationPropFamily.Table,
                Sockets = wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor,
                Mount = wall ? DecorationMountMode.Wall : DecorationMountMode.Floor,
                Backend = backend, Interaction = flags, Size = Size(i),
                Clearance = wall ? new int3(1,1,1) : new int3(2,0,2),
            };
        }

        public static DecorationPropDescriptor Describe(in DecorationContext context, uint sceneId, uint slotId, DecorationExpansion380Kind kind)
        {
            DecorationExpansion380Recipe recipe = Recipe(kind);
            if (!context.IsWellFormed || sceneId == 0 || slotId == 0 || !recipe.IsWellFormed) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            return new DecorationPropDescriptor
            {
                Family = recipe.ProxyFamily, AcceptedSockets = recipe.Sockets, MountMode = recipe.Mount,
                Backend = recipe.Backend, Interaction = recipe.Interaction, Size = recipe.Size, Clearance = recipe.Clearance,
                Variant = DecorationExpansion380Variants.Encode(kind, DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind)),
            };
        }

        private static int3 Size(int i)
        {
            if (i == 0) return new int3(28, 24, 4);
            if (i == 11) return new int3(18, 26, 16);
            if (i == 18) return new int3(24, 30, 8);
            return new int3(10 + (i % 5) * 4, 8 + (i % 4) * 4, 9 + (i % 3) * 4);
        }
    }
}
