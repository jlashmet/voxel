using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Simulation.Api;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Simulation.Runtime
{
    /// <summary>
    /// Sparse deterministic simulation for burning solids and source-driven water.
    ///
    /// Fire preserves the structural material while burning, applies the Fire coating, spreads
    /// across face neighbours, and consumes the voxel after its lifetime. Water falls first, then
    /// spreads horizontally with a decaying level when supported. Storage is consumed exclusively
    /// through Storage.Api query/mutation capabilities; this runtime knows nothing about physical
    /// regions, brick pools or render implementation details.
    /// </summary>
    public sealed class FireWaterSimulation : IFireWaterSimulation
    {
        private struct FireState
        {
            public int RemainingTicks;
            public uint NextSpreadTick;
            public byte Material;
            public VoxelSurfaceSemantics OriginalSurface;
        }

        private static readonly int3[] FireNeighbours =
        {
            new int3( 1, 0, 0), new int3(-1, 0, 0),
            new int3( 0, 1, 0), new int3( 0,-1, 0),
            new int3( 0, 0, 1), new int3( 0, 0,-1),
        };

        private static readonly int3[] HorizontalNeighbours =
        {
            new int3( 1, 0, 0), new int3(-1, 0, 0),
            new int3( 0, 0, 1), new int3( 0, 0,-1),
        };

        private readonly IVoxelSurfaceQuery _reads;
        private readonly IRegionMutationStore _mutations;
        private readonly MaterialSimulationView _materials;
        private readonly Dictionary<int3, FireState> _fire = new Dictionary<int3, FireState>();
        private readonly List<int3> _fireKeys = new List<int3>();
        private readonly List<int3> _pendingIgnitions = new List<int3>();
        private Dictionary<int3, WaterVoxelState> _water = new Dictionary<int3, WaterVoxelState>();
        private Dictionary<int3, WaterVoxelState> _nextWater = new Dictionary<int3, WaterVoxelState>();
        private readonly List<int3> _waterKeys = new List<int3>();
        private FireWaterConfig _config;

        public event Action<int3> VoxelChanged;

        public uint TickIndex { get; private set; }
        public int BurningCount => _fire.Count;
        public int ActiveWaterCount => _water.Count;
        public FireWaterConfig Config => _config;

        public FireWaterSimulation(
            IVoxelSurfaceQuery reads,
            IRegionMutationStore mutations,
            in MaterialSimulationView materials)
            : this(reads, mutations, in materials, FireWaterConfig.Default)
        {
        }

        public FireWaterSimulation(
            IVoxelSurfaceQuery reads,
            IRegionMutationStore mutations,
            in MaterialSimulationView materials,
            FireWaterConfig config)
        {
            _reads = reads ?? throw new ArgumentNullException(nameof(reads));
            _mutations = mutations ?? throw new ArgumentNullException(nameof(mutations));
            _materials = materials;
            _config = Normalize(config);
        }

        public bool IsBurning(int3 voxel) => _fire.ContainsKey(voxel);

        public bool TryGetWaterState(int3 voxel, out WaterVoxelState state) =>
            _water.TryGetValue(voxel, out state);

        public bool Ignite(int3 voxel)
        {
            if (_fire.ContainsKey(voxel) || _fire.Count >= _config.MaxActiveFireCells)
                return false;
            if (!_reads.TryRead(voxel, out VoxelCell cell)
                || !cell.IsSolid
                || !_materials.IsFlammable(cell.BaseMaterialId))
                return false;

            VoxelSurfaceSemantics originalSurface = cell.Surface;
            cell.Surface.CoatingId = Coatings.Fire;
            if (!WriteCell(voxel, in cell))
                return false;

            _fire.Add(voxel, new FireState
            {
                RemainingTicks = _config.FireLifetimeTicks,
                NextSpreadTick = TickIndex + (uint)_config.FireSpreadIntervalTicks,
                Material = cell.BaseMaterialId,
                OriginalSurface = originalSurface,
            });
            return true;
        }

        public bool Extinguish(int3 voxel)
        {
            if (!_fire.TryGetValue(voxel, out FireState fire))
                return false;

            RestoreBurningSurface(voxel, in fire);
            _fire.Remove(voxel);
            return true;
        }

        public bool AddWaterSource(int3 voxel)
        {
            if (!_reads.TryRead(voxel, out VoxelCell cell))
                return false;

            byte material = cell.BaseMaterialId;
            bool existingWater = material == _config.WaterMaterial
                              || material == _config.CascadeMaterial;
            if (material != VoxelGrid.MaterialEmpty && !existingWater)
                return false;
            if (!_water.ContainsKey(voxel) && _water.Count >= _config.MaxActiveWaterCells)
                return false;

            _water[voxel] = new WaterVoxelState(_config.WaterMaxLevel, true, false);
            return WriteMaterial(voxel, _config.WaterMaterial) || existingWater;
        }

        public bool RemoveWaterSource(int3 voxel)
        {
            if (!_water.TryGetValue(voxel, out WaterVoxelState state) || !state.IsSource)
                return false;
            state.IsSource = false;
            _water[voxel] = state;
            return true;
        }

        public void Tick()
        {
            TickIndex++;
            TickWater();
            TickFire();
        }

        private void TickWater()
        {
            _nextWater.Clear();
            _waterKeys.Clear();
            foreach (int3 key in _water.Keys)
                _waterKeys.Add(key);
            _waterKeys.Sort(CompareCoordinates);

            for (int i = 0; i < _waterKeys.Count; i++)
            {
                int3 voxel = _waterKeys[i];
                WaterVoxelState state = _water[voxel];
                if (!_reads.TryRead(voxel, out VoxelCell cell))
                    continue;
                byte material = cell.BaseMaterialId;
                if (material != _config.WaterMaterial && material != _config.CascadeMaterial)
                    continue;

                if (state.IsSource)
                {
                    MergeWater(_nextWater, voxel,
                        new WaterVoxelState(_config.WaterMaxLevel, true, false));
                }
                if (state.Level == 0)
                    continue;

                int3 below = voxel + new int3(0, -1, 0);
                if (CanFlowInto(below))
                {
                    MergeWater(_nextWater, below,
                        new WaterVoxelState(state.Level, false, true));
                    continue;
                }

                if (state.Level <= 1)
                    continue;

                byte nextLevel = (byte)(state.Level - 1);
                for (int n = 0; n < HorizontalNeighbours.Length; n++)
                {
                    int3 neighbour = voxel + HorizontalNeighbours[n];
                    if (CanFlowInto(neighbour))
                    {
                        MergeWater(_nextWater, neighbour,
                            new WaterVoxelState(nextLevel, false, false));
                    }
                }
            }

            for (int i = 0; i < _waterKeys.Count; i++)
            {
                int3 voxel = _waterKeys[i];
                if (_nextWater.ContainsKey(voxel))
                    continue;
                if (!_reads.TryRead(voxel, out VoxelCell cell))
                    continue;
                if (cell.BaseMaterialId == _config.WaterMaterial
                    || cell.BaseMaterialId == _config.CascadeMaterial)
                {
                    WriteMaterial(voxel, VoxelGrid.MaterialEmpty);
                }
            }

            foreach (KeyValuePair<int3, WaterVoxelState> pair in _nextWater)
            {
                byte desired = pair.Value.IsFalling
                    ? _config.CascadeMaterial
                    : _config.WaterMaterial;
                WriteMaterial(pair.Key, desired);
            }

            Dictionary<int3, WaterVoxelState> swap = _water;
            _water = _nextWater;
            _nextWater = swap;
        }

        private void TickFire()
        {
            _fireKeys.Clear();
            foreach (int3 key in _fire.Keys)
                _fireKeys.Add(key);
            _fireKeys.Sort(CompareCoordinates);
            _pendingIgnitions.Clear();

            for (int i = 0; i < _fireKeys.Count; i++)
            {
                int3 voxel = _fireKeys[i];
                if (!_fire.TryGetValue(voxel, out FireState state))
                    continue;

                if (!_reads.TryRead(voxel, out VoxelCell cell)
                    || cell.BaseMaterialId != state.Material
                    || !_materials.IsFlammable(cell.BaseMaterialId))
                {
                    _fire.Remove(voxel);
                    continue;
                }

                if (TouchesWater(voxel))
                {
                    RestoreBurningSurface(voxel, in state);
                    _fire.Remove(voxel);
                    continue;
                }

                state.RemainingTicks--;
                if (state.RemainingTicks <= 0)
                {
                    WriteMaterial(voxel, VoxelGrid.MaterialEmpty);
                    _fire.Remove(voxel);
                    continue;
                }

                if (TickIndex >= state.NextSpreadTick)
                {
                    TrySpreadFire(voxel);
                    state.NextSpreadTick = TickIndex + (uint)_config.FireSpreadIntervalTicks;
                }
                _fire[voxel] = state;
            }

            _pendingIgnitions.Sort(CompareCoordinates);
            int3 previous = default;
            bool havePrevious = false;
            for (int i = 0; i < _pendingIgnitions.Count; i++)
            {
                int3 candidate = _pendingIgnitions[i];
                if (havePrevious && candidate.Equals(previous))
                    continue;
                Ignite(candidate);
                previous = candidate;
                havePrevious = true;
            }
        }

        private void TrySpreadFire(int3 source)
        {
            for (int i = 0; i < FireNeighbours.Length; i++)
            {
                int3 offset = FireNeighbours[i];
                int3 target = source + offset;
                if (_fire.ContainsKey(target))
                    continue;
                if (!_reads.TryRead(target, out VoxelCell cell)
                    || !cell.IsSolid
                    || !_materials.IsFlammable(cell.BaseMaterialId))
                    continue;

                int chance = _config.FireSpreadChancePercent + (offset.y > 0 ? 15 : 0);
                if (chance > 100)
                    chance = 100;
                if (RollPercent(source, target, TickIndex) < chance)
                    _pendingIgnitions.Add(target);
            }
        }

        private bool TouchesWater(int3 voxel)
        {
            if (_water.ContainsKey(voxel))
                return true;
            for (int i = 0; i < FireNeighbours.Length; i++)
            {
                if (_water.ContainsKey(voxel + FireNeighbours[i]))
                    return true;
            }
            return false;
        }

        private bool CanFlowInto(int3 voxel)
        {
            if (!_reads.TryRead(voxel, out VoxelCell cell))
                return false;
            byte material = cell.BaseMaterialId;
            if (material == VoxelGrid.MaterialEmpty)
                return true;
            return _water.ContainsKey(voxel)
                && (material == _config.WaterMaterial || material == _config.CascadeMaterial);
        }

        private void MergeWater(
            Dictionary<int3, WaterVoxelState> target,
            int3 voxel,
            WaterVoxelState incoming)
        {
            if (target.TryGetValue(voxel, out WaterVoxelState existing))
            {
                if (incoming.Level > existing.Level)
                    existing.Level = incoming.Level;
                existing.IsSource |= incoming.IsSource;
                existing.IsFalling &= incoming.IsFalling;
                target[voxel] = existing;
                return;
            }

            if (target.Count < _config.MaxActiveWaterCells)
                target.Add(voxel, incoming);
        }

        private void RestoreBurningSurface(int3 voxel, in FireState state)
        {
            if (!_reads.TryRead(voxel, out VoxelCell cell))
                return;
            if (cell.BaseMaterialId != state.Material || cell.Surface.CoatingId != Coatings.Fire)
                return;
            cell.Surface = state.OriginalSurface;
            WriteCell(voxel, in cell);
        }

        private bool WriteMaterial(int3 voxel, byte material)
        {
            int3 regionCoord = voxel >> VoxelGrid.RegionVoxelEdgeLog2;
            if (!_mutations.IsRegionResident(regionCoord))
                return false;

            int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_mutations.TryBeginPartialBlock(
                    worldBlock, material, false, out VoxelBlockMutation mutation))
                return false;

            bool payloadChanged = false;
            if (mutation.IsCreated)
            {
                int index = VoxelIndex(voxel);
                payloadChanged = mutation.SetMaterial(index, material);
            }

            bool changed = _mutations.CompletePartialBlock(ref mutation, payloadChanged);
            if (changed)
                VoxelChanged?.Invoke(voxel);
            return changed;
        }

        private bool WriteCell(int3 voxel, in VoxelCell cell)
        {
            int3 regionCoord = voxel >> VoxelGrid.RegionVoxelEdgeLog2;
            if (!_mutations.IsRegionResident(regionCoord))
                return false;

            int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
            if (!_mutations.TryBeginCellBlock(worldBlock, false, out VoxelBlockMutation mutation))
                return false;

            bool payloadChanged = mutation.IsCreated && mutation.SetCell(VoxelIndex(voxel), in cell);
            bool changed = _mutations.CompletePartialBlock(ref mutation, payloadChanged);
            if (changed)
                VoxelChanged?.Invoke(voxel);
            return changed;
        }

        private static int VoxelIndex(int3 voxel)
        {
            int x = voxel.x & VoxelReadGrid.BlockEdgeMask;
            int y = voxel.y & VoxelReadGrid.BlockEdgeMask;
            int z = voxel.z & VoxelReadGrid.BlockEdgeMask;
            return x
                 | (y << VoxelReadGrid.BlockEdgeLog2)
                 | (z << (VoxelReadGrid.BlockEdgeLog2 * 2));
        }

        private static FireWaterConfig Normalize(FireWaterConfig config)
        {
            FireWaterConfig defaults = FireWaterConfig.Default;
            if (config.FireLifetimeTicks <= 0)
                config.FireLifetimeTicks = defaults.FireLifetimeTicks;
            if (config.FireSpreadIntervalTicks <= 0)
                config.FireSpreadIntervalTicks = defaults.FireSpreadIntervalTicks;
            if (config.FireSpreadChancePercent > 100)
                config.FireSpreadChancePercent = 100;
            if (config.WaterMaxLevel == 0)
                config.WaterMaxLevel = defaults.WaterMaxLevel;
            if (config.WaterMaterial == VoxelGrid.MaterialEmpty)
                config.WaterMaterial = defaults.WaterMaterial;
            if (config.CascadeMaterial == VoxelGrid.MaterialEmpty)
                config.CascadeMaterial = defaults.CascadeMaterial;
            if (config.MaxActiveFireCells <= 0)
                config.MaxActiveFireCells = defaults.MaxActiveFireCells;
            if (config.MaxActiveWaterCells <= 0)
                config.MaxActiveWaterCells = defaults.MaxActiveWaterCells;
            return config;
        }

        private static int CompareCoordinates(int3 a, int3 b)
        {
            int y = a.y.CompareTo(b.y);
            if (y != 0)
                return y;
            int x = a.x.CompareTo(b.x);
            if (x != 0)
                return x;
            return a.z.CompareTo(b.z);
        }

        private static int RollPercent(int3 source, int3 target, uint tick)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)source.x) * 16777619u;
                hash = (hash ^ (uint)source.y) * 16777619u;
                hash = (hash ^ (uint)source.z) * 16777619u;
                hash = (hash ^ (uint)target.x) * 16777619u;
                hash = (hash ^ (uint)target.y) * 16777619u;
                hash = (hash ^ (uint)target.z) * 16777619u;
                hash = (hash ^ tick) * 16777619u;
                hash ^= hash >> 16;
                return (int)(hash % 100u);
            }
        }
    }
}
