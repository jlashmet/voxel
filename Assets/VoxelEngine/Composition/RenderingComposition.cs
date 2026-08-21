using System;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-owned world capabilities required by Rendering. All members are stable API
    /// contracts; the concrete Rendering.Runtime world view is constructed only inside Composition.
    /// </summary>
    public readonly struct RenderingWorldBinding
    {
        public readonly IRegionReadSource Storage;
        public readonly MaterialPaletteView Palette;
        public readonly SurfaceCatalogueView SurfaceCatalogue;
        public readonly CoatingCatalogueView CoatingCatalogue;
        public readonly IProfileBlockReadSource ProfileBlocks;

        public RenderingWorldBinding(
            IRegionReadSource storage,
            in MaterialPaletteView palette,
            in SurfaceCatalogueView surfaceCatalogue,
            in CoatingCatalogueView coatingCatalogue,
            IProfileBlockReadSource profileBlocks = null)
        {
            Storage = storage;
            Palette = palette;
            SurfaceCatalogue = surfaceCatalogue;
            CoatingCatalogue = coatingCatalogue;
            ProfileBlocks = profileBlocks;
        }
    }

    /// <summary>
    /// Rendering-specific application wiring. Presentation policy stays with scene/application
    /// code while concrete renderer bridges/catalogues remain private to Rendering.Runtime.
    /// </summary>
    public static class RenderingComposition
    {
        private static RenderingWorldBinding s_world;
        private static uint s_terrainSeed;
        private static bool s_hasWorld;

        /// <summary>
        /// Registers authoritative read capabilities with the production renderer. The binding is
        /// captured once; render frames do not allocate adapters or dispatch through application
        /// Runtime types.
        /// </summary>
        public static void ConfigureWorld(
            in RenderingWorldBinding world,
            IVoxelChangeSource changes,
            uint terrainSeed,
            double solidBuildBudgetMs,
            double waterBuildBudgetMs,
            bool farFieldEnabled)
        {
            BindWorld(in world, changes, terrainSeed, farFieldEnabled);
            VoxelRenderBridge.SolidBuildBudgetMs = solidBuildBudgetMs;
            VoxelRenderBridge.WaterBuildBudgetMs = waterBuildBudgetMs;
        }

        /// <summary>
        /// Registers a world without changing renderer budget defaults owned by the active
        /// rendering configuration.
        /// </summary>
        public static void ConfigureWorld(
            in RenderingWorldBinding world,
            IVoxelChangeSource changes,
            uint terrainSeed,
            bool farFieldEnabled) =>
            BindWorld(in world, changes, terrainSeed, farFieldEnabled);

        /// <summary>
        /// Returns the application-owned world binding currently registered with Rendering.
        /// Scene adapters can consume the same stable Storage.Api capabilities without reaching
        /// through the Rendering.Runtime bridge.
        /// </summary>
        public static bool TryGetWorld(out RenderingWorldBinding world, out uint terrainSeed)
        {
            world = s_world;
            terrainSeed = s_terrainSeed;
            return s_hasWorld;
        }

        /// <summary>Disconnects the application world from Rendering.Runtime.</summary>
        public static void ClearWorld()
        {
            // Renderer-derived caches may still own immutable Storage pins. Release them while
            // the application world is alive; clearing the binding and disposing Storage first
            // would leave the persistent URP feature holding dead NativeArray safety handles.
            VoxelRenderBridge.ReleaseWorldResources();
            s_hasWorld = false;
            s_world = default;
            s_terrainSeed = 0;
            VoxelRenderBridge.Source = null;
            VoxelRenderBridge.Changes = null;
            VoxelRenderBridge.FarFieldEnabled = false;
        }

        public static void ResetSurfacePassDiagnostics(string reason) =>
            VoxelRenderBridge.ResetSurfacePassDiagnostics(reason);

        public static void SetSurfaceBuildEnabled(bool enabled) =>
            VoxelRenderBridge.SurfaceBuildEnabled = enabled;

        public static void SetFarBaseHeight(uint baseHeight) =>
            VoxelRenderBridge.FarBaseHeight = baseHeight;

        /// <summary>
        /// Limits voxel-meshed rings to the radius the world actually streams. Rings beyond it
        /// have no resident regions to mesh and render as holes rather than terrain.
        /// </summary>
        /// <summary>Turns coarse voxel LOD rings on or off.</summary>
        public static void SetVoxelLodEnabled(bool enabled) =>
            VoxelRenderBridge.SurfaceLodEnabled = enabled;

        public static void SetVoxelRingRadiusMetres(float metres) =>
            VoxelRenderBridge.SurfaceMaxVoxelRingRadiusMetres = Mathf.Max(0f, metres);

        /// <summary>
        /// Per-LOD residency and the live ring bands, for diagnostics. Returns null before the
        /// first surface pass has run. Allocates; never call it per frame from gameplay.
        /// </summary>
        public static string DescribeVoxelRings() => VoxelRenderBridge.DescribeRings?.Invoke();

        /// <summary>
        /// Chunks the surface pass drew, and chunks it wanted and did not have, as of the last
        /// frame. Allocation-free, so a diagnostic may sample it every frame: a chunk that
        /// disappears and returns within a few frames is invisible to per-second sampling and is
        /// exactly what a flicker is.
        /// </summary>
        public static void GetVoxelSurfaceCounts(out int visible, out int missing)
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            visible = metrics.VisibleSolidChunks;
            missing = metrics.MissingVisibleSolidChunks;
        }

        /// <summary>
        /// How many surface builds may be in flight at once while the view is still filling, and
        /// once it has caught up. The converging figure is the frame's dominant cost in a large
        /// scene: freezing builds entirely takes the full showcase from 30 ms a frame to 0.3 ms,
        /// so what this admits per frame is what the frame costs.
        /// </summary>
        public static void SetVoxelBuildConcurrency(int converging, int converged)
        {
            VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging = Mathf.Max(0, converging);
            VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged = Mathf.Max(0, converged);
        }

        /// <summary>Overrides the surface geometry arena budget, in bytes. Must be set before
        /// the renderer builds its scheduler.</summary>
        public static void SetVoxelArenaBudgetBytes(long bytes) =>
            VoxelRenderBridge.SurfaceArenaBudgetBytesOverride = System.Math.Max(0L, bytes);

        /// <summary>
        /// Whether arena pressure may retire a chunk that is currently on screen. Doing so
        /// punches a hole until it rebuilds, which reads as terrain flickering while moving.
        /// </summary>
        public static void SetEvictVisibleUnderArenaPressure(bool enabled) =>
            VoxelRenderBridge.SurfaceEvictVisibleUnderArenaPressure = enabled;

        /// <summary>
        /// Turns on per-chunk reappearance tracking: geometry that left the drawn set and came
        /// back within a few frames, which is what a flicker is. Off by default; it keeps a map
        /// keyed by chunk coordinate.
        /// </summary>
        /// <summary>
        /// Turns the settled-frame visibility reuse on or off. Off restores the unconditional
        /// per-frame traversal, which exists so a correctness question can be answered by A/B
        /// against the identical binary rather than by rebuilding and hoping nothing else moved.
        /// </summary>
        /// <summary>
        /// Scales the LOD hand-over distances. 1 is the shipped layout (finest step to 96 m);
        /// smaller draws fewer chunks and meshes more of the mid distance at half resolution.
        /// </summary>
        public static void SetVoxelDetailBandScale(float scale) =>
            VoxelEngine.Rendering.Runtime.SurfaceExtraction.VoxelSurfaceScheduler
                .DetailBandScale = scale;

        public static void SetVisibilityReuseEnabled(bool enabled) =>
            VoxelEngine.Rendering.Runtime.SurfaceExtraction.VoxelSurfaceScheduler
                .VisibilityReuseEnabled = enabled;

        public static void SetTrackSurfaceReappearance(bool enabled) =>
            VoxelEngine.Rendering.Runtime.SurfaceExtraction.VoxelSurfaceScheduler
                .TrackSurfaceReappearance = enabled;

        /// <summary>Render-feature pass rebuilds, and frames the sky pass had no material.</summary>
        public static ulong GetRenderFeatureCreateCount() =>
            VoxelRenderBridge.RenderFeatureCreateCount;

        public static ulong GetSkyPassMissingMaterialCount() =>
            VoxelRenderBridge.SkyPassMissingMaterialCount;

        /// <summary>Cumulative reappearances, or zero when tracking is off.</summary>
        public static ulong GetSurfaceReappearances() =>
            VoxelRenderBridge.SurfaceReappearances?.Invoke() ?? 0UL;

        public static void SetWaterRenderEnabled(bool enabled) =>
            VoxelRenderBridge.WaterRenderEnabled = enabled;

        public static void SetVoxelBuildBudgetMs(double budgetMs, double convergenceScale)
        {
            VoxelRenderBridge.SolidBuildBudgetMs = System.Math.Max(0.0, budgetMs);
            VoxelRenderBridge.SurfaceConvergenceBudgetScale = System.Math.Max(1.0, convergenceScale);
        }

        public static void SetLocalLights(Vector4[] lights, Vector4[] colours)
        {
            VoxelRenderBridge.LocalLights = lights ?? Array.Empty<Vector4>();
            VoxelRenderBridge.LocalLightColours = colours ?? Array.Empty<Vector4>();
        }

        public static void SetFlashlight(bool enabled, Vector3 position, Vector3 direction)
        {
            VoxelRenderBridge.FlashlightEnabled = enabled;
            VoxelRenderBridge.FlashlightPosition = position;
            VoxelRenderBridge.FlashlightDirection = direction;
        }

        public static void SetCutaway(bool enabled, Vector3 minVoxel, Vector3 maxVoxel)
        {
            VoxelRenderBridge.CutawayMinVoxel = minVoxel;
            VoxelRenderBridge.CutawayMaxVoxel = maxVoxel;
            VoxelRenderBridge.CutawayEnabled = enabled;
        }

        public static void ResetTransientPresentation()
        {
            VoxelRenderBridge.CutawayEnabled = false;
            VoxelRenderBridge.LocalLights = Array.Empty<Vector4>();
            VoxelRenderBridge.LocalLightColours = Array.Empty<Vector4>();
            VoxelRenderBridge.FlashlightEnabled = false;
        }

        /// <summary>
        /// Applies the common environment values owned by a scene while keeping the renderer's
        /// mutable global bridge private to Composition.
        /// </summary>
        public static void ConfigureEnvironment(
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
        /// Returns the renderer's authoritative albedo for a semantic voxel material.
        /// Far-field/application presentation can stay colour-consistent with the near field
        /// without taking a direct dependency on Rendering.Runtime.
        /// </summary>
        public static Vector4 GetMaterialAlbedo(byte material) =>
            VoxelPresentationCatalogue.MaterialAlbedo[material];

        public static Vector4 GetCoatingTint(byte coating) =>
            VoxelPresentationCatalogue.CoatingTint[coating];

        public static void SetMaterialAlbedo(byte material, Vector4 albedo) =>
            VoxelPresentationCatalogue.MaterialAlbedo[material] = albedo;

        public static void SetCoatingTint(byte coating, Vector4 tint) =>
            VoxelPresentationCatalogue.CoatingTint[coating] = tint;

        public static void SetBuildBudgets(double solidBuildBudgetMs, double waterBuildBudgetMs)
        {
            VoxelRenderBridge.SolidBuildBudgetMs = solidBuildBudgetMs;
            VoxelRenderBridge.WaterBuildBudgetMs = waterBuildBudgetMs;
        }

        public static void SetSky(Color horizon, Color zenith)
        {
            VoxelRenderBridge.SkyHorizon = horizon;
            VoxelRenderBridge.SkyZenith = zenith;
        }

        /// <summary>
        /// Sets the key-light direction without disturbing sky or debug tint. Look-development
        /// benches drive this continuously from sun azimuth/elevation controls, for which
        /// <see cref="ConfigureEnvironment"/> is too coarse: it would clobber the surface debug
        /// tint and both sky colours on every frame.
        /// </summary>
        public static void SetSunDirection(Vector3 sunDirection) =>
            VoxelRenderBridge.SunDirection = sunDirection;

        public static bool TryGetSurfaceBuildStatus(
            out int knownChunks,
            out int dirtyChunks,
            out int residentChunks,
            out long residentGeometryBytes)
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            knownChunks = metrics.SolidKnownChunks;
            dirtyChunks = metrics.SolidDirtyChunks;
            residentChunks = metrics.SolidResidentChunks;
            residentGeometryBytes = metrics.ResidentGeometryBytes;
            return knownChunks > 0;
        }

        /// <summary>
        /// Conservative handoff signal for application-owned far-field presentation.
        ///
        /// Generated Storage residency is not enough to open a hole in fallback terrain: the
        /// asynchronous renderer can still be dirty, building, awaiting upload, or explicitly
        /// missing visible chunks. Until those publication states are quiescent the far field
        /// must remain available underneath the voxel renderer. This is intentionally stricter
        /// than ordinary draw readiness; stale ready geometry may still be drawable while a
        /// replacement is pending, but keeping fallback coverage during that interval is safe.
        /// </summary>
        public static bool HasCompletePublishedNearSurfaceCoverage()
        {
            // Coverage is about what the camera can see, not about the world being idle. Dirty
            // chunks, running jobs and pending uploads all include the 360-degree prefetch shell,
            // which never empties while the player moves — so requiring them left the far field's
            // hole permanently shut and the clipmap drawn straight over the near terrain, double
            // shading the ground the player is standing on.
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            return metrics.SolidKnownChunks > 0
                && metrics.SolidResidentChunks > 0
                && metrics.MissingVisibleSolidChunks == 0;
        }

        private static void BindWorld(
            in RenderingWorldBinding world,
            IVoxelChangeSource changes,
            uint terrainSeed,
            bool farFieldEnabled)
        {
            if (world.Storage == null)
                throw new ArgumentException("Rendering requires a storage read source.", nameof(world));

            // A persistent renderer feature can outlive many application worlds. If a caller
            // replaces the authoritative storage binding without an explicit ClearWorld first,
            // retire scheduler jobs, pins, derived meshes, and old-world transient presentation
            // while the old owner is still live. Reapplying configuration for the same storage is
            // intentionally cheap and keeps both its warm derived geometry and presentation state.
            if (s_hasWorld && !ReferenceEquals(s_world.Storage, world.Storage))
            {
                VoxelRenderBridge.ReleaseWorldResources();
                ResetTransientPresentation();
            }

            s_world = world;
            s_terrainSeed = terrainSeed;
            s_hasWorld = true;
            VoxelRenderBridge.Source = ResolveWorld;
            VoxelRenderBridge.Changes = changes;
            VoxelRenderBridge.FarFieldEnabled = farFieldEnabled;
            VoxelRenderBridge.TerrainSeed = terrainSeed;
        }

        private static VoxelWorldView ResolveWorld()
        {
            if (!s_hasWorld) return default;
            return new VoxelWorldView
            {
                Storage = s_world.Storage,
                Palette = s_world.Palette,
                SurfaceCatalogueView = s_world.SurfaceCatalogue,
                CoatingCatalogueView = s_world.CoatingCatalogue,
                ProfileBlocks = s_world.ProfileBlocks,
            };
        }
    }
}
