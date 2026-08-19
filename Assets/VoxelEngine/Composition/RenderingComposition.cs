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
