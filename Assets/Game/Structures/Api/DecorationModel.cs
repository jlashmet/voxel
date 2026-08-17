using System;
using Unity.Mathematics;

namespace Game.Structures.Api
{
    public enum DecorationStructureKind : byte
    {
        Unknown = 0,
        Castle = 1,
        House = 2,
        Inn = 3,
        Church = 4,
        Ruin = 5,
        Cave = 6,
        Mine = 7,
        Camp = 8,
        Dungeon = 9,
    }

    public enum DecorationSpaceKind : byte
    {
        Unknown = 0,
        Bedroom = 1,
        DiningRoom = 2,
        GuardPost = 3,
        Chapel = 4,
        Study = 5,
        Storage = 6,
        CaveChamber = 7,
        MineTunnel = 8,
        Shrine = 9,
        ExteriorYard = 10,
    }

    public enum DecorationWealthTier : byte
    {
        Poor = 0,
        Modest = 1,
        Comfortable = 2,
        Wealthy = 3,
        Noble = 4,
    }

    public enum DecorationConditionTier : byte
    {
        Ruined = 0,
        Abandoned = 1,
        Worn = 2,
        Maintained = 3,
        Pristine = 4,
    }

    [Flags]
    public enum DecorationEnvironmentTags : uint
    {
        None = 0,
        Interior = 1u << 0,
        Exterior = 1u << 1,
        Underground = 1u << 2,
        Damp = 1u << 3,
        Cold = 1u << 4,
        Hot = 1u << 5,
        Sacred = 1u << 6,
        Military = 1u << 7,
        Residential = 1u << 8,
        Abandoned = 1u << 9,
    }

    [Flags]
    public enum DecorationSocketKind : ushort
    {
        None = 0,
        Floor = 1 << 0,
        Wall = 1 << 1,
        Corner = 1 << 2,
        Ceiling = 1 << 3,
        Tabletop = 1 << 4,
        BesideAnchor = 1 << 5,
        AboveAnchor = 1 << 6,
        DoorwaySide = 1 << 7,
        WindowSide = 1 << 8,
    }

    public enum DecorationPropFamily : ushort
    {
        Unknown = 0,
        Bed = 1,
        Dresser = 2,
        Rug = 3,
        Painting = 4,
        WallTorch = 5,
        Table = 6,
        Chair = 7,
        Bench = 8,
        Chest = 9,
        Shelf = 10,
        Bookcase = 11,
        Fireplace = 12,
        Candle = 13,
        Chandelier = 14,
        Banner = 15,
        Curtain = 16,
        WeaponRack = 17,
        Altar = 18,
        Crate = 19,
        Barrel = 20,
        Bedroll = 21,
        Campfire = 22,
        Lantern = 23,
    }

    public enum DecorationRenderBackend : byte
    {
        BoxAssembly = 0,
        ThinSurface = 1,
        VoxelStamp = 2,
        ProceduralMesh = 3,
    }

    public enum DecorationMountMode : byte
    {
        Floor = 0,
        Wall = 1,
        Ceiling = 2,
        Surface = 3,
    }

    [Flags]
    public enum DecorationInteractionFlags : ushort
    {
        None = 0,
        BlocksNavigation = 1 << 0,
        Destructible = 1 << 1,
        Container = 1 << 2,
        Lootable = 1 << 3,
        Movable = 1 << 4,
        EmitsLight = 1 << 5,
        EmitsParticles = 1 << 6,
    }

    public struct DecorationContext
    {
        public uint WorldSeed;
        public uint StructureId;
        public uint SpaceId;
        public DecorationStructureKind StructureKind;
        public DecorationSpaceKind SpaceKind;
        public uint StyleId;
        public DecorationWealthTier Wealth;
        public DecorationConditionTier Condition;
        public DecorationEnvironmentTags Environment;

        public bool IsWellFormed =>
            StructureId != 0 &&
            SpaceId != 0 &&
            StructureKind != DecorationStructureKind.Unknown &&
            SpaceKind != DecorationSpaceKind.Unknown &&
            (byte)Wealth <= (byte)DecorationWealthTier.Noble &&
            (byte)Condition <= (byte)DecorationConditionTier.Pristine;
    }

    public struct DecorationBounds
    {
        public int3 Min;
        public int3 MaxExclusive;

        public int3 Size => MaxExclusive - Min;
        public int3 Center => Min + Size / 2;
        public bool IsWellFormed => math.all(MaxExclusive > Min);

        public bool Contains(in DecorationBounds other) =>
            math.all(other.Min >= Min) && math.all(other.MaxExclusive <= MaxExclusive);

        public bool Overlaps(in DecorationBounds other) =>
            math.all(Min < other.MaxExclusive) && math.all(other.Min < MaxExclusive);

        public DecorationBounds Expanded(int3 amount) => new DecorationBounds
        {
            Min = Min - amount,
            MaxExclusive = MaxExclusive + amount,
        };
    }

    public struct DecorationSpace
    {
        public uint SpaceId;
        public DecorationSpaceKind Kind;
        public DecorationBounds Bounds;

        public bool IsWellFormed =>
            SpaceId != 0 &&
            Kind != DecorationSpaceKind.Unknown &&
            Bounds.IsWellFormed;
    }

    public struct DecorationExclusion
    {
        public DecorationBounds Bounds;
        public uint Tag;

        public bool IsWellFormed => Bounds.IsWellFormed;
    }

    public struct DecorationSocket
    {
        public uint Id;
        public DecorationSocketKind Kind;
        public int3 Position;
        public int3 Facing;
        public int2 UsableSize;
        public uint Tags;
        public uint AnchorId;

        public bool IsWellFormed =>
            Id != 0 &&
            Kind != DecorationSocketKind.None &&
            UsableSize.x > 0 &&
            UsableSize.y > 0 &&
            math.abs(Facing.x) + math.abs(Facing.y) + math.abs(Facing.z) <= 1;
    }

    public struct DecorationPropDescriptor
    {
        public DecorationPropFamily Family;
        public DecorationSocketKind AcceptedSockets;
        public DecorationMountMode MountMode;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public int3 Size;
        public int3 Clearance;
        public uint Variant;

        public bool IsWellFormed =>
            Family != DecorationPropFamily.Unknown &&
            AcceptedSockets != DecorationSocketKind.None &&
            math.all(Size > 0) &&
            math.all(Clearance >= 0);
    }

    public struct GeneratedPropId : IEquatable<GeneratedPropId>
    {
        public ulong High;
        public ulong Low;

        public bool IsValid => High != 0 || Low != 0;
        public bool Equals(GeneratedPropId other) => High == other.High && Low == other.Low;
        public override bool Equals(object obj) => obj is GeneratedPropId other && Equals(other);
        public override int GetHashCode() => unchecked((High.GetHashCode() * 397) ^ Low.GetHashCode());
        public override string ToString() => $"{High:x16}{Low:x16}";
        public static bool operator ==(GeneratedPropId a, GeneratedPropId b) => a.Equals(b);
        public static bool operator !=(GeneratedPropId a, GeneratedPropId b) => !a.Equals(b);
    }

    public struct DecorationPlacement
    {
        public GeneratedPropId Id;
        public uint SceneId;
        public uint SlotId;
        public uint AnchorSlotId;
        public DecorationPropFamily Family;
        public uint Variant;
        public DecorationBounds Bounds;
        public int3 Facing;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public uint StyleId;
        public DecorationWealthTier Wealth;
        public DecorationConditionTier Condition;

        public bool IsWellFormed =>
            Id.IsValid &&
            SceneId != 0 &&
            SlotId != 0 &&
            Family != DecorationPropFamily.Unknown &&
            Bounds.IsWellFormed &&
            math.abs(Facing.x) + math.abs(Facing.y) + math.abs(Facing.z) <= 1 &&
            (byte)Wealth <= (byte)DecorationWealthTier.Noble &&
            (byte)Condition <= (byte)DecorationConditionTier.Pristine;
    }

    public struct DecorationSceneSlot
    {
        public uint SlotId;
        public DecorationPropFamily Family;
        public DecorationSocketKind RequestedSocket;
        public uint AnchorSlotId;
        public ushort Weight;
        public bool Required;

        public bool IsWellFormed =>
            SlotId != 0 &&
            Family != DecorationPropFamily.Unknown &&
            RequestedSocket != DecorationSocketKind.None &&
            Weight > 0 &&
            AnchorSlotId != SlotId;
    }

    public struct DecorationScene
    {
        public uint SceneId;
        public DecorationSceneSlot[] Slots;

        public bool IsWellFormed
        {
            get
            {
                if (SceneId == 0 || Slots == null || Slots.Length == 0)
                    return false;
                for (int i = 0; i < Slots.Length; i++)
                    if (!Slots[i].IsWellFormed)
                        return false;
                return true;
            }
        }
    }
}
