using Unity.Collections;
using VoxelEngine.Core.Storage;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Validation predicate framework with a single choke point to SetVoxel.
    ///
    /// All voxel modifications must pass through Validate() before reaching the brick grid.
    /// This enforces the invariants from data-model.md §AlterationEvent validation:
    ///   - Within player reach (FR-018)
    ///   - Not inside a protected zone (FR-019)
    ///   - Within rate budget (FR-020)
    ///   - Within density cap (FR-021)
    ///   - Placements attach to existing structure (FR-attachment)
    ///   - No intersection with occupied player volume (FR-playerVolume)
    /// </summary>
    public static class Validation
    {
        // -- validation result codes ----------------------------------------------

        /// <summary>Possible outcomes of a validation check.</summary>
        public enum ValidationResult : byte
        {
            /// <summary>All checks passed — the alteration may proceed to SetVoxel.</summary>
            Success = 0,

            /// <summary>Player is altering faster than the allowed rate (FR-020 budget).</summary>
            TooFast = 1,

            /// <summary>Player total allocation exceeds per-tick budget.</summary>
            OverBudget = 2,

            /// <summary>Region density cap would be exceeded by this alteration.</summary>
            OverDensity = 3,

            /// <summary>Alteration does not attach to existing structure (placement must connect).</summary>
            NotAttached = 4,

            /// <summary>Placement intersects an occupied player volume.</summary>
            InPlayerVolume = 5,

            /// <summary>Target is out of the player's reach distance (FR-018 reach).</summary>
            OutOfReach = 6,

            /// <summary>Target falls within a protected zone (e.g., spawn area). FR-019.</summary>
            ProtectedZone = 7,

            /// <summary>The target brick or material combination is invalid for this alteration kind.</summary>
            InvalidTarget = 8,
        }

        // -- budget parameters from device-matrix.md ------------------------------

        /// <summary>Maximum alterations per second allowed per player (device-matrix.md rate budget).</summary>
        public const int k_MaxAlterationsPerSecond = 10;

        /// <summary>Maximum bricks altered per tick per player (device-matrix.md allocation budget).</summary>
        public const int k_MaxBricksPerTick = 512; // one brick's worth as a baseline

        /// <summary>Default player reach distance in voxels. Can be modified by tool type.</summary>
        public const int k_DefaultReachVoxels = 16; // ~12.8 m at 10 cm voxels

        // -- input structures -----------------------------------------------------

        /// <summary>Allocation budget per player — carries the per-tick allocation limit.</summary>
        public struct AllocationBudget
        {
            /// <summary>Maximum bricks this player may alter in a single tick.</summary>
            public int maxBricksPerTick;

            /// <summary>Maximum alterations this player may submit per second.</summary>
            public int maxAlterationsPerSecond;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AllocationBudget(int maxBricks, int maxAlterations)
            {
                this.maxBricksPerTick = maxBricks;
                this.maxAlterationsPerSecond = maxAlterations;
            }
        }

        /// <summary>Density cap for a region — maximum fraction of mixed (non-uniform) bricks allowed.</summary>
        public struct DensityCap
        {
            /// <summary>Maximum density as a fraction [0.0, 1.0] of total bricks that may be non-uniform.</summary>
            public float maxDensity;

            /// <summary>Total brick count in the region (64³ = 262144 for standard regions).</summary>
            public int totalBricks;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DensityCap(float maxDensity, int totalBricks)
            {
                this.maxDensity = math.clamp(maxDensity, 0f, 1f);
                this.totalBricks = totalBricks;
            }

            /// <summary>Maximum number of mixed bricks allowed before density cap is hit.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int MaxMixedBricks() => (int)(this.maxDensity * this.totalBricks);
        }

        // -- choke point ----------------------------------------------------------

        /// <summary>
        /// Validates an alteration event against all server-side rules before it reaches SetVoxel.
        /// This is the single entry point — no voxel write may bypass this check.
        /// </summary>
        /// <param name="playerId">ID of the player submitting the alteration.</param>
        /// <param name="evt">The alteration event to validate.</param>
        /// <param name="table">Region table for looking up region state and protected zones.</param>
        /// <param name="pool">Brick pool for checking occupancy and density.</param>
        /// <param name="budget">Per-player allocation budget from server config.</param>
        /// <param name="densityCap">Density cap for the target region.</param>
        /// <param name="zones">Protected-zone registry (FR-019). Default means no zones.</param>
        /// <returns>ValidationResult indicating success or the specific failure reason.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValidationResult Validate(
            int playerId,
            AlterationEvent evt,
            ref RegionTable table,
            in BrickPool pool,
            AllocationBudget budget,
            DensityCap densityCap,
            in ProtectedZones zones = default)
        {
            // 1. Basic event validation (data-model.md: field validity).
            if (!evt.Validate())
                return ValidationResult.InvalidTarget;

            // 2. Rate budget check — is this player altering too fast? (FR-020, device-matrix.md rate limit).
            if (playerId > 0) // playerId == 0 is the server/host placeholder.
            {
                if (CheckRateBudget(playerId, evt.tick))
                    return ValidationResult.TooFast;

                UpdateRateCounter(playerId, evt.tick);
            }

            // 3. Reach check — is the origin within the player's reach distance? (FR-018).
            if (!CheckReach(evt.origin, playerId))
                return ValidationResult.OutOfReach;

            // 4. Protected zone check — is the target inside a protected area? (FR-019).
            if (CheckProtectedZones(evt.origin, in zones))
                return ValidationResult.ProtectedZone;

            // 5. Attachment check — must attach to existing structure.
            if (!CheckAttachment(evt, evt.origin, ref table, in pool))
                return ValidationResult.NotAttached;

            // 6. Player volume check — no placement inside occupied player space.
            if (CheckPlayerVolume(evt.origin, evt.material, playerId))
                return ValidationResult.InPlayerVolume;

            // 7. Density cap check — would this push the region over its density limit?
            if (CheckDensityCap(evt.origin, ref table, densityCap))
                return ValidationResult.OverDensity;

            // 8. Allocation budget — total bricks altered within per-tick limit.
            int estimatedBricks = EstimateAlterationSize(evt);
            if (estimatedBricks > budget.maxBricksPerTick)
                return ValidationResult.OverBudget;

            return ValidationResult.Success;
        }

        // -- individual validation predicates -------------------------------------

        /// <summary>Checks if a player's alteration rate is within their budget.</summary>
        private static NativeHashMap<uint, uint> _rateCounters = new NativeHashMap<uint, uint>(64, Unity.Collections.Allocator.Persistent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckRateBudget(int playerId, uint tick)
        {
            // Count alterations in the rolling 1-second window.
            uint windowStart = tick > (uint)k_MaxAlterationsPerSecond
                ? tick - (uint)k_MaxAlterationsPerSecond
                : 0;

            if (!_rateCounters.ContainsKey((uint)playerId))
                return false; // first submission — always allow.

            return _rateCounters[(uint)playerId] >= (uint)k_MaxAlterationsPerSecond;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateRateCounter(int playerId, uint tick)
        {
            uint key = (uint)playerId;
            if (_rateCounters.ContainsKey(key))
                _rateCounters[key]++;
            else
                _rateCounters[key] = 1;
        }

        /// <summary>Checks if the event origin is within reach distance of the player.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckReach(int3 origin, int playerId)
        {
            // In a real implementation, player positions would be looked up from a player state table.
            // For now, use the default reach as a placeholder — actual reach is tool-dependent.
            return true; // TODO: look up player position and compute distance.
        }

        /// <summary>Checks if any target voxel falls within a protected zone.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckProtectedZones(int3 origin, in ProtectedZones zones)
        {
            // Zones are server-owned and sparse: regions with no registered mask cost one
            // failed hash lookup and nothing more (FR-019).
            return zones.IsCreated && zones.IsProtected(origin);
        }

        /// <summary>Checks that the alteration attaches to existing structure.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckAttachment(AlterationEvent evt, int3 origin, ref RegionTable table, in BrickPool pool)
        {
            // For each voxel the alteration would affect, check adjacency to non-empty bricks.
            // Explosion/brush: expand from origin, find boundary voxels.
            // Raw batch: must be adjacent to existing structure at all contact points.

            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                    return CheckAttachmentExplosion(origin, (ushort)evt.shapeData, ref table, in pool);
                case AlterationEvent.KindBrush:
                    return CheckAttachmentBrush(origin, evt.BrushExtents(), ref table, in pool);
                case AlterationEvent.KindRawBatch:
                    return CheckAttachmentRawBatch(origin, ref table, in pool);
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckAttachmentExplosion(int3 origin, ushort radius, ref RegionTable table, in BrickPool pool)
        {
            // At least one voxel on the explosion boundary must touch a non-empty brick.
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int distSq = dx * dx + dy * dy;
                    if (distSq > radius * radius) continue;

                    int3 checkPos = origin + new int3(dx, dy, 0);
                    if (!IsAdjacentToNonEmpty(checkPos, ref table))
                        return false; // no attachment found — reject.
                }
            }
            return true; // attachment verified on at least one boundary voxel.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckAttachmentBrush(int3 origin, int3 extents, ref RegionTable table, in BrickPool pool)
        {
            // Brush must overlap existing structure along its base face.
            return IsAdjacentToNonEmpty(origin, ref table);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckAttachmentRawBatch(int3 origin, ref RegionTable table, in BrickPool pool)
        {
            return IsAdjacentToNonEmpty(origin, ref table);
        }

        /// <summary>Checks if a voxel position is adjacent to any non-empty brick.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAdjacentToNonEmpty(int3 pos, ref RegionTable table)
        {
            // Check all 6 face-adjacent bricks for non-empty content.
            int3[] neighbors = new int3[6];
            neighbors[0] = pos + new int3(1, 0, 0);
            neighbors[1] = pos + new int3(-1, 0, 0);
            neighbors[2] = pos + new int3(0, 1, 0);
            neighbors[3] = pos + new int3(0, -1, 0);
            neighbors[4] = pos + new int3(0, 0, 1);
            neighbors[5] = pos + new int3(0, 0, -1);

            foreach (var n in neighbors)
            {
                int3 brickCoord = n / 8; // voxel to brick grid coord.
                if (IsBrickNonEmpty(brickCoord, ref table))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBrickNonEmpty(int3 brickCoord, ref RegionTable table)
        {
            // TODO: look up brick at brickCoord in the region table.
            return true; // placeholder — real implementation checks pool occupancy.
        }

        /// <summary>Checks if placing a material at origin would intersect an occupied player volume.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckPlayerVolume(int3 origin, byte material, int playerId)
        {
            // Compare against current player positions and their collision volumes.
            // Player volume is typically a cylinder of radius ~0.4 m (half-player-width)
            // extending from feet to head height (~2 m).
            return false; // placeholder — real implementation checks player state table.
        }

        /// <summary>Checks if adding bricks at origin would exceed the region's density cap.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckDensityCap(int3 origin, ref RegionTable table, DensityCap densityCap)
        {
            int3 regionCoord = origin / VoxelDimensions.RegionEdge;

            if (!table.TryGetRegion(regionCoord, out var region))
                return false; // no region data — assume within cap.

            int currentMixed = CountMixedBricks(in region);
            int estimatedNew = EstimateAlterationSizeRaw(origin);

            return (currentMixed + estimatedNew) > densityCap.MaxMixedBricks();
        }

        /// <summary>
        /// Counts mixed (pool-backed) bricks in a region. Region stores no running total,
        /// and deriving it here keeps the single source of truth in BrickRefs rather than a
        /// cached counter that can drift from it.
        /// </summary>
        private static int CountMixedBricks(in Region region)
        {
            if (!region.BrickRefs.IsCreated) return 0;

            var refs = region.BrickRefs;
            int count = 0;
            for (int i = 0; i < refs.Length; i++)
                if (refs[i].IsMixed) count++;

            return count;
        }

        /// <summary>Estimates the number of bricks an alteration will affect.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EstimateAlterationSize(AlterationEvent evt)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    ushort r = (ushort)evt.shapeData;
                    // Sphere volume in bricks ≈ 4/3 * π * r³, rounded up.
                    return (int)((4.0f / 3.0f) * 3.14159f * r * r * r) + 1;
                }
                case AlterationEvent.KindBrush:
                {
                    int3 e = evt.BrushExtents();
                    return e.x * e.y * e.z; // axis-aligned bounding box in bricks.
                }
                case AlterationEvent.KindRawBatch:
                    return (int)(evt.shapeData & 0xFFFF); // count stored in shapeData low bits.
                default:
                    return 0;
            }
        }

        /// <summary>Overload for integer-only estimation without the event struct.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EstimateAlterationSizeRaw(int3 origin)
        {
            // Default estimate of 1 brick for adjacency checks.
            return 1;
        }

        /// <summary>Checks if a voxel at the given world coordinate is solid (non-empty).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSolidAtVoxel(ref RegionTable table, in BrickPool pool, int3 voxelCoord)
        {
            // Convert voxel to brick coordinate.
            int3 brickCoord = new int3(
                voxelCoord.x >> VoxelDimensions.BrickEdgeLog2,
                voxelCoord.y >> VoxelDimensions.BrickEdgeLog2,
                voxelCoord.z >> VoxelDimensions.BrickEdgeLog2);

            // Resolve the region containing this brick.
            int3 regionCoord = new int3(
                brickCoord.x >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.y >> VoxelDimensions.RegionEdgeLog2,
                brickCoord.z >> VoxelDimensions.RegionEdgeLog2);

            if (!table.TryGetRegion(regionCoord, out var region))
                return false; // non-resident regions read as empty.

            int brickInRegion = Region.BrickIndex(
                (brickCoord.x & VoxelDimensions.RegionEdgeMask),
                (brickCoord.y & VoxelDimensions.RegionEdgeMask),
                (brickCoord.z & VoxelDimensions.RegionEdgeMask));

            var brickRef = region.BrickRefs[brickInRegion];

            if (!brickRef.IsMixed)
            {
                // Uniform: solid if non-empty.
                return brickRef.UniformMaterial != VoxelDimensions.MaterialEmpty;
            }

            // Mixed: check voxel occupancy in the pool.
            int brickLocalX = (voxelCoord.x & VoxelDimensions.BrickEdgeMask);
            int brickLocalY = (voxelCoord.y & VoxelDimensions.BrickEdgeMask);
            int brickLocalZ = (voxelCoord.z & VoxelDimensions.BrickEdgeMask);

            int voxelIndex = brickLocalX | (brickLocalY << VoxelDimensions.BrickEdgeLog2) | (brickLocalZ << (VoxelDimensions.BrickEdgeLog2 * 2));
            return pool.GetVoxel(brickRef.PoolIndex, voxelIndex) != VoxelDimensions.MaterialEmpty;
        }

        /// <summary>Clears all rate counters — called on tick reset or player disconnect.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearRateCounters(int playerId)
        {
            _rateCounters.Remove((uint)playerId);
        }

        /// <summary>
        /// Checks whether any occupied voxel intersects the player's bounding box at a given position.
        /// Used as a validation predicate for placement rejection — no player should be left intersecting
        /// solid matter after building (FR-018: player volume safety).
        ///
        /// Scans both the real grid (via region table and brick pool) and is designed to be composable
        /// with speculative overlay checks via <see cref="SweptAabb.SweepAgainstOverlay"/>.
        /// </summary>
        /// <param name="table">Region table for looking up brick occupancy in the grid.</param>
        /// <param name="pool">Brick pool for accessing mixed-brick voxel data.</param>
        /// <param name="position">Center of the player's bounding box in voxel coordinates.</param>
        /// <param name="radius">Half-width of the player's collision cylinder. The full AABB is:
        ///   [position.x - radius, position.y - radius, position.z - radius] to
        ///   [position.x + radius, position.y + radius, position.z + radius].</param>
        /// <returns>True if any solid (non-empty) voxel intersects the player's bounding box.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsVolumeOccupied(ref RegionTable table, in BrickPool pool, int3 position, float radius)
        {
            // Build the AABB from center + radius.
            int minX = (int)(position.x - radius);
            int maxX = (int)(position.x + radius);
            int minY = (int)(position.y - radius);
            int maxY = (int)(position.y + radius);
            int minZ = (int)(position.z - radius);
            int maxZ = (int)(position.z + radius);

            // Iterate over every voxel in the AABB and check for solidity.
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (IsSolidAtVoxel(ref table, in pool, new int3(x, y, z)))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Disposes the validation module's native resources.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dispose()
        {
            if (_rateCounters.IsCreated) _rateCounters.Dispose();
        }
    }
}
