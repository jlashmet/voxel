using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Core.Features
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
    /// A place inside a definition where another definition may be attached — the mechanism behind
    /// a castle being a keep, walls, towers, and a gatehouse rather than one enormous program.
    /// </summary>
    public struct SlotSpec
    {
        public FixedString32Bytes Name;

        /// <summary>Definition attached here. Validation proves the slot graph is acyclic.</summary>
        public int DefinitionId;

        /// <summary>Volume inside the parent footprint this slot's children must fit within.</summary>
        public int3 LocalMin;
        public int3 LocalMax;

        /// <summary>Instances placed in this slot, drawn per parent instance.</summary>
        public int CountMin;
        public int CountMax;

        /// <summary>Minimum spacing between children, in voxels.</summary>
        public int Spacing;
    }
}
