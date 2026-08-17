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
        FloorAgainstWall = 1,
        Wall = 2,
        Ceiling = 3,
        AnchorRelative = 4,
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

    [Flags]
    public enum DecorationExclusionKind : byte
    {
        None = 0,
        Door = 1 << 0,
        Stair = 1 << 1,
        Navigation = 1 << 2,
        Gameplay = 1 << 3,
        Hazard = 1 << 4,
    }

    /// <summary>Deterministic game-facing context for one decoration pass.</summary>
    public struct DecorationContext
    {
        public uint WorldSeed;
        public uint StructureId;
        public uint SpaceId;
        public uint StyleId;
        public DecorationStructureKind StructureKind;
        public DecorationSpaceKind SpaceKind;
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

    /// <summary>Integer voxel AABB using an inclusive minimum and exclusive maximum.</summary>
    public struct DecorationBounds
    {
        public int3 Min;
        public int3 MaxExclusive;

        public int3 Size => MaxExclusive - Min;
        public bool IsWellFormed => math.all(MaxExclusive > Min);

        public bool Contains(in DecorationBounds other) =>
            math.all(other.Min >= Min) && math.all(other.MaxExclusive <= MaxExclusive);

        public bool Overlaps(in DecorationBounds other) =>
            math.all(Min < other.MaxExclusive) && math.all(MaxExclusive > other.Min);

        public DecorationBounds Expanded(int3 amount)
        {
            int3 safe = math.max(amount, int3.zero);
            return new DecorationBounds
            {
                Min = Min - safe,
                MaxExclusive = MaxExclusive + safe,
            };
        }
    }

    public struct DecorationSpace
    {
        public uint SpaceId;
        public DecorationSpaceKind Kind;
        public DecorationBounds Bounds;

        public bool IsWellFormed =>
            SpaceId != 0 && Kind != DecorationSpaceKind.Unknown && Bounds.IsWellFormed;
    }

    public struct DecorationExclusion
    {
        public DecorationExclusionKind Kind;
        public DecorationBounds Bounds;

        public bool IsWellFormed => Kind != DecorationExclusionKind.None && Bounds.IsWellFormed;
    }

    public struct DecorationSocket
    {
        public uint SocketId;
        public DecorationSocketKind Kind;
        public DecorationBounds Bounds;
        /// <summary>Cardinal direction pointing from the supporting surface into usable room volume.</summary>
        public int3 Facing;
        public uint AnchorSlotId;

        public bool IsWellFormed =>
            SocketId != 0 &&
            DecorationValidation.IsSingleSocketKind(Kind) &&
            Bounds.IsWellFormed &&
            math.csum(math.abs(Facing)) == 1;
    }

    /// <summary>Backend-independent description of one parameterized prop family variant.</summary>
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

        public bool IsWellFormed => DecorationValidation.IsWellFormed(this);
        public bool Accepts(DecorationSocketKind kind) => (AcceptedSockets & kind) != 0;
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
            DecorationValidation.IsSingleSocketKind(RequestedSocket) &&
            Weight > 0 &&
            AnchorSlotId != SlotId;
    }

    public readonly struct GeneratedPropId : IEquatable<GeneratedPropId>
    {
        public readonly ulong Value;

        public GeneratedPropId(ulong value) => Value = value == 0 ? 1UL : value;

        public bool Equals(GeneratedPropId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GeneratedPropId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString("X16");
        public static bool operator ==(GeneratedPropId left, GeneratedPropId right) => left.Equals(right);
        public static bool operator !=(GeneratedPropId left, GeneratedPropId right) => !left.Equals(right);
    }

    /// <summary>Resolved semantic output. Render/build systems consume this without re-running scene logic.</summary>
    public struct DecorationPlacement
    {
        public GeneratedPropId Id;
        public uint SceneId;
        public uint SlotId;
        public uint AnchorSlotId;
        public uint SocketId;
        public DecorationPropFamily Family;
        public DecorationRenderBackend Backend;
        public DecorationInteractionFlags Interaction;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;

        public bool IsWellFormed =>
            Id.Value != 0 &&
            SceneId != 0 &&
            SlotId != 0 &&
            Family != DecorationPropFamily.Unknown &&
            Bounds.IsWellFormed &&
            math.csum(math.abs(Facing)) == 1;
    }

    public static class DecorationSeed
    {
        public static uint Derive(uint parent, uint discriminator)
        {
            uint value = parent ^ (discriminator + 0x9E3779B9u + (parent << 6) + (parent >> 2));
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0 ? 0xA511E9B3u : value;
        }

        public static uint ForSpace(in DecorationContext context) =>
            Derive(Derive(context.WorldSeed, context.StructureId), context.SpaceId);

        public static uint ForScene(in DecorationContext context, uint sceneId) =>
            Derive(ForSpace(in context), sceneId);

        public static uint ForSlot(in DecorationContext context, uint sceneId, uint slotId) =>
            Derive(ForScene(in context, sceneId), slotId);
    }

    public static class GeneratedPropIds
    {
        public static GeneratedPropId Create(in DecorationContext context, uint sceneId, uint slotId)
        {
            uint low = DecorationSeed.ForSlot(in context, sceneId, slotId);
            uint high = DecorationSeed.Derive(low, context.StyleId ^ ((uint)context.StructureKind << 24));
            return new GeneratedPropId(((ulong)high << 32) | low);
        }
    }

    public static class DecorationValidation
    {
        public static bool IsSingleSocketKind(DecorationSocketKind kind)
        {
            uint value = (uint)kind;
            return value != 0 && (value & (value - 1)) == 0;
        }

        public static bool IsWellFormed(in DecorationPropDescriptor descriptor)
        {
            if (descriptor.Family == DecorationPropFamily.Unknown || descriptor.AcceptedSockets == DecorationSocketKind.None)
                return false;
            if (!math.all(descriptor.Size > 0) || math.any(descriptor.Clearance < 0))
                return false;

            switch (descriptor.MountMode)
            {
                case DecorationMountMode.Floor:
                    return (descriptor.AcceptedSockets & DecorationSocketKind.Floor) != 0;
                case DecorationMountMode.FloorAgainstWall:
                case DecorationMountMode.Wall:
                    return (descriptor.AcceptedSockets & DecorationSocketKind.Wall) != 0;
                case DecorationMountMode.Ceiling:
                    return (descriptor.AcceptedSockets & DecorationSocketKind.Ceiling) != 0;
                case DecorationMountMode.AnchorRelative:
                    return (descriptor.AcceptedSockets &
                        (DecorationSocketKind.BesideAnchor | DecorationSocketKind.AboveAnchor | DecorationSocketKind.Floor)) != 0;
                default:
                    return false;
            }
        }

        /// <summary>Validates uniqueness, anchor existence, and acyclic slot dependencies.</summary>
        public static bool ValidateScene(DecorationSceneSlot[] slots, out uint errorSlotId)
        {
            errorSlotId = 0;
            if (slots == null || slots.Length == 0)
                return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsWellFormed)
                {
                    errorSlotId = slots[i].SlotId;
                    return false;
                }

                for (int j = i + 1; j < slots.Length; j++)
                {
                    if (slots[i].SlotId == slots[j].SlotId)
                    {
                        errorSlotId = slots[i].SlotId;
                        return false;
                    }
                }

                if (slots[i].AnchorSlotId != 0 && FindSlot(slots, slots[i].AnchorSlotId) < 0)
                {
                    errorSlotId = slots[i].SlotId;
                    return false;
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                uint current = slots[i].AnchorSlotId;
                int hops = 0;
                while (current != 0)
                {
                    if (++hops > slots.Length)
                    {
                        errorSlotId = slots[i].SlotId;
                        return false;
                    }
                    int index = FindSlot(slots, current);
                    if (index < 0)
                    {
                        errorSlotId = slots[i].SlotId;
                        return false;
                    }
                    current = slots[index].AnchorSlotId;
                }
            }

            return true;
        }

        private static int FindSlot(DecorationSceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return i;
            return -1;
        }
    }

    /// <summary>Initial parameterized prop families used to prove the decoration pipeline.</summary>
    public static class DecorationPropPresets
    {
        public static DecorationPropDescriptor Bed(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.BedSlot);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bed,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(16 + wealth * 2 + (int)(seed & 1u) * 2, 8 + wealth, 28 + (int)((seed >> 1) & 1u) * 2),
                Clearance = new int3(3, 0, 6),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ (uint)context.Condition),
            };
        }

        public static DecorationPropDescriptor Dresser(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.DresserSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Dresser,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.FloorAgainstWall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable,
                Size = new int3(12 + (int)(seed & 3u) * 2, 16 + (int)context.Wealth * 2, 6),
                Clearance = new int3(3, 0, 5),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xD2E55E12u),
            };
        }

        public static DecorationPropDescriptor Rug(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.RugSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Rug,
                AcceptedSockets = DecorationSocketKind.Floor | DecorationSocketKind.BesideAnchor,
                MountMode = DecorationMountMode.AnchorRelative,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.None,
                Size = new int3(20 + (int)context.Wealth * 3, 1, 30 + (int)(seed & 3u) * 2),
                Clearance = int3.zero,
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xA6F31C91u),
            };
        }

        public static DecorationPropDescriptor Painting(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.PaintingSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Painting,
                AcceptedSockets = DecorationSocketKind.Wall | DecorationSocketKind.AboveAnchor,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                Size = new int3(10 + (int)(seed & 3u) * 2, 10 + (int)((seed >> 3) & 3u) * 2, 1),
                Clearance = new int3(2, 2, 0),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x9BC01F3Du),
            };
        }

        public static DecorationPropDescriptor WallTorch(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, BedroomSceneDefinition.SceneId, BedroomSceneDefinition.TorchSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.WallTorch,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible | DecorationInteractionFlags.EmitsLight |
                              DecorationInteractionFlags.EmitsParticles,
                Size = new int3(3, 8, 3),
                Clearance = new int3(5, 4, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xF17E5A11u),
            };
        }
    }

    public static class BedroomSceneDefinition
    {
        public const uint SceneId = 0x42454431u; // BED1
        public const uint BedSlot = 1;
        public const uint RugSlot = 2;
        public const uint DresserSlot = 3;
        public const uint PaintingSlot = 4;
        public const uint TorchSlot = 5;

        public static DecorationSceneSlot[] CreateSlots() => new[]
        {
            new DecorationSceneSlot
            {
                SlotId = BedSlot,
                Family = DecorationPropFamily.Bed,
                RequestedSocket = DecorationSocketKind.Wall,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = RugSlot,
                Family = DecorationPropFamily.Rug,
                RequestedSocket = DecorationSocketKind.BesideAnchor,
                AnchorSlotId = BedSlot,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = DresserSlot,
                Family = DecorationPropFamily.Dresser,
                RequestedSocket = DecorationSocketKind.Wall,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = PaintingSlot,
                Family = DecorationPropFamily.Painting,
                RequestedSocket = DecorationSocketKind.AboveAnchor,
                AnchorSlotId = DresserSlot,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = TorchSlot,
                Family = DecorationPropFamily.WallTorch,
                RequestedSocket = DecorationSocketKind.Wall,
                Weight = 1,
                Required = true,
            },
        };
    }
}
