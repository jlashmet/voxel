using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Client
{
    /// <summary>
    /// Presentation helpers for authoritative alteration rejections. The wire type lives in
    /// VoxelEngine.Net.Protocol; client code must not shadow it with a second incompatible struct.
    /// </summary>
    public static class RejectionFeedback
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ShowReason(in S_AlterationRejected rejected)
        {
            return rejected.ReasonEnum() switch
            {
                S_AlterationRejected.Reason.InPlayerVolume => "Placement intersects player volume",
                S_AlterationRejected.Reason.OutOfReach => "Target out of reach distance",
                S_AlterationRejected.Reason.ProtectedZone => "Area is protected",
                S_AlterationRejected.Reason.NotAttached => "Not attached to existing structure",
                S_AlterationRejected.Reason.TooFast => "Rate limit exceeded",
                S_AlterationRejected.Reason.OverDensity => "Region density cap reached",
                S_AlterationRejected.Reason.OverBudget => "Allocation budget exceeded",
                S_AlterationRejected.Reason.InvalidTarget => "Invalid target or material",
                _ => "Placement rejected by server",
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetReasonCode(in S_AlterationRejected rejected) => rejected.reason;

        private static NativeHashMap<int3, DissolveState> _dissolves;

        private struct DissolveState
        {
            public double startTime;
            public float duration;
            public float progress;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StartDissolveAnimation(int3 startRegion, float durationSeconds)
        {
            if (!_dissolves.IsCreated)
                _dissolves = new NativeHashMap<int3, DissolveState>(16, Allocator.Persistent);

            if (_dissolves.TryGetValue(startRegion, out DissolveState existing) && existing.progress < 1f)
                return;

            _dissolves[startRegion] = new DissolveState
            {
                startTime = Environment.TickCount / 1000.0,
                duration = durationSeconds > 0f ? durationSeconds : 0.3f,
                progress = 0f,
            };
        }

        public static NativeList<int3> UpdateDissolves(out NativeArray<float> alphaLevels)
        {
            var completed = new NativeList<int3>(8, Allocator.Temp);
            if (!_dissolves.IsCreated)
            {
                alphaLevels = default;
                return completed;
            }

            double now = Environment.TickCount / 1000.0;
            NativeArray<int3> keys = _dissolves.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < keys.Length; i++)
            {
                int3 regionCoord = keys[i];
                if (!_dissolves.TryGetValue(regionCoord, out DissolveState state))
                    continue;

                state.progress = math.clamp((float)(now - state.startTime) / state.duration, 0f, 1f);
                if (state.progress >= 1f)
                {
                    completed.Add(regionCoord);
                    _dissolves.Remove(regionCoord);
                }
                else
                {
                    _dissolves[regionCoord] = state;
                }
            }
            keys.Dispose();

            int activeCount = _dissolves.Count();
            alphaLevels = new NativeArray<float>(activeCount > 0 ? activeCount : 1, Allocator.Temp);
            int alphaIndex = 0;
            foreach (var pair in _dissolves)
                alphaLevels[alphaIndex++] = math.lerp(0.5f, 0f, pair.Value.progress);

            if (alphaIndex == 0)
                alphaLevels[0] = 0.5f;

            return completed;
        }

        public static void Dispose()
        {
            if (_dissolves.IsCreated)
                _dissolves.Dispose();
            _dissolves = default;
        }
    }
}
