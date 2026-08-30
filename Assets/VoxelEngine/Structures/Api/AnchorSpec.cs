using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Cardinal facing. Integer, so orientation never involves a rotation matrix.</summary>
    public enum Facing : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        Up = 4,
        Down = 5,
    }

    /// <summary>Semantic structural roles. Flags let one reusable piece participate in several recipes.</summary>
    [Flags]
    public enum StructuralSocketRole : uint
    {
        None = 0,
        Traversal = 1u << 0,
        TerrainAnchor = 1u << 1,
        BridgeSpan = 1u << 2,
        Support = 1u << 3,
        Wall = 1u << 4,
        Tower = 1u << 5,
        Gate = 1u << 6,
        Platform = 1u << 7,
        VerticalConnection = 1u << 8,
        Facade = 1u << 9,
        Roof = 1u << 10,
        Building = 1u << 11,
    }

    /// <summary>Behavioral requirements carried by a structural socket.</summary>
    [Flags]
    public enum StructuralSocketFlags : ushort
    {
        None = 0,
        Required = 1 << 0,
        RequireTerrainSupport = 1 << 1,
        RequireStructuralSupport = 1 << 2,
        InvalidateOnSupportLoss = 1 << 3,
        DecorationHandoff = 1 << 4,
    }

    /// <summary>
    /// Engine-neutral decoration handoff. Game.Structures maps these flags to its richer
    /// DecorationSocketKind contract; VoxelEngine.Structures intentionally has no dependency on it.
    /// </summary>
    [Flags]
    public enum StructuralDecorationHandoff : ushort
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

    /// <summary>
    /// Attachment interface exposed by a reusable structural piece. A definition has one ingress
    /// interface and may expose any number of outgoing <see cref="SlotSpec"/> sockets. This keeps
    /// composition a bounded tree while still permitting walls, towers, spans and platforms to be
    /// reused by many recipes.
    /// </summary>
    public struct StructuralPieceSpec
    {
        /// <summary>Stable content-authored identity; zero means the definition is not composable.</summary>
        public uint PieceId;
        public StructuralSocketRole Role;

        /// <summary>
        /// Compatibility is mutual: parent.Offers must intersect child.Accepts and child.Offers
        /// must intersect parent.Accepts. Tags are authored numeric bits, never string conventions.
        /// </summary>
        public ulong Offers;
        public ulong Accepts;

        /// <summary>Child-local point/facing aligned to the parent socket.</summary>
        public int3 LocalPosition;
        public Facing Facing;

        /// <summary>Reserved child-local clearance volume, inclusive min / exclusive max.</summary>
        public int3 ClearanceMin;
        public int3 ClearanceMax;
    }

    /// <summary>
    /// A named point a definition promises to have — a door, a courtyard, a spawn point.
    ///
    /// Anchors are what make an instance addressable to systems that do not care about voxels.
    /// They are derived rather than stored, so asking where a house's door is costs a regeneration
    /// of that candidate, not a lookup in a registry that would have to be kept in sync.
    /// </summary>
    public struct AnchorSpec
    {
        public FixedString32Bytes Name;

        /// <summary>Position in definition-local voxels, before orientation and placement.</summary>
        public int3 LocalPosition;

        public Facing Facing;

        /// <summary>When set, the anchor's height is resolved to the ground rather than the local Y.</summary>
        public bool SnapToGround;
    }

    /// <summary>An anchor resolved to world space for a particular instance.</summary>
    public struct ResolvedAnchor
    {
        public FixedString32Bytes Name;
        public int3 Position;
        public Facing Facing;
    }

    /// <summary>
    /// A typed structural socket where another independently bounded definition may be attached.
    /// The legacy placement box/count/spacing fields remain the deterministic recipe controls;
    /// typed fields make compatibility, orientation, support and downstream handoff explicit.
    /// </summary>
    public struct SlotSpec
    {
        public FixedString32Bytes Name;

        /// <summary>Stable content-authored socket identity within the parent definition.</summary>
        public uint SocketId;
        public StructuralSocketRole Role;
        public ulong Offers;
        public ulong Accepts;
        public int3 LocalPosition;
        public Facing Facing;

        /// <summary>Definition proposed here. Typed compatibility is still validated before use.</summary>
        public int DefinitionId;

        /// <summary>Volume inside the parent footprint where this slot may place children.</summary>
        public int3 LocalMin;
        public int3 LocalMax;

        /// <summary>Reserved socket clearance, relative to LocalPosition.</summary>
        public int3 ClearanceMin;
        public int3 ClearanceMax;

        /// <summary>Instances placed in this slot, drawn per parent instance.</summary>
        public int CountMin;
        public int CountMax;

        /// <summary>Hard attachment capacity. Zero is invalid for a typed socket.</summary>
        public ushort Capacity;

        /// <summary>Minimum spacing between children, in voxels.</summary>
        public int Spacing;

        public StructuralSocketFlags Flags;

        /// <summary>World-space probe box after composition; support is tested before acceptance.</summary>
        public int3 SupportProbeMin;
        public int3 SupportProbeMax;
        public ushort MinimumSupportContacts;

        /// <summary>Optional handoff to the existing fine-detail decoration stage.</summary>
        public StructuralDecorationHandoff DecorationHandoff;
    }

    /// <summary>Pure validation/compatibility helpers shared by authoring and runtime planning.</summary>
    public static class StructuralSocketValidation
    {
        public static bool IsCardinal(Facing facing) => (byte)facing <= (byte)Facing.Down;

        public static Facing Opposite(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return Facing.South;
                case Facing.East: return Facing.West;
                case Facing.South: return Facing.North;
                case Facing.West: return Facing.East;
                case Facing.Up: return Facing.Down;
                case Facing.Down: return Facing.Up;
                default: return facing;
            }
        }

        public static bool Compatible(in SlotSpec parent, in StructuralPieceSpec child)
        {
            if (parent.SocketId == 0 || child.PieceId == 0)
                return false;
            if ((parent.Role & child.Role) == 0)
                return false;
            if ((parent.Offers & child.Accepts) == 0 || (child.Offers & parent.Accepts) == 0)
                return false;
            return Opposite(parent.Facing) == child.Facing;
        }

        public static bool HasValidBounds(int3 min, int3 max) =>
            min.x <= max.x && min.y <= max.y && min.z <= max.z;
    }
}
