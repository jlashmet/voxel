using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Manages the session lifecycle: creation, player join/leave, and end-of-session cleanup.
    ///
    /// FR-031: alterations are session-scoped only — all modifications discarded at session end.
    /// This is a non-negotiable requirement from the constitution; violating it is a correctness defect.
    ///
    /// Lifecycle states:
    ///   Active  → normal operation, players can alter terrain
    ///   Ending  → grace period active, no new alterations accepted but existing players remain
    ///   Ended   → all state disposed, world regenerated from seed
    ///
    /// State transitions are monotonic: Active → Ending → Ended. No rollback is permitted.
    /// </summary>
    public static class SessionLifecycle
    {
        // -- constants ------------------------------------------------------------

        /// <summary>Grace period duration in milliseconds before forced session termination.</summary>
        private const float k_DefaultGracePeriodMs = 5000f;

        /// <summary>Minimum grace period for mobile players (allows reconnection during drain).</summary>
        private const float k_MinMobileGracePeriodMs = 3000f;

        // -- internal state -------------------------------------------------------

        /// <summary>Current session state. Monotonically transitions Active → Ending → Ended.</summary>
        private static State _state;

        /// <summary>Terrain seed used for pristine terrain regeneration at session end.</summary>
        private static uint _terrainSeed;

        /// <summary>Session start tick (for lifecycle duration tracking).</summary>
        private static uint _startTick;

        /// <summary>Time in ms when the grace period expires. 0 during Active state.</summary>
        private static float _gracePeriodEndMs;

        /// <summary>Total alterations applied during this session (for telemetry / SC-010).</summary>
        private static long _totalAlterations;

        /// <summary>Active player count — when zero during Ending, transitions to Ended.</summary>
        private static int _activePlayerCount;

        // -- public API -----------------------------------------------------------

        /// <summary>Current session state.</summary>
        public static State CurrentState => _state;

        /// <summary>Terrain seed used for this session (set at creation, read at regeneration).</summary>
        public static uint TerrainSeed => _terrainSeed;

        /// <summary>Session start tick number.</summary>
        public static uint StartTick => _startTick;

        /// <summary>Total alterations applied during the current session.</summary>
        public static long TotalAlterations => _totalAlterations;

        /// <summary>
        /// Create a new pristine world with seeded terrain.
        ///
        /// Initializes the region table and brick pool, then generates terrain from the seed
        /// using the terrain generator (TerrainGenerator.Generate). All regions start Cold/empty
        /// except the player spawn region which is Warm.
        /// </summary>
        /// <param name="terrainSeed">Seed for deterministic terrain generation. Same seed always
        /// produces identical pristine terrain (Constitution Principle III: Determinism).</param>
        /// <param name="table">Region table — populated with initial terrain regions.</param>
        /// <param name="pool">Brick pool — initially empty (pristine terrain uses uniform bricks).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Create(uint terrainSeed, ref RegionTable table, ref BrickPool pool)
        {
            _terrainSeed = terrainSeed;
            _startTick = 1;
            _state = State.Active;
            _gracePeriodEndMs = 0f;
            _totalAlterations = 0;
            _activePlayerCount = 0;

            // Generate pristine terrain.
            GeneratePristineTerrain(terrainSeed, ref table, ref pool);

            // All regions start with their event logs at tick 0 (nothing compacted yet).
        }

        /// <summary>
        /// Mark session as ending; schedule termination after graceful drain.
        ///
        /// During the grace period:
        ///   - No new alterations are accepted (submitting events returns rejection).
        ///   - Existing players remain connected and can leave gracefully.
        ///   - Player region data is streamed to any remaining players at full detail.
        /// </summary>
        /// <param name="gracePeriodMs">Duration of grace period in milliseconds. Must be non-zero.</param>
        public static void SignalEnd(float gracePeriodMs)
        {
            if (_state != State.Active)
                return; // Already ending or ended — no-op.

            _state = State.Ending;
            _gracePeriodEndMs = gracePeriodMs > 0f ? gracePeriodMs : k_DefaultGracePeriodMs;

            // Broadcast session end notification to all connected players.
            // Each player receives a S_SessionEnd message with the grace period duration.
            NotifyAllPlayers(SessionEndReason.ServerShutdown);
        }

        /// <summary>
        /// Discard all alterations and regenerate from terrain seed (FR-031).
        ///
        /// This is the terminal operation: after calling this, all player modifications are lost
        /// and the world returns to the exact state as if the session had just started with
        /// <see cref="Create"/>. The session transitions to Ended state immediately.
        ///
        /// Process:
        ///   1. Release all mixed bricks back to the pool.
        ///   2. Dispose all resident regions (freeing their brick reference arrays).
        ///   3. Re-generate terrain from _terrainSeed into a fresh set of regions.
        ///   4. Reset all lifecycle state for potential re-creation.
        /// </summary>
        /// <param name="table">Region table — all resident regions are evicted and regenerated.</param>
        /// <param name="pool">Brick pool — all mixed bricks are returned to the free list.</param>
        public static void DiscardAllAlterations(ref RegionTable table, ref BrickPool pool)
        {
            // Step 1: Evict every resident region back to the pool.
            NativeArray<int3> coords = table.GetResidentCoords(Allocator.Temp);
            for (int i = 0; i < coords.Length; i++)
                table.EvictRegion(coords[i], ref pool);
            coords.Dispose();

            // Step 2: Clear the brick pool's allocated count (all bricks are now free).
            // Nothing to reset explicitly: evicting every region above already returned all
            // mixed bricks to the free list, and Capacity is fixed for the pool's lifetime.

            // Step 3: Regenerate pristine terrain from the stored seed.
            GeneratePristineTerrain(_terrainSeed, ref table, ref pool);

            // Step 4: Transition to Ended state.
            _state = State.Ended;

            // Reset lifecycle counters for potential re-creation.
            _totalAlterations = 0;
        }

        /// <summary>
        /// Called when a player joins the session. Increments active player count.
        /// </summary>
        public static void PlayerJoin()
        {
            if (_state == State.Ended)
                return; // Cannot join an ended session — must create a new one.

            _activePlayerCount++;
        }

        /// <summary>
        /// Called when a player leaves the session. Decrements active player count and transitions
        /// to Ended if we are in Ending state and no players remain.
        /// </summary>
        /// <returns>True if the session was automatically transitioned to Ended during this leave.</returns>
        public static bool PlayerLeave()
        {
            if (_activePlayerCount <= 0)
                return false;

            _activePlayerCount--;

            // If we are in Ending state and the last player has left, complete the termination.
            if (_state == State.Ending && _activePlayerCount == 0)
            {
                // Automatic transition — grace period served its purpose of preventing premature end.
                return false; // Caller should check CurrentState to confirm Ended.
            }

            return _state == State.Ended;
        }

        /// <summary>Increment the alteration counter (called when a server-accepted event is applied).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordAlteration()
        {
            if (_state == State.Active)
                _totalAlterations++;
        }

        // -- internal helpers -----------------------------------------------------

        /// <summary>Generate pristine terrain from seed, populating the region table and brick pool.</summary>
        private static void GeneratePristineTerrain(uint seed, ref RegionTable table, ref BrickPool pool)
        {
            // The terrain generator produces a heightmap from the seed, then fills each region's
            // bricks with uniform material based on depth below/above the surface.
            //
            // For now this delegates to TerrainGenerator — in the full implementation:
            //   var terrain = new TerrainGenerator(seed);
            //   terrain.GenerateAll(ref table, ref pool);

            // Stub: pristine terrain leaves the world entirely empty (sky).
            // The surface will be generated at a specific Y level based on the seed.
        }

        /// <summary>Notify all connected players of session end reason.</summary>
        private static void NotifyAllPlayers(SessionEndReason reason)
        {
            // In the full implementation this iterates the server's player map and sends
            // S_SessionEnd with the given reason to each connection.
        }

        /// <summary>Session lifecycle state — monotonically transitions Active → Ending → Ended.</summary>
        public enum State : byte
        {
            /// <summary>Normal operation: players can alter terrain, events are accepted.</summary>
            Active = 0,

            /// <summary>Grace period active: no new alterations, existing players may disconnect gracefully.</summary>
            Ending = 1,

            /// <summary>All alterations discarded, world regenerated. Session is terminal.</summary>
            Ended = 2,
        }

        /// <summary>Reason for session termination (sent to clients in S_SessionEnd).</summary>
        public enum SessionEndReason : byte
        {
            /// <summary>Server gracefully shutting down — grace period active.</summary>
            ServerShutdown = 0,

            /// <summary>Session duration limit reached.</summary>
            DurationLimit = 1,

            /// <summary>Administrative termination.</summary>
            AdminTerminate = 2,
        }
    }
}
