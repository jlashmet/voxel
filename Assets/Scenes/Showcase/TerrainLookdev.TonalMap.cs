using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private const byte TerrainTurfNear = 22;
        private const byte TerrainTurfMid = 23;
        private const byte TerrainTurfFar = 24;
        private bool _tonalOverlayApplied;

        private void Start()
        {
            ConfigureTurfPresentation();

            VoxelRenderBridge.SunDirection = new Vector3(-0.50f, 0.81f, -0.31f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(1.00f, 0.94f, 0.66f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.90f, 0.89f, 0.62f, 1f);

            ApplyTonalOverlay();
            ApplyVisibleDetails();
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(TerrainTurfNear, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, default, SurfaceStyles.Smooth, weather);

            Vector4 grassSampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 grassSurface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];

            // Match the reference's warm yellow-green depth ramp without using the existing
            // ground texture as a high-frequency colour source. That atlas creates visible
            // parallel bands at this camera angle, so terrain microstructure comes from the
            // production material variation and authored voxel geometry until a purpose-built
            // turf texture is introduced.
            SetTurfPresentation(TerrainTurfNear, new Color(0.290f, 0.360f, 0.140f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfMid,  new Color(0.460f, 0.510f, 0.210f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfFar,  new Color(0.640f, 0.640f, 0.300f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Grass,       new Color(0.420f, 0.470f, 0.190f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Moss,        new Color(0.205f, 0.290f, 0.105f), grassSampling, grassSurface);

            // The stock masonry texture reads as horizontal strata when projected over broad
            // planar voxel clusters. Keep the production shader/material path but suppress that
            // unsuitable source texture for this terrain lookdev. The limestone silhouette,
            // lighting, coatings and procedural material variation still come from production.
            SetMaterialPresentation(Mat.TerrainLimestone,
                new Color(0.750f, 0.680f, 0.500f), 0.00f, 0.025f, 0.80f, 0.020f);
            SetMaterialPresentation(Mat.TerrainPathStone,
                new Color(0.655f, 0.585f, 0.385f), 0.00f, 0.020f, 0.90f, 0.016f);
            SetMaterialPresentation(Mat.Sand,
                new Color(0.585f, 0.535f, 0.315f), 0.00f, 0.015f, 0.92f, 0.012f);

            SetMaterialPresentation(Mat.FlowerWhite,
                new Color(0.975f, 0.955f, 0.820f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerYellow,
                new Color(0.955f, 0.800f, 0.175f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerPink,
                new Color(0.955f, 0.590f, 0.650f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerBlue,
                new Color(0.570f, 0.770f, 0.830f), 0f, 0f, 0.88f, 0f);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, 0.0f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, 0.015f, 0.92f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.022f, 0.010f, 0.014f);
        }

        private static void SetMaterialPresentation(byte material, Color colour,
            float textureWeight, float normalStrength, float roughness, float variation)
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

        private void ApplyTonalOverlay()
        {
            if (!_built || _tonalOverlayApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 900_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
                writer.SetStyled(x, top, z, GroundToneMaterial(x, z),
                    SurfaceStyles.Smooth, GroundToneCoating(x, z));
            }

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain tonal overlay exceeded voxel authoring budget.");

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _tonalOverlayApplied = true;
        }

        private static int FinalTerrainTopVoxel(int x, int z)
        {
            return HeightVoxel(x, z);
        }

        private static byte GroundToneMaterial(int x, int z)
        {
            float depth = math.saturate((z + 70f) / 630f);

            float warpX = math.sin(z * 0.025f + 0.7f) * 11.0f
                        + math.sin(z * 0.011f - 1.3f) * 7.0f;
            float warpZ = math.sin(x * 0.022f - 1.1f) * 12.0f
                        + math.sin(x * 0.009f + 0.4f) * 8.0f;
            float field = 0.58f * math.sin((x + warpX) * 0.014f + (z + warpZ) * 0.009f + 0.5f)
                        + 0.27f * math.sin((x - warpZ) * 0.027f - (z + warpX) * 0.019f + 1.8f)
                        + 0.15f * math.sin((x + z) * 0.041f - 0.2f);

            // Keep colour variation broad and subordinate to depth. Large semantic patches were
            // visually reading as painted rectangles instead of grass variation.
            float tone = depth + field * 0.075f;
            if (tone < 0.30f) return TerrainTurfNear;
            if (tone > 0.67f) return TerrainTurfFar;
            return TerrainTurfMid;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 95 && fromPath > 24)
            {
                float mossField = math.sin(x * 0.055f + z * 0.031f)
                                + 0.55f * math.sin(x * 0.024f - z * 0.047f + 1.2f);
                if (mossField > 0.68f) return Coatings.Moss;
            }
            return Coatings.None;
        }
    }
}
