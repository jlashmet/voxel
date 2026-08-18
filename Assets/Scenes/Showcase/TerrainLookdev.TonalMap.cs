using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

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

            VoxelEngineBootstrap.ConfigureRenderingSky(
                new Vector3(-0.50f, 0.81f, -0.31f).normalized,
                new Color(0.96f, 0.91f, 0.61f, 1f),
                new Color(0.82f, 0.84f, 0.54f, 1f));

            ApplyTonalOverlay();
            ApplyVegetatedCap();
            ApplyReferenceDetails();
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _storage.RegisterMaterial(TerrainTurfNear, 24, default, SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(TerrainTurfMid, 24, default, SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(TerrainTurfFar, 24, default, SurfaceStyles.Smooth, weather);
            _storage.RegisterMaterial(TerrainLimestoneAccent, 210, default, SurfaceStyles.Planar, weather);

            // Read the grass template before overriding the grass row itself. Moss is configured
            // first so every turf row receives the same original sampling/surface template.
            VoxelEngineBootstrap.ConfigureTurfMaterialPresentation(
                TerrainTurfNear, Mat.Grass, new Color(0.34f, 0.43f, 0.16f));
            VoxelEngineBootstrap.ConfigureTurfMaterialPresentation(
                TerrainTurfMid, Mat.Grass, new Color(0.39f, 0.48f, 0.18f));
            VoxelEngineBootstrap.ConfigureTurfMaterialPresentation(
                TerrainTurfFar, Mat.Grass, new Color(0.44f, 0.51f, 0.20f));
            VoxelEngineBootstrap.ConfigureTurfMaterialPresentation(
                Mat.Moss, Mat.Grass, new Color(0.24f, 0.34f, 0.12f));
            VoxelEngineBootstrap.ConfigureTurfMaterialPresentation(
                Mat.Grass, Mat.Grass, new Color(0.39f, 0.48f, 0.18f));

            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.TerrainLimestone,
                new Color(0.69f, 0.64f, 0.47f), 0.24f, 0.11f, 0.82f, 0.018f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(TerrainLimestoneAccent,
                new Color(0.76f, 0.70f, 0.53f), 0.28f, 0.13f, 0.80f, 0.020f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.TerrainPathStone,
                new Color(0.66f, 0.59f, 0.41f), 0.18f, 0.09f, 0.88f, 0.014f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.Sand,
                new Color(0.56f, 0.51f, 0.31f), 0.10f, 0.05f, 0.92f, 0.010f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.TerrainEarth,
                new Color(0.34f, 0.38f, 0.17f), 0.02f, 0.025f, 0.92f, 0.010f);

            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.FlowerWhite,
                new Color(0.98f, 0.96f, 0.84f), 0f, 0f, 0.88f, 0f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.FlowerYellow,
                new Color(0.96f, 0.80f, 0.20f), 0f, 0f, 0.88f, 0f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.FlowerPink,
                new Color(0.96f, 0.61f, 0.67f), 0f, 0f, 0.88f, 0f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(Mat.FlowerBlue,
                new Color(0.57f, 0.77f, 0.84f), 0f, 0f, 0.88f, 0f);
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
