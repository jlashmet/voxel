using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Broadcast message from the server indicating that an alteration was rejected.
    /// Wire format mirrors the S_AlterationEvent structure with a rejection-specific payload.
    /// </summary>
    public struct S_AlterationRejected
    {
        /// <summary>Server tick at which the rejection was issued.</summary>
        public uint Tick;

        /// <summary>The player who submitted the rejected alteration.</summary>
        public ushort PlayerId;

        /// <summary>Sequence number of the rejected event (matches the original submission).</summary>
        public ushort Sequence;

        /// <summary>Human-readable reason code for client-side feedback display (FR-009).</summary>
        public byte ReasonCode;

        /// <summary>Region coordinate of the rejection origin, packed as a single int.</summary>
        public int RegionPacked;

        /// <summary>Voxel-level origin of the rejected alteration, packed as three shorts.</summary>
        public short OriginX, OriginY, OriginZ;
    }

    /// <summary>
    /// Renders rejection feedback: visual dissolve of pending voxels and a player-visible
    /// reason string. This is presentation — it doesn't change world state, but it must
    /// convey the server's decision clearly to the player (FR-009).
    ///
    /// The rejection flow:
    ///   1. Server sends S_AlterationRejected message.
    ///   2. ShowReason() maps ReasonCode to a human-readable string for UI display.
    ///   3. StartDissolveAnimation() triggers the visual dissolve of pending voxels.
    ///   4. The client's SpeculativeOverlay.RejectTick() removes the pending state internally.
    /// </summary>
    public static class RejectionFeedback
    {
        // -- reason code mapping --------------------------------------------------

        /// <summary>Maps server rejection ReasonCode to a player-visible string.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ShowReason(in S_AlterationRejected rejected)
        {
            // Map the server's reason code to a human-readable string for display.
            // These strings are presentation — they don't change world state but must clearly
            /// convey the rejection to the player per FR-009.
            return rejected.ReasonCode switch
            {
                1 => "Placement intersects player volume",       // InPlayerVolume
                2 => "Target out of reach distance",              // OutOfReach
                3 => "Area is protected",                         // ProtectedZone
                4 => "Not attached to existing structure",        // NotAttached
                5 => "Rate limit exceeded",                       // TooFast
                6 => "Region density cap reached",                // OverDensity
                7 => "Allocation budget exceeded",                // OverBudget
                8 => "Invalid target or material",                // InvalidTarget
                _ => "Placement rejected by server",             // Generic fallback.
            };
        }

        /// <summary>Returns the raw reason code for logging and telemetry purposes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetReasonCode(in S_AlterationRejected rejected) => rejected.ReasonCode;

        // -- dissolve animation tracking ------------------------------------------

        /// <summary>Registry of active dissolve animations keyed by region coordinate.</summary>
        private static NativeHashMap<int3, DissolveState> _dissolves;

        /// <summary>Dissolve animation state for a pending region.</summary>
        private struct DissolveState
        {
            public double startTime;   // timestamp when dissolve started.
            public float duration;     // total duration in seconds.
            public float progress;     // 0.0 (start) → 1.0 (complete).
            public int regionKey;      // packed region coordinate for dedup.
        }

        /// <summary>
        /// Start a dissolve animation for rejected pending voxels in the given region.
        /// The animation fades from opaque to transparent over <paramref name="durationSeconds"/>
        /// and triggers overlay cleanup on completion.
        /// </summary>
        /// <param name="startRegion">Region coordinate where pending voxels are being dissolved.</param>
        /// <param name="durationSeconds">Duration of the dissolve animation in seconds (default: 0.3).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartDissolveAnimation(int3 startRegion, float durationSeconds)
        {
            if (!_dissolves.IsCreated)
                _dissolves = new NativeHashMap<int3, DissolveState>(16, Allocator.Persistent);

            // Avoid duplicating an existing dissolve for the same region.
            if (_dissolves.TryGetValue(startRegion, out var existing))
            {
                if (existing.progress < 1.0f)
                    return; // already animating — skip.
            }

            var state = new DissolveState
            {
                startTime = Environment.TickCount / 1000.0,
                duration = durationSeconds > 0f ? durationSeconds : 0.3f,
                progress = 0.0f,
                regionKey = startRegion.x | (startRegion.y << 16),
            };

            _dissolves[startRegion] = state;
        }

        /// <summary>
        /// Update all active dissolve animations and return those that have completed.
        /// Completed dissolves are removed from the registry and their pending entries are cleared.
        /// </summary>
        /// <returns>A NativeList of region coordinates whose dissolve has completed. Caller must Dispose.</returns>
        public static NativeList<int3> UpdateDissolves(out NativeArray<float> alphaLevels)
        {
            var completed = new NativeList<int3>(8, Allocator.Temp);

            if (!_dissolves.IsCreated)
            {
                alphaLevels = default;
                return completed;
            }

            double now = Environment.TickCount / 1000.0;
            var keys = _dissolves.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                int3 regionCoord = keys[i];
                if (!_dissolves.TryGetValue(regionCoord, out var state))
                    continue;

                float elapsed = (float)(now - state.startTime);
                float progress = math.clamp(elapsed / state.duration, 0.0f, 1.0f);

                if (progress >= 1.0f)
                {
                    completed.Add(regionCoord);
                    _dissolves.Remove(regionCoord);
                    continue; // remove after iteration — keys are disposed below.
                }

                state.progress = progress;
                _dissolves[regionCoord] = state;
            }

            keys.Dispose();

            // Pack alpha levels for the render system.
            int activeCount = 0;
            if (_dissolves.IsCreated)
            {
                foreach (var kvp in _dissolves)
                {
                    if (kvp.Value.progress < 1.0f)
                        activeCount++;
                }
            }

            alphaLevels = new NativeArray<float>(activeCount > 0 ? activeCount : 1, Allocator.Temp);

            int alphaIdx = 0;
            if (_dissolves.IsCreated)
            {
                foreach (var kvp in _dissolves)
                {
                    if (kvp.Value.progress < 1.0f)
                    {
                        // Fade from 0.5 to 0.0 over the dissolve duration.
                        alphaLevels[alphaIdx++] = math.lerp(0.5f, 0.0f, kvp.Value.progress);
                    }
                }
            }

            if (alphaIdx == 0)
                alphaLevels[0] = 0.5f; // default pending opacity when no active dissolves.

            return completed;
        }

        /// <summary>Disposes all native resources used by the rejection feedback system.</summary>
        public static void Dispose()
        {
            if (_dissolves.IsCreated)
                _dissolves.Dispose();
            _dissolves = default;
        }
    }
}
