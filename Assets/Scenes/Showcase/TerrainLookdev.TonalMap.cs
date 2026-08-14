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
        private const byte TerrainLimestoneAccent = 25;
        private bool _tonalOverlayApplied;

        private void Start()
        {
            ConfigureTurfPresentation();

            VoxelRenderBridge.SunDirection = new Vector3(-0.50f, 0.81f, -0.31f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(1.00f, 0.93f, 0.62f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.88f, 0.87f, 0.58f, 1f);

            ApplyTonalOverlay();
            ApplyVegetatedCap();
            ApplyForegroundContrastAccents();
            ApplyVisibleDetails();
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

            SetTurfPresentation(TerrainTurfNear, new Color(0.225f, 0.315f, 0.130f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfMid,  new Color(0.390f, 0.460f, 0.185f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfFar,  new Color(0.620f, 0.610f, 0.275f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Grass,       new Color(0.360f, 0.440f, 0.175f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Moss,        new Color(0.175f, 0.260f, 0.095f), grassSampling, grassSurface);

            // Rock placement now keeps limestone boxes above the local terrain instead of cutting
            // wide shallow boxes through hillsides. With the structural source of the contour
            // bands removed, use the pale warm limestone from the reference again.
            SetMaterialPresentation(Mat.TerrainLimestone,
                new Color(0.720f, 0.650f, 0.465f), 0.22f, 0.12f, 0.80f, 0.020f);
            SetMaterialPresentation(TerrainLimestoneAccent,
                new Color(0.720f, 0.650f, 0.465f), 0.22f, 0.12f, 0.80f, 0.020f);
            SetMaterialPresentation(Mat.TerrainPathStone,
                new Color(0.615f, 0.550f, 0.345f), 0.16f, 0.08f, 0.90f, 0.014f);
            SetMaterialPresentation(Mat.Sand,
                new Color(0.545f, 0.505f, 0.285f), 0.10f, 0.05f, 0.92f, 0.010f);
            SetMaterialPresentation(Mat.TerrainEarth,
                new Color(0.315f, 0.365f, 0.155f), 0.00f, 0.025f, 0.92f, 0.012f);

            SetMaterialPresentation(Mat.FlowerWhite,
                new Color(0.965f, 0.940f, 0.790f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerYellow,
                new Color(0.950f, 0.790f, 0.180f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerPink,
                new Color(0.950f, 0.580f, 0.640f), 0f, 0f, 0.88f, 0f);
            SetMaterialPresentation(Mat.FlowerBlue,
                new Color(0.560f, 0.760f, 0.820f), 0f, 0f, 0.88f, 0f);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, 0.10f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, 0.06f, 0.90f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.014f, 0.007f, 0.010f);
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

            float tone = depth + field * 0.17f;
            if (tone < 0.31f) return TerrainTurfNear;
            if (tone > 0.64f) return TerrainTurfFar;
            return TerrainTurfMid;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 95 && fromPath > 24)
            {
                float mossField = math.sin(x * 0.055f + z * 0.031f)
                                + 0.55f * math.sin(x * 0.024f - z * 0.047f + 1.2f);
                if (mossField > 0.58f) return Coatings.Moss;
            }
            return Coatings.None;
        }
    }
}
