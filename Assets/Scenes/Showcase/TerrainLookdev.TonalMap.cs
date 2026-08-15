using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private const byte TerrainTurfNear = 22;
        private const byte TerrainTurfMid = 23;
        private const byte TerrainTurfFar = 24;
        private const byte TerrainLimestoneAccent = 25;
        private bool _tonalOverlayApplied;

        private void Start()
        {
            ConfigureTurfPresentation();

            VoxelRenderBridge.SunDirection = new Vector3(-0.50f, 0.81f, -0.31f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.96f, 0.91f, 0.61f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.82f, 0.84f, 0.54f, 1f);

            ApplyTonalOverlay();
            ApplyVegetatedCap();
            ApplyReferenceDetails();
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(TerrainTurfNear, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainLimestoneAccent, 210, default, SurfaceStyles.Planar, weather);

            Vector4 grassSampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 grassSurface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];

            SetTurfPresentation(TerrainTurfNear, new Color(0.34f, 0.43f, 0.16f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfMid,  new Color(0.39f, 0.48f, 0.18f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfFar,  new Color(0.44f, 0.51f, 0.20f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Grass,       new Color(0.39f, 0.48f, 0.18f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Moss,        new Color(0.24f, 0.34f, 0.12f), grassSampling, grassSurface);

            SetMaterialPresentation(Mat.TerrainLimestone,
                new Color(0.69f, 0.64f, 0.47f), 0.24f, 0.11f, 0.82f, 0.018f);
            SetMaterialPresentation(TerrainLimestoneAccent,
                new Color(0.76f, 0.70f, 0.53f), 0.28f, 0.13f, 0.80f, 0.020f);
            SetMaterialPresentation(Mat.TerrainPathStone,
                new Color(0.66f, 0.59f, 0.41f), 0.18f, 0.09f, 0.88f, 0.014f);
            SetMaterialPresentation(Mat.Sand,
                new Color(0.56f, 0.51f, 0.31f), 0.10f, 0.05f, 0.92f, 0.010f);
            SetMaterialPresentation(Mat.TerrainEarth,
                new Color(0.34f, 0.38f, 0.17f), 0.02f, 0.025f, 0.92f, 0.010f);

            SetMaterialPresentation(Mat.FlowerWhite,
                new Color(0.98f, 0.96f, 0.84f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerYellow,
                new Color(0.96f, 0.80f, 0.20f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerPink,
                new Color(0.96f, 0.61f, 0.67f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerBlue,
                new Color(0.57f, 0.77f, 0.84f), 0f, 0f, 0.88f, 0f);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, 0.13f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, 0.07f, 0.91f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.018f, 0.009f, 0.012f);
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
            _tonalOverlayApplied = true;
        }

        private static int FinalTerrainTopVoxel(int x, int z)
        {
            return HeightVoxel(x, z);
        }

        private static byte GroundToneMaterial(int x, int z)
        {
            return TurfMaterial(x, z);
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 110 && fromPath > 24)
            {
                float mossField = math.sin(x * 0.043f + z * 0.029f)
                                + 0.48f * math.sin(x * 0.019f - z * 0.037f + 1.2f);
                if (mossField > 0.72f) return Coatings.Moss;
            }
            return Coatings.None;
        }
    }
}
