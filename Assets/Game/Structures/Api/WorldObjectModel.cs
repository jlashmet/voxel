using System;
using Unity.Mathematics;

namespace Game.Structures.Api
{
    public readonly struct WorldObjectId : IEquatable<WorldObjectId>
    {
        public readonly ulong Value;

        public WorldObjectId(ulong value) => Value = value == 0 ? 1UL : value;

        public bool Equals(WorldObjectId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldObjectId other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Value ^ (Value >> 32)));
        public override string ToString() => Value.ToString("X16");
        public static bool operator ==(WorldObjectId left, WorldObjectId right) => left.Equals(right);
        public static bool operator !=(WorldObjectId left, WorldObjectId right) => !left.Equals(right);
    }

    public enum WorldObjectKind : ushort
    {
        Unknown = 0,
        Door = 1,
        Gate = 2,
        Portcullis = 3,
        Drawbridge = 4,
        Elevator = 5,
        MovingPlatform = 6,
        Lever = 7,
        Switch = 8,
        Button = 9,
        PressurePlate = 10,
        PullChain = 11,
        Chest = 12,
        Dresser = 13,
        Cabinet = 14,
        Crate = 15,
        Barrel = 16,
        Bed = 17,
        Chair = 18,
        Bench = 19,
        Torch = 20,
        Lantern = 21,
        Brazier = 22,
        Fireplace = 23,
        Trap = 24,
        SpikeTrap = 25,
        DartTrap = 26,
        FallingBlockTrap = 27,
        Crusher = 28,
        SecretDoor = 29,
        RotatingWall = 30,
        BreakableWall = 31,
        WeaponRack = 32,
        Bookshelf = 33,
        Altar = 34,
        Bell = 35,
        Winch = 36,
        Valve = 37,
        Generator = 38,
        FuseBox = 39,
        MineCart = 40,
        Cart = 41,
        Ladder = 42,
        Rope = 43,
        Zipline = 44,
        Teleporter = 45,
        Checkpoint = 46,
        SpawnPoint = 47,
    }

    [Flags]
    public enum WorldObjectCapabilities : uint
    {
        None = 0,
        Interactable = 1u << 0,
        Stateful = 1u << 1,
        Persistent = 1u << 2,
        Movable = 1u << 3,
        Destructible = 1u << 4,
        Container = 1u << 5,
        Lootable = 1u << 6,
        BlocksNavigation = 1u << 7,
        EmitsLight = 1u << 8,
        EmitsParticles = 1u << 9,
        SignalSource = 1u << 10,
        SignalTarget = 1u << 11,
        Rideable = 1u << 12,
        Climbable = 1u << 13,
        Hazard = 1u << 14,
        Usable = 1u << 15,
        Lockable = 1u << 16,
        PowerConsumer = 1u << 17,
        PowerSource = 1u << 18,
        Hidden = 1u << 19,
    }

    public enum WorldObjectSignal : byte
    {
        None = 0,
        Activated = 1,
        Deactivated = 2,
        Toggled = 3,
        Pressed = 4,
        Released = 5,
        Entered = 6,
        Exited = 7,
        Opened = 8,
        Closed = 9,
        Destroyed = 10,
        Looted = 11,
        Powered = 12,
        Unpowered = 13,
        Arrived = 14,
    }

    public enum WorldObjectAction : byte
    {
        None = 0,
        Activate = 1,
        Deactivate = 2,
        Toggle = 3,
        Open = 4,
        Close = 5,
        Lock = 6,
        Unlock = 7,
        Trigger = 8,
        Reset = 9,
        Enable = 10,
        Disable = 11,
        MoveToStop = 12,
        PowerOn = 13,
        PowerOff = 14,
        Reveal = 15,
        Hide = 16,
    }

    [Flags]
    public enum WorldObjectStateFlags : uint
    {
        None = 0,
        Active = 1u << 0,
        Open = 1u << 1,
        Locked = 1u << 2,
        Destroyed = 1u << 3,
        Looted = 1u << 4,
        Disabled = 1u << 5,
        Powered = 1u << 6,
        Hidden = 1u << 7,
        Triggered = 1u << 8,
        Moving = 1u << 9,
    }

    public struct WorldObjectDescriptor
    {
        public WorldObjectId Id;
        public WorldObjectKind Kind;
        public WorldObjectCapabilities Capabilities;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
        public uint LocalKey;
        public uint ParentId;
        public WorldObjectStateFlags DefaultState;
        public int Parameter0;
        public int Parameter1;
        public int Parameter2;
        public int Parameter3;

        public bool IsWellFormed =>
            Id.Value != 0 &&
            Kind != WorldObjectKind.Unknown &&
            Bounds.IsWellFormed &&
            math.csum(math.abs(Facing)) == 1;
    }

    public struct WorldObjectConnection
    {
        public WorldObjectId Source;
        public WorldObjectSignal Signal;
        public WorldObjectId Target;
        public WorldObjectAction Action;
        public int Argument;

        public bool IsWellFormed =>
            Source.Value != 0 && Target.Value != 0 &&
            Signal != WorldObjectSignal.None && Action != WorldObjectAction.None;
    }

    public static class WorldObjectIds
    {
        public static WorldObjectId FromDecoration(GeneratedPropId id) => new WorldObjectId(id.Value);

        public static WorldObjectId Create(uint worldSeed, uint parentId, uint localKey)
        {
            uint low = DecorationSeed.Derive(DecorationSeed.Derive(worldSeed, parentId), localKey);
            uint high = DecorationSeed.Derive(low, 0x574F424Au); // WOBJ
            return new WorldObjectId(((ulong)high << 32) | low);
        }
    }
}
