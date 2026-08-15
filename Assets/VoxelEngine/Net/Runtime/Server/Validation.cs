using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Stable server-side alteration validation vocabulary and policy limits.
    ///
    /// Authoritative checks live in <see cref="AuthoritativeAlterationValidator"/>, which consumes
    /// Storage.Api capabilities. This type deliberately contains no world-storage implementation
    /// and no duplicate validation path.
    /// </summary>
    public static class Validation
    {
        public enum ValidationResult : byte
        {
            Success = 0,
            TooFast = 1,
            OverBudget = 2,
            OverDensity = 3,
            NotAttached = 4,
            InPlayerVolume = 5,
            OutOfReach = 6,
            ProtectedZone = 7,
            InvalidTarget = 8,
        }

        public const int k_MaxAlterationsPerSecond = 10;
        public const int k_MaxBricksPerTick = 512;
        public const int k_DefaultReachVoxels = 16;

        public struct AllocationBudget
        {
            public int maxBricksPerTick;
            public int maxAlterationsPerSecond;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AllocationBudget(int maxBricks, int maxAlterations)
            {
                maxBricksPerTick = maxBricks;
                maxAlterationsPerSecond = maxAlterations;
            }
        }

        public struct DensityCap
        {
            public float maxDensity;
            public int totalBricks;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public DensityCap(float maxDensity, int totalBricks)
            {
                this.maxDensity = math.clamp(maxDensity, 0f, 1f);
                this.totalBricks = totalBricks;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int MaxMixedBricks() => (int)(maxDensity * totalBricks);
        }
    }
}
