using VoxelEngine.Tiering;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Debris
{
    /// <summary>
    /// Debris body — a cluster of bricks that fell due to structural failure.
    ///
    /// Key distinction: visual-only debris may be culled per device tier (Constitution
    /// Principle IV / C-006). Debris that settles and rejoins the grid changes world state
    /// and MAY NOT be culled. Conflating the two is a divergence bug — hence an explicit
    /// flag rather than a convention.
    ///
    /// See data-model.md §DebrisBody for the authoritative field list.
    /// </summary>
    public struct DebrisBody
    {
        /// <summary>Pooled brick reference for the debris body's shape (null when visual-only).</summary>
        public int BrickRef;

        /// <summary>World-space position (presentation-only, float acceptable per data-model.md).</summary>
        public float3 Position;

        /// <summary>Orientation (presentation). Stored as quaternion for direct use in transforms.</summary>
        public quaternion Orientation;

        /// <summary>Linear velocity for physics integration.</summary>
        public float3 Velocity;

        /// <summary>True when debris has settled and should be re-baked into the grid.</summary>
        public bool Settled;

        /// <summary>Load-bearing: visual-only debris may be culled per device tier. State-changing debris may not.</summary>
        public bool VisualOnly;

        /// <summary>Time since last collision (used to trigger settle).</summary>
        public float TimeSinceCollision;

        /// <summary>Bounding sphere radius in world units, computed once at allocation.</summary>
        public float Radius;

        /// <summary>World-space gravity direction (server: -1 on Y axis; presentation may differ).</summary>
        public static readonly float3 Gravity = new float3(0f, -9.81f, 0f);

        /// <summary>Minimum time to settle after last collision (seconds).</summary>
        public const float SettleDelay = 2.0f;

        /// <summary>Debris body pool capacity — bounded by device tier budget.</summary>
        public const int MaxDebrisBodies = 4096;

    }

    /// <summary>
    /// Pool of debris bodies with slot management. Bounded by tier-specific limits.
    ///
    /// The pool is flat: allocation scans the inUse bitmap for the first free bit, O(N)
    /// but N <= MaxDebrisBodies so this is a fast cache-friendly linear scan. Free-list
    /// reuse after settle avoids allocation churn under repeated collapse events.
    /// </summary>
    public static class DebrisBodyPool
    {
        private static NativeArray<bool> _inUse;
        private static NativeList<DebrisBody> _bodies;
        private static int _capacity;
        private static readonly int s_bitsPerWord = sizeof(int) * 8;

        /// <summary>Create the pool with a tier-bounded capacity.</summary>
        public static void Create(DeviceTier tier)
        {
            // Tier budget determines MaxDebris (data-model.md: presentation parameter, C-006).
            int maxDebris = tier switch
            {
                DeviceTier.PC => 2000,
                DeviceTier.Console => 1500,
                DeviceTier.MobileHE => 400,
                _ => 4096 // Default to absolute cap.
            };

            // Clamp to the hard upper bound.
            maxDebris = math.min(maxDebris, DebrisBody.MaxDebrisBodies);
            _capacity = maxDebris;

            var wordCount = (maxDebris + s_bitsPerWord - 1) / s_bitsPerWord;
            _inUse = new NativeArray<bool>(maxDebris, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bodies = new NativeList<DebrisBody>(maxDebris, Allocator.Persistent);
            _bodies.Resize(maxDebris, NativeArrayOptions.ClearMemory);
        }

        /// <summary>Allocate a new debris body slot, returning its index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Allocate(DebrisBody body)
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (!_inUse[i])
                {
                    _inUse[i] = true;
                    _bodies[i] = body;
                    return i;
                }
            }

            throw new InvalidOperationException(
                $"DebrisBodyPool exhausted at capacity {_capacity}. A debris leak is in progress.");
        }

        /// <summary>Return a debris body slot to the pool when it settles or expires.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(int index)
        {
            if (index < 0 || index >= _capacity)
                throw new ArgumentOutOfRangeException(nameof(index));

            _inUse[index] = false;
            _bodies[index] = default;
        }

        /// <summary>Get the debris body at a given slot index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DebrisBody Get(int index)
        {
            if (index < 0 || index >= _capacity)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _bodies.ElementAt(index);
        }

        /// <summary>True when no debris bodies are active.</summary>
        public static bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                for (int i = 0; i < _capacity; i++)
                    if (_inUse[i]) return false;
                return true;
            }
        }

        /// <summary>Enumerate all active debris body indices into an output array.</summary>
        public static void EnumerateActive(NativeList<int> outIndices)
        {
            for (int i = 0; i < _capacity; i++)
            {
                if (_inUse[i])
                    outIndices.Add(i);
            }
        }

        /// <summary>Clear all debris bodies — called at session end (FR-031).</summary>
        public static void Clear()
        {
            for (int i = 0; i < _capacity; i++)
                _inUse[i] = false;

            _bodies.Clear();
            _bodies.Resize(_capacity, NativeArrayOptions.ClearMemory);
        }

        /// <summary>Dispose the pool and release all native memory.</summary>
        public static void Dispose()
        {
            if (_inUse.IsCreated) _inUse.Dispose();
            if (_bodies.IsCreated) _bodies.Dispose();
            _capacity = 0;
        }

        /// <summary>Current pool capacity (for budget checks).</summary>
        public static int Capacity => _capacity;
    }
}
