using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Public lifetime boundary for the physical voxel store. Callers receive only Storage.Api
    /// capabilities; the Composition assembly is the sole owner of concrete storage/catalogue
    /// implementations and the table/pool pair.
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

        IMaterialAuthoringCatalogue MaterialAuthoring { get; }
        MaterialPaletteView MaterialPresentation { get; }
        SurfaceCatalogueView SurfacePresentation { get; }
        CoatingCatalogueView CoatingPresentation { get; }

        void RegisterMaterial(
            byte materialId,
            byte hardness,
            DestructionClass destructionClass,
            ushort defaultSurfaceStyle,
            uint allowedCoatings);

        /// <summary>
        /// Adjusts presentation-only decoration values on an existing coating while concrete
        /// catalogue ownership remains inside Composition/Storage.Runtime.
        /// </summary>
        void ConfigureCoatingDecoration(
            byte coatingId,
            byte density,
            byte radiusQ4,
            byte heightQ4,
            byte dropQ4,
            byte separation);

        /// <summary>
        /// Publishes the currently resident regions to derived consumers after an application
        /// finishes a bulk authoring phase. Physical region enumeration and journal mutation stay
        /// inside Composition/Storage.Runtime.
        /// </summary>
        void PublishAllResidentRegions();
    }

    /// <summary>
    /// Top-level runtime construction entry point. Domain algorithms do not belong here; this
    /// class owns allocation, concrete implementation selection, API-capability wiring and
    /// disposal only.
    /// </summary>
    public static class VoxelEngineBootstrap
    {
        /// <summary>
        /// Fallback safety ceiling for one eagerly allocated mixed-brick pool. BrickPool reserves
        /// every payload plane up front, so an unchecked capacity can otherwise consume gigabytes
        /// before streaming has authored a single brick.
        ///
        /// This is a backstop for callers that do not know their own budget, not a global cap: it
        /// sits below every tier in device-matrix.md, which is authoritative for memory targets.
        /// A caller that has already sized itself against a tier budget must pass that budget to
        /// <see cref="CreateStorage"/> so this constant does not silently re-clamp it downward.
        /// </summary>
        public const long MaximumMixedBrickAllocationBytes = 256L * 1024 * 1024;

        /// <summary>
        /// Converts an application memory budget into a mixed-brick capacity without exposing
        /// Storage.Runtime physical byte layout to scene/application code.
        /// </summary>
        /// <remarks>
        /// A non-positive <paramref name="budgetBytes"/> means "no budget information available"
        /// and degrades to <paramref name="minimumCapacity"/> rather than throwing: this is a
        /// clamp, and the safe answer for an absent budget is the smallest pool, not an exception
        /// on a caller that has no budget to offer. Entry points that genuinely require a budget
        /// — <see cref="CreateStorage"/> — validate it at their own boundary instead.
        /// </remarks>
        public static int ClampMixedBrickCapacityToBudget(
            int requestedCapacity,
            long budgetBytes,
            int minimumCapacity = 4096)
        {
            if (requestedCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedCapacity));
            if (minimumCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumCapacity));

            long budgetCapacityLong = Math.Max(
                (long)minimumCapacity,
                budgetBytes / VoxelDimensions.BytesPerMixedBrick);
            int budgetCapacity = (int)Math.Min(int.MaxValue, budgetCapacityLong);
            return Math.Min(Math.Max(requestedCapacity, minimumCapacity), budgetCapacity);
        }

        /// <summary>
        /// Applies application-owned presentation settings to the concrete renderer bridge.
        /// Scene code supplies presentation values but does not reference Rendering.Runtime.
        /// </summary>
        public static void ConfigureRenderingEnvironment(
            Color surfaceDebugTint,
            Vector3 sunDirection,
            Color skyHorizon,
            Color skyZenith)
        {
            VoxelRenderBridge.SurfaceDebugTint = surfaceDebugTint;
            VoxelRenderBridge.SunDirection = sunDirection;
            VoxelRenderBridge.SkyHorizon = skyHorizon;
            VoxelRenderBridge.SkyZenith = skyZenith;
        }

        /// <summary>
        /// Applies sky-only lookdev settings while keeping renderer globals behind Composition.
        /// </summary>
        public static void ConfigureRenderingSky(
            Vector3 sunDirection,
            Color skyHorizon,
            Color skyZenith)
        {
            VoxelRenderBridge.SunDirection = sunDirection;
            VoxelRenderBridge.SkyHorizon = skyHorizon;
            VoxelRenderBridge.SkyZenith = skyZenith;
        }

        /// <summary>
        /// Configures a turf presentation row from an existing renderer material template.
        /// Concrete presentation arrays remain private to Rendering.Runtime.
        /// </summary>
        public static void ConfigureTurfMaterialPresentation(
            byte material,
            byte templateMaterial,
            Color colour)
        {
            Vector4 sampling = VoxelPresentationCatalogue.MaterialSampling[templateMaterial];
            Vector4 surface = VoxelPresentationCatalogue.MaterialSurface[templateMaterial];
            VoxelPresentationCatalogue.MaterialAlbedo[material] =
                new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, 0.13f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, 0.07f, 0.91f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.018f, 0.009f, 0.012f);
        }

        /// <summary>
        /// Applies a material presentation override while preserving the row's authored sampling
        /// projection and UV scale. Scene code owns the lookdev values; Rendering.Runtime owns the
        /// physical GPU catalogue.
        /// </summary>
        public static void ConfigureMaterialPresentation(
            byte material,
            Color colour,
            float textureWeight,
            float normalStrength,
            float roughness,
            float variation)
        {
            Vector4 sampling = VoxelPresentationCatalogue.MaterialSampling[material];
            Vector4 surface = VoxelPresentationCatalogue.MaterialSurface[material];
            VoxelPresentationCatalogue.MaterialAlbedo[material] =
                new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, textureWeight);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, normalStrength, roughness, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, variation, variation * 0.5f, variation);
        }

        /// <summary>
        /// Projects concrete renderer timing state into the stable presentation diagnostics API.
        /// </summary>
        public static SurfaceTimingDiagnostics GetSurfaceTimingDiagnostics()
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            return new SurfaceTimingDiagnostics(
                metrics.SchedulerPrepareTiming.P95Ms,
                metrics.SurfaceDiscoveryTiming.P95Ms,
                metrics.SnapshotTiming.P95Ms,
                metrics.TopologyCompactTiming.P95Ms,
                metrics.FacetedMergeTiming.P95Ms,
                metrics.UploadTiming.P95Ms,
                metrics.QueueLatencyTiming.P95Ms);
        }

        /// <summary>
        /// Creates a Structures.Api authoring session while keeping the concrete VoxelBrush and
        /// its Runtime adapter behind the Composition boundary.
        /// </summary>
        public static IStructureAuthoringSession CreateStructureAuthoring(
            IVoxelStorageRuntime storage,
            int writeBudget)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));
            if (writeBudget <= 0)
                throw new ArgumentOutOfRangeException(nameof(writeBudget));

            return new StructureAuthoringSession(
                storage.Reads,
                storage.Mutations,
                storage.MaterialAuthoring,
                writeBudget);
        }

        /// <param name="maxMixedBrickAllocationBytes">
        /// Memory ceiling this storage's eager mixed-brick pool must respect. Callers that have
        /// already sized <paramref name="mixedBrickCapacity"/> against their device-tier budget
        /// should pass that same budget; leaving the default applies the conservative
        /// <see cref="MaximumMixedBrickAllocationBytes"/> backstop instead.
        /// </param>
        public static IVoxelStorageRuntime CreateStorage(
            int expectedResidentRegions,
            int mixedBrickCapacity,
            int changeJournalCapacity = 4096,
            long maxMixedBrickAllocationBytes = MaximumMixedBrickAllocationBytes)
        {
            if (expectedResidentRegions <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedResidentRegions));
            if (mixedBrickCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(mixedBrickCapacity));
            if (changeJournalCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(changeJournalCapacity));
            if (maxMixedBrickAllocationBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxMixedBrickAllocationBytes));

            return new StorageRuntimeLifetime(
                expectedResidentRegions,
                mixedBrickCapacity,
                changeJournalCapacity,
                maxMixedBrickAllocationBytes);
        }

        internal sealed class StorageRuntimeLifetime : IVoxelStorageRuntime
        {
            private RegionTable _table;
            private BrickPool _pool;
            private MaterialPalette _materials;
            private SurfaceCatalogue _surfaces;
            private CoatingCatalogue _coatings;
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
                int changeJournalCapacity,
                long maxMixedBrickAllocationBytes = MaximumMixedBrickAllocationBytes)
            {
                // The ceiling is still applied before the eager BrickPool allocation — that guard
                // exists to stop a pathological request freezing the machine. What it must not do
                // is invent its own number: a caller that already clamped against its device-tier
                // budget passes that budget here, so this cannot silently halve it.
                int boundedMixedBrickCapacity = ClampMixedBrickCapacityToBudget(
                    mixedBrickCapacity,
                    maxMixedBrickAllocationBytes,
                    minimumCapacity: 1);

                _table = new RegionTable(expectedResidentRegions, Allocator.Persistent);
                _pool = new BrickPool(boundedMixedBrickCapacity, Allocator.Persistent);
                _materials = default;
                _surfaces = SurfaceCatalogue.CreateBuiltIns();
                _coatings = CoatingCatalogue.CreateBuiltIns();
                _changes = new VoxelChangeJournal(changeJournalCapacity);
                _generation = new RegionGenerationStore(in _table);
                _reads = new RegionReadSource(in _table, in _pool, _changes);
                _mutations = new RegionMutationStore(in _table, in _pool);
                _residency = new RegionResidencyStore(in _table, in _pool);
                _snapshotMutations = new RegionSnapshotMutationStore(in _table, in _pool);
            }

            // Composition-owned hot paths may borrow these physical structs by ref, but no
            // reference escapes this assembly. One lifetime owns allocation, adapter refresh, and disposal.
            internal ref RegionTable Table => ref _table;
            internal ref BrickPool Pool => ref _pool;
            internal ref MaterialPalette Materials => ref _materials;
            internal ref SurfaceCatalogue Surfaces => ref _surfaces;
            internal ref CoatingCatalogue Coatings => ref _coatings;
            internal VoxelChangeJournal ChangeJournal => _changes;

            internal RegionReadSource ReadSource
            {
                get
                {
                    _reads.Refresh(in _table, in _pool);
                    return _reads;
                }
            }

            internal RegionMutationStore MutationStore
            {
                get
                {
                    _mutations.Refresh(in _table, in _pool);
                    return _mutations;
                }
            }

            internal RegionResidencyStore ResidencyStore
            {
                get
                {
                    _residency.Refresh(in _table, in _pool);
                    return _residency;
                }
            }

            internal RegionSnapshotMutationStore SnapshotMutationStore
            {
                get
                {
                    _snapshotMutations.Refresh(in _table, in _pool);
                    return _snapshotMutations;
                }
            }

            public IRegionGenerationStore Generation
            {
                get
                {
                    _generation.Refresh(in _table);
                    return _generation;
                }
            }
            public IRegionReadSource Reads => ReadSource;
            public IRegionMutationStore Mutations => MutationStore;
            public IRegionResidencyStore Residency => ResidencyStore;
            public IRegionSnapshotSource Snapshots => ReadSource;
            public IRegionSnapshotMutationStore SnapshotMutations => SnapshotMutationStore;
            public IVoxelSurfaceQuery SurfaceQuery => ReadSource;
            public IVoxelChangeSource Changes => _changes;

            public IMaterialAuthoringCatalogue MaterialAuthoring => _materials;
            public MaterialPaletteView MaterialPresentation => _materials.PresentationView;
            public SurfaceCatalogueView SurfacePresentation => _surfaces;
            public CoatingCatalogueView CoatingPresentation => _coatings;

            public void RegisterMaterial(
                byte materialId,
                byte hardness,
                DestructionClass destructionClass,
                ushort defaultSurfaceStyle,
                uint allowedCoatings)
            {
                ThrowIfDisposed();
                _materials.Register(
                    materialId,
                    hardness,
                    destructionClass,
                    defaultSurfaceStyle,
                    allowedCoatings);
            }

            public void ConfigureCoatingDecoration(
                byte coatingId,
                byte density,
                byte radiusQ4,
                byte heightQ4,
                byte dropQ4,
                byte separation)
            {
                ThrowIfDisposed();
                CoatingDefinition coating = _coatings.Get(coatingId);
                coating.DecorationDensity = density;
                coating.DecorationRadiusQ4 = radiusQ4;
                coating.DecorationHeightQ4 = heightQ4;
                coating.DecorationDropQ4 = dropQ4;
                coating.DecorationSeparation = separation;
                _coatings.Register(in coating);
                _coatings.Seal(_coatings.Version, _coatings.ComputeHash());
            }

            public void PublishAllResidentRegions()
            {
                ThrowIfDisposed();
                using NativeArray<int3> regions = ReadSource.GetResidentRegionCoords(Allocator.Temp);
                for (int i = 0; i < regions.Length; i++)
                    _changes.PublishRegion(regions[i]);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_table.IsCreated) _table.Dispose();
                if (_pool.IsCreated) _pool.Dispose();
            }

            private void ThrowIfDisposed()
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(StorageRuntimeLifetime));
            }
        }
    }
}
