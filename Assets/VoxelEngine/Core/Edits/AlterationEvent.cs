using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// The unit of replication, the moderation record, and the rollback substrate: a compact
    /// struct that encodes the *cause* of a voxel change, not the effect.
    ///
    /// Packed to approximately 32 bytes on the wire — one AlterationEvent expands deterministically
    /// to thousands of voxel writes via <see cref="ExplosionExpansion"/>, <see cref="BrushExpansion"/>,
    /// or <see cref="RawBatchExpansion"/>. This is what makes SC-002 (server-authoritative edits at
    /// mobile bandwidth) achievable and what makes 64 players fit a constrained connection.
    ///
    /// Layout on the wire (little-endian):
    ///   byte  0  — kind
    ///   uint  1  — tick
    ///   int3  5  — origin (voxel coordinate)
    ///   ~8 B    17 — shape union (radius, extents, or prefab ID)
    ///   byte  ~25 — material
    ///   uint  ~26 — seed
    ///   ushort ~30 — playerId
    ///   ushort ~32 — sequence
    /// Total: 32 bytes (padded to nearest multiple of 4 for wire alignment).
    ///
    /// See data-model.md §AlterationEvent for the authoritative field table.
    /// </summary>
    [Serializable]
    public struct AlterationEvent : IEquatable<AlterationEvent>
    {
        // -- fields ---------------------------------------------------------------

        /// <summary>Kind of alteration: explosion, brush, or raw-batch.</summary>
        public byte kind;

        /// <summary>Server tick when this event was authored. Used for ordering and rollback.</summary>
        public uint tick;

        /// <summary>Origin voxel coordinate — center of the effect volume.</summary>
        public int3 origin;

        /// <summary>
        /// Union: interpreted based on <see cref="kind"/>.
        ///   Explosion → ushort radius, ushort padding (8 bytes total)
        ///   Brush     → int4 extents.xzyw + rotation axis-angle (8 bytes via packed form)
        ///   RawBatch  → uint prefabId + ushort pad1 + ushort pad2
        /// Packed to 8 bytes using shapeKind discriminator.
        /// </summary>
        public uint shapeKind;

        /// <summary>
        /// Additional shape data: union with shapeKind. See <see cref="shapeKind"/> notes above.
        /// Stored as a second uint for 8-byte total per shape field pair.
        /// </summary>
        public uint shapeData;

        /// <summary>Placement material index (into the session's material palette).</summary>
        public byte material;

        /// <summary>
        /// Seed for deterministic expansion via <see cref="DeterministicRandom"/>.
        /// Same seed + same origin always produces identical voxel writes — required by
        /// Constitution Principle III (Determinism).
        /// </summary>
        public uint seed;

        /// <summary>Player who authored this event. Used for attribution and arbitration (FR-020).</summary>
        public ushort playerId;

        /// <summary>
        /// Ordinal within the server tick, completing the total order for arbitration
        /// (data-model.md: Arbitration). The server assigns this — clients never re-derive it.
        /// </summary>
        public ushort sequence;

        // -- constants ------------------------------------------------------------

        /// <summary>Explosion alteration kind.</summary>
        public const byte KindExplosion = 1;

        /// <summary>Brush alteration kind.</summary>
        public const byte KindBrush = 2;

        /// <summary>Raw-batch alteration kind (run-length-encoded voxel placement).</summary>
        public const byte KindRawBatch = 3;

        /// <summary>Maximum valid material index. The palette is bounded by this value.</summary>
        public const byte MaxMaterialIndex = 254; // 255 reserved for internal use.

        // -- constructors ---------------------------------------------------------

        /// <summary>
        /// Construct an AlterationEvent with all fields explicitly set.
        /// Caller must ensure validity; use <see cref="Validate"/> to check before submission.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AlterationEvent(byte kind, uint tick, int3 origin, ushort shapeRadius,
            byte material, uint seed, ushort playerId, ushort sequence)
        {
            this.kind = kind;
            this.tick = tick;
            this.origin = origin;
            this.shapeKind = (uint)kind; // discriminator for shape interpretation
            this.shapeData = (uint)shapeRadius; // explosion: radius in bricks
            this.material = material;
            this.seed = seed;
            this.playerId = playerId;
            this.sequence = sequence;
        }

        // -- validation -----------------------------------------------------------

        /// <summary>
        /// Validate that all fields are within bounds expected by the server.
        /// Used during acceptance (FR-018 to FR-021, FR-032) before the event is committed
        /// to a RegionEventLog or replicated to other clients.
        /// </summary>
        /// <returns>True if all fields are valid; false otherwise. The caller should surface
        /// the specific failure reason to the client.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Validate()
        {
            // Kind must be one of the defined alteration types.
            if (kind < KindExplosion || kind > KindRawBatch) return false;

            // Tick must be non-zero (zero is the "null tick" sentinel).
            if (tick == 0) return false;

            // Material must be a valid palette index (0 = empty, 1..254 = materials).
            if (material > MaxMaterialIndex) return false;

            // playerId: player IDs start at 1 (0 is the server/host placeholder).
            if (playerId == 0) return false;

            // Shape interpretation depends on kind.
            switch (kind)
            {
                case KindExplosion:
                    // Radius must be 1..63 bricks (region edge minus margin).
                    if (shapeData < 1 || shapeData >= (uint)VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge)
                        return false;
                    break;

                case KindBrush:
                    // Brush extents encoded as int4 in shapeKind + shapeData.
                    // Minimum extent is 1 brick; maximum is RegionEdge bricks per axis.
                    if (!ValidateBrushShape()) return false;
                    break;

                case KindRawBatch:
                    // Raw batch shapeData encodes the prefab ID (ushort) and count fields.
                    if (shapeKind == 0 && shapeData == 0) return false; // trivially empty
                    break;
            }

            return true;
        }

        /// <summary>Validate brush-specific shape fields.</summary>
        private bool ValidateBrushShape()
        {
            // Brush shape is encoded across shapeKind (extents.x, extents.y) and
            // shapeData (extents.z, rotation packed). Minimum extent is 1.
            int ex = (int)((shapeKind >> 0) & 0xFFFF);
            int ey = (int)((shapeKind >> 16) & 0xFFFF);
            int ez = (int)(shapeData & 0xFFFF);

            if (ex < 1 || ey < 1 || ez < 1) return false;
            if (ex > VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge) return false;
            if (ey > VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge) return false;
            if (ez > VoxelEngine.Core.Storage.VoxelDimensions.RegionEdge) return false;

            return true;
        }

        /// <summary>
        /// Validation for wire-format compatibility. Checks that the struct can be safely
        /// serialized to and from the protocol buffer without data loss.
        /// </summary>
        public bool ValidateWireFormat()
        {
            // Wire format is 32 bytes with specific field layouts — validate nothing overflows.
            if (tick == 0) return false;
            if ((uint)kind > KindRawBatch) return false;
            if (material > MaxMaterialIndex) return false;
            if (playerId == 0) return false;
            return true;
        }

        // -- shape accessors ------------------------------------------------------

        /// <summary>Explosion radius in bricks. Only valid when <see cref="kind"/> is <see cref="KindExplosion"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Radius() => kind == KindExplosion ? (ushort)shapeData : (ushort)0;

        /// <summary>Brush extents as a 3D vector. Only valid when <see cref="kind"/> is <see cref="KindBrush"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int3 BrushExtents()
        {
            if (kind != KindBrush) return int3.zero;
            return new int3(
                (int)((shapeKind >> 0) & 0xFFFF),
                (int)((shapeKind >> 16) & 0xFFFF),
                (int)(shapeData & 0xFFFF));
        }

        /// <summary>Raw batch prefab ID. Only valid when <see cref="kind"/> is <see cref="KindRawBatch"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort RawPrefabId() => kind == KindRawBatch ? (ushort)(shapeData & 0xFFFF) : (ushort)0;

        /// <summary>Serialization size in bytes on the wire (always 32, padded to word boundary).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WireSize() => 32;

        // -- equality ------------------------------------------------------------

        public bool Equals(AlterationEvent other) =>
            kind == other.kind && tick == other.tick && math.all(origin == other.origin) &&
            shapeKind == other.shapeKind && shapeData == other.shapeData &&
            material == other.material && seed == other.seed &&
            playerId == other.playerId && sequence == other.sequence;

        public override bool Equals(object obj) => obj is AlterationEvent o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                var h = kind.GetHashCode();
                h = (h * 397) ^ tick.GetHashCode();
                h = (h * 397) ^ origin.GetHashCode();
                h = (h * 397) ^ shapeKind.GetHashCode();
                h = (h * 397) ^ shapeData.GetHashCode();
                h = (h * 397) ^ material;
                h = (h * 397) ^ seed.GetHashCode();
                h = (h * 397) ^ playerId.GetHashCode();
                h = (h * 397) ^ sequence.GetHashCode();
                return h;
            }
        }

        public static bool operator ==(AlterationEvent a, AlterationEvent b) => a.Equals(b);
        public static bool operator !=(AlterationEvent a, AlterationEvent b) => !a.Equals(b);

        public override string ToString() =>
            kind switch
            {
                KindExplosion => $"Explosion(t={tick}, o={origin}, r={Radius()}, p={playerId})",
                KindBrush => $"Brush(t={tick}, o={origin}, e={BrushExtents()}, p={playerId})",
                KindRawBatch => $"RawBatch(t={tick}, o={origin}, p={playerId})",
                _ => $"Unknown(k={kind}, t={tick}, p={playerId})",
            };
    }

    /// <summary>
    /// Kind discriminator for broadcast events and wire-format interpretation.
    /// Mirrors the kind field values on <see cref="AlterationEvent"/>.
    /// </summary>
    public enum AlterationEventKind : byte
    {
        /// <summary>No-op event.</summary>
        None = 0,

        /// <summary>Destructive explosion — removes voxels from the grid.</summary>
        Explosion = AlterationEvent.KindExplosion,

        /// <summary>Constructive brush — adds voxels to the grid.</summary>
        Brush = AlterationEvent.KindBrush,

        /// <summary>Raw batch — pre-computed voxel writes.</summary>
        RawBatch = AlterationEvent.KindRawBatch,
    }
}
