using System;
using Unity.Mathematics;

namespace VoxelEngine.Simulation.Api
{
    /// <summary>Deterministic gameplay tuning for sparse fire and source-driven water.</summary>
    public struct FireWaterConfig
    {
        public int FireLifetimeTicks;
        public int FireSpreadIntervalTicks;
        public byte FireSpreadChancePercent;
        public byte WaterMaxLevel;
        public byte WaterMaterial;
        public byte CascadeMaterial;
        public int MaxActiveFireCells;
        public int MaxActiveWaterCells;

        public static FireWaterConfig Default => new FireWaterConfig
        {
            FireLifetimeTicks = 48,
            FireSpreadIntervalTicks = 4,
            FireSpreadChancePercent = 70,
            WaterMaxLevel = 8,
            WaterMaterial = 11,
            CascadeMaterial = 16,
            MaxActiveFireCells = 8192,
            MaxActiveWaterCells = 65536,
        };
    }

    /// <summary>Logical state for one simulated water voxel.</summary>
    public struct WaterVoxelState
    {
        public byte Level;
        public bool IsSource;
        public bool IsFalling;

        public WaterVoxelState(byte level, bool isSource, bool isFalling)
        {
            Level = level;
            IsSource = isSource;
            IsFalling = isFalling;
        }
    }

    /// <summary>
    /// Stable gameplay-facing fire/water API. Composition owns the concrete runtime and injects
    /// Storage capabilities; callers never reach into Simulation.Runtime implementation details.
    /// </summary>
    public interface IFireWaterSimulation
    {
        event Action<int3> VoxelChanged;

        uint TickIndex { get; }
        int BurningCount { get; }
        int ActiveWaterCount { get; }
        FireWaterConfig Config { get; }

        bool IsBurning(int3 voxel);
        bool TryGetWaterState(int3 voxel, out WaterVoxelState state);
        bool Ignite(int3 voxel);
        bool Extinguish(int3 voxel);
        bool AddWaterSource(int3 voxel);
        bool RemoveWaterSource(int3 voxel);
        void Tick();
    }
}
