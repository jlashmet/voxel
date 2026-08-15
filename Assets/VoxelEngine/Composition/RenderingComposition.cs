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
            if (world.Storage == null)
                throw new ArgumentException("Rendering requires a storage read source.", nameof(world));

            s_world = world;
            s_hasWorld = true;
            VoxelRenderBridge.Source = ResolveWorld;
            VoxelRenderBridge.Changes = changes;
            VoxelRenderBridge.SolidBuildBudgetMs = solidBuildBudgetMs;
            VoxelRenderBridge.WaterBuildBudgetMs = waterBuildBudgetMs;
            VoxelRenderBridge.FarFieldEnabled = farFieldEnabled;
            VoxelRenderBridge.TerrainSeed = terrainSeed;
        }

        /// <summary>Disconnects the application world from Rendering.Runtime.</summary>
        public static void ClearWorld()
        {
            s_hasWorld = false;
            s_world = default;
            VoxelRenderBridge.Source = null;
            VoxelRenderBridge.Changes = null;
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
