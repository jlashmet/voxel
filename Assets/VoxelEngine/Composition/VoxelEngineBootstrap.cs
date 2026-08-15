using System;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Public lifetime boundary for the physical voxel store. Callers receive only Storage.Api
    /// capabilities; the Composition assembly is the sole owner of the concrete table/pool pair.
    /// </summary>
    public interface IVoxelStorageRuntime : IDisposable
    {
        IRegionGenerationStore Generation { get; }
        IRegionReadSource Reads { get; }
        IRegionMutationStore Mutations { get; }
        IRegionResidencyStore Residency { get; }
        IRegionSnapshotSource Snapshots { get; }
        IRegionSnapshotMutationStore SnapshotMutations { get; }
        IVoxelSurfaceQuery SurfaceQuery { get; }
        IVoxelChangeSource Changes { get; }
    }

    /// <summary>
    /// Top-level runtime construction entry point. Domain algorithms do not belong here; this
    /// class owns allocation, concrete implementation selection, API-capability wiring and
    /// disposal only.
    /// </summary>
    public static class VoxelEngineBootstrap
    {
        /// <summary>
        /// Converts an application memory budget into a mixed-brick capacity without exposing
        /// Storage.Runtime physical byte layout to scene/application code.
        /// </summary>
        public static int ClampMixedBrickCapacityToBudget(
            int requestedCapacity,
            int budgetBytes,
            int minimumCapacity = 4096)
        {
            if (minimumCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity));

            int budgetCapacity = Math.Max(
                minimumCapacity,
                budgetBytes / VoxelDimensions.BytesPerMixedBrick);
            return Math.Min(Math.Max(requestedCapacity, minimumCapacity), budgetCapacity);
        }

        public static IVoxelStorageRuntime CreateStorage(
            int expectedResidentRegions,
            int mixedBrickCapacity,
            int changeJournalCapacity = 4096)
        {
            if (expectedResidentRegions <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedResidentRegions));
            if (mixedBrickCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(mixedBrickCapacity));
            if (changeJournalCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(changeJournalCapacity));

            return new StorageRuntimeLifetime(
                expectedResidentRegions,
                mixedBrickCapacity,
                changeJournalCapacity);
        }

        private sealed class StorageRuntimeLifetime : IVoxelStorageRuntime
        {
            private RegionTable _table;
            private BrickPool _pool;
            private readonly VoxelChangeJournal _changes;
            private readonly RegionGenerationStore _generation;
            private readonly RegionReadSource _reads;
            private readonly RegionMutationStore _mutations;
            private readonly RegionResidencyStore _residency;
            private readonly RegionSnapshotMutationStore _snapshotMutations;
            private bool _disposed;

            public StorageRuntimeLifetime(
                int expectedResidentRegions,
                int mixedBrickCapacity,
                int changeJournalCapacity)
            {
                _table = new RegionTable(expectedResidentRegions, Allocator.Persistent);
                _pool = new BrickPool(mixedBrickCapacity, Allocator.Persistent);
                _changes = new VoxelChangeJournal(changeJournalCapacity);
                _generation = new RegionGenerationStore(in _table);
                _reads = new RegionReadSource(in _table, in _pool, _changes);
                _mutations = new RegionMutationStore(in _table, in _pool);
                _residency = new RegionResidencyStore(in _table, in _pool);
                _snapshotMutations = new RegionSnapshotMutationStore(in _table, in _pool);
            }

            public IRegionGenerationStore Generation => _generation;
            public IRegionReadSource Reads => _reads;
            public IRegionMutationStore Mutations => _mutations;
            public IRegionResidencyStore Residency => _residency;
            public IRegionSnapshotSource Snapshots => _reads;
            public IRegionSnapshotMutationStore SnapshotMutations => _snapshotMutations;
            public IVoxelSurfaceQuery SurfaceQuery => _reads;
            public IVoxelChangeSource Changes => _changes;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_table.IsCreated) _table.Dispose();
                if (_pool.IsCreated) _pool.Dispose();
            }
        }
    }
}
