using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum GuildSignatureKind : ushort
    {
        GuildCrestPlaque = 401, HangingGuildSign = 402, GuildmasterChair = 403, GuildmasterDesk = 404,
        MembershipRosterBoard = 405, InitiationPedestal = 406, OathStone = 407, KnightOathBanner = 408,
        ArmorMaintenanceRack = 409, TournamentShieldWall = 410, AssassinTargetSilhouette = 411,
        PoisonLockCabinet = 412, ConcealedWeaponPanel = 413, CodedContractBoard = 414,
        DruidSeedShrine = 415, AnimalTotemPole = 416, LivingRootSeat = 417, HerbDryingTree = 418,
        LockPracticeBoard = 419, StolenGoodsSortingTable = 420, ConcealedFloorCache = 421,
        HealerCot = 422, MedicineScreen = 423, BlessingTable = 424, RangerBowyerStation = 425,
        FletchingBench = 426, HuntingMapWall = 427, BardStageRiser = 428, InstrumentCabinet = 429,
        SongBoard = 430, CostumeTrunk = 431, AlchemistFumeHood = 432, ReagentSortingWheel = 433,
        UnstableExperimentCage = 434, WizardGuildSeal = 435, SpellRankBoard = 436,
        FamiliarFeedingStation = 437, AdventurerPartyTable = 438, TrophyMonsterMount = 439,
        GuildDonationChest = 440,
    }

    public readonly struct GuildSignatureRecipe
    {
        public readonly GuildSignatureKind Kind;
        public readonly DecorationContentShape Shape;
        public readonly DecorationPropFamily ProxyFamily;
        public readonly DecorationSocketKind Sockets;
        public readonly DecorationMountMode Mount;
        public readonly DecorationRenderBackend Backend;
        public readonly DecorationInteractionFlags Interaction;
        public readonly int3 Size;
        public readonly int3 Clearance;

        public GuildSignatureRecipe(GuildSignatureKind kind, DecorationContentShape shape,
            DecorationPropFamily family, DecorationSocketKind sockets, DecorationMountMode mount,
            DecorationRenderBackend backend, DecorationInteractionFlags interaction, int3 size, int3 clearance)
        {
            Kind = kind; Shape = shape; ProxyFamily = family; Sockets = sockets; Mount = mount;
            Backend = backend; Interaction = interaction; Size = size; Clearance = clearance;
        }

        public bool IsWellFormed => (ushort)Kind >= 401 && (ushort)Kind <= 440 &&
            ProxyFamily != DecorationPropFamily.Unknown && Sockets != DecorationSocketKind.None &&
            math.all(Size > 0) && math.all(Clearance >= 0);
    }

    public static class GuildSignatureVariants
    {
        private const uint Marker = 0xC0000000u;
        public static uint Encode(GuildSignatureKind kind, uint variation) =>
            Marker | ((uint)kind << 20) | (variation & 0x000FFFFFu);
        public static ushort StableIdOf(uint variant) => (ushort)((variant & 0x3FF00000u) >> 20);
        public static bool IsGuildSignature(uint variant)
        {
            ushort id = StableIdOf(variant);
            return (variant & 0xC0000000u) == Marker && id >= 401 && id <= 440;
        }
        public static GuildSignatureKind KindOf(uint variant) =>
            IsGuildSignature(variant) ? (GuildSignatureKind)StableIdOf(variant) : default;
    }

    public static class GuildSignatureDecorationCatalog
    {
        public const int Count = 40;

        public static GuildSignatureRecipe Recipe(GuildSignatureKind kind)
        {
            int i = (int)kind - 401;
            if (i < 0 || i >= Count) return default;

            bool wall = kind == GuildSignatureKind.GuildCrestPlaque || kind == GuildSignatureKind.HangingGuildSign ||
                kind == GuildSignatureKind.MembershipRosterBoard || kind == GuildSignatureKind.KnightOathBanner ||
                kind == GuildSignatureKind.TournamentShieldWall || kind == GuildSignatureKind.AssassinTargetSilhouette ||
                kind == GuildSignatureKind.ConcealedWeaponPanel || kind == GuildSignatureKind.CodedContractBoard ||
                kind == GuildSignatureKind.LockPracticeBoard || kind == GuildSignatureKind.HuntingMapWall ||
                kind == GuildSignatureKind.SongBoard || kind == GuildSignatureKind.SpellRankBoard ||
                kind == GuildSignatureKind.TrophyMonsterMount;
            bool thin = kind == GuildSignatureKind.KnightOathBanner || kind == GuildSignatureKind.AssassinTargetSilhouette ||
                kind == GuildSignatureKind.CodedContractBoard || kind == GuildSignatureKind.HuntingMapWall ||
                kind == GuildSignatureKind.SongBoard || kind == GuildSignatureKind.SpellRankBoard;
            bool mesh = kind == GuildSignatureKind.AnimalTotemPole || kind == GuildSignatureKind.LivingRootSeat ||
                kind == GuildSignatureKind.HerbDryingTree || kind == GuildSignatureKind.ReagentSortingWheel ||
                kind == GuildSignatureKind.TrophyMonsterMount;
            bool container = kind == GuildSignatureKind.PoisonLockCabinet || kind == GuildSignatureKind.ConcealedFloorCache ||
                kind == GuildSignatureKind.CostumeTrunk || kind == GuildSignatureKind.GuildDonationChest;
            bool light = kind == GuildSignatureKind.DruidSeedShrine || kind == GuildSignatureKind.WizardGuildSeal ||
                kind == GuildSignatureKind.InitiationPedestal;

            DecorationContentShape shape = Shape(kind);
            DecorationPropFamily family = container ? DecorationPropFamily.Chest :
                wall ? DecorationPropFamily.Shelf : shape == DecorationContentShape.Cage ? DecorationPropFamily.Chest : DecorationPropFamily.Table;
            DecorationSocketKind sockets = wall ? DecorationSocketKind.Wall : DecorationSocketKind.Floor;
            DecorationMountMode mount = wall ? DecorationMountMode.Wall : DecorationMountMode.Floor;
            DecorationRenderBackend backend = thin ? DecorationRenderBackend.ThinSurface :
                mesh ? DecorationRenderBackend.ProceduralMesh : DecorationRenderBackend.BoxAssembly;
            DecorationInteractionFlags flags = DecorationInteractionFlags.Destructible;
            if (!wall && !thin && !mesh) flags |= DecorationInteractionFlags.BlocksNavigation;
            if (container) flags |= DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;
            if (light) flags |= DecorationInteractionFlags.EmitsLight;

            int3 size = wall ? new int3(12 + (i % 4) * 3, 10 + (i % 3) * 3, 2) :
                new int3(10 + (i % 5) * 3, 8 + (i % 4) * 3, 8 + (i % 3) * 3);
            return new GuildSignatureRecipe(kind, shape, family, sockets, mount, backend, flags, size,
                wall ? new int3(1, 1, 1) : new int3(2, 0, 2));
        }

        public static DecorationPropDescriptor Describe(in DecorationContext context, uint sceneId, uint slotId, GuildSignatureKind kind)
        {
            GuildSignatureRecipe r = Recipe(kind);
            if (!context.IsWellFormed || !r.IsWellFormed || sceneId == 0 || slotId == 0) return default;
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            uint variation = DecorationSeed.Derive(seed, context.StyleId ^ (uint)kind ^ ((uint)context.Wealth << 9));
            return new DecorationPropDescriptor
            {
                Family = r.ProxyFamily, AcceptedSockets = r.Sockets, MountMode = r.Mount, Backend = r.Backend,
                Interaction = r.Interaction, Size = r.Size, Clearance = r.Clearance,
                Variant = GuildSignatureVariants.Encode(kind, variation),
            };
        }

        private static DecorationContentShape Shape(GuildSignatureKind kind)
        {
            switch (kind)
            {
                case GuildSignatureKind.GuildCrestPlaque:
                case GuildSignatureKind.HangingGuildSign:
                case GuildSignatureKind.MembershipRosterBoard:
                case GuildSignatureKind.KnightOathBanner:
                case GuildSignatureKind.TournamentShieldWall:
                case GuildSignatureKind.AssassinTargetSilhouette:
                case GuildSignatureKind.ConcealedWeaponPanel:
                case GuildSignatureKind.CodedContractBoard:
                case GuildSignatureKind.LockPracticeBoard:
                case GuildSignatureKind.HuntingMapWall:
                case GuildSignatureKind.SongBoard:
                case GuildSignatureKind.SpellRankBoard:
                case GuildSignatureKind.TrophyMonsterMount: return DecorationContentShape.Sign;
                case GuildSignatureKind.InitiationPedestal:
                case GuildSignatureKind.OathStone:
                case GuildSignatureKind.DruidSeedShrine:
                case GuildSignatureKind.WizardGuildSeal: return DecorationContentShape.Pedestal;
                case GuildSignatureKind.ArmorMaintenanceRack:
                case GuildSignatureKind.InstrumentCabinet:
                case GuildSignatureKind.RangerBowyerStation: return DecorationContentShape.Rack;
                case GuildSignatureKind.UnstableExperimentCage: return DecorationContentShape.Cage;
                case GuildSignatureKind.AnimalTotemPole:
                case GuildSignatureKind.HerbDryingTree: return DecorationContentShape.Post;
                case GuildSignatureKind.AlchemistFumeHood: return DecorationContentShape.Hearth;
                default: return DecorationContentShape.WorkSurface;
            }
        }
    }
}
