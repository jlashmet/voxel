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

            // The reference is lit from the side enough for every turf mound and limestone block
            // to cast a readable value change. The previous almost-overhead sun flattened the
            // scene and forced colour noise to do work that lighting should be doing.
            VoxelRenderBridge.SunDirection = new Vector3(-0.54f, 0.76f, -0.34f).normalized;

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

            // Quaternius-style nature scenes get their readability from clean clustered shapes,
            // not per-pixel colour confetti. Keep three broad turf families, with enough value
            // separation for depth but close enough that their boundaries still read as turf.
            SetTurfPresentation(TerrainTurfNear, new Color(0.205f, 0.285f, 0.125f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfMid,  new Color(0.335f, 0.405f, 0.175f), grassSampling, grassSurface);
            SetTurfPresentation(TerrainTurfFar,  new Color(0.555f, 0.555f, 0.255f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Grass,       new Color(0.315f, 0.390f, 0.165f), grassSampling, grassSurface);
            SetTurfPresentation(Mat.Moss,        new Color(0.165f, 0.245f, 0.095f), grassSampling, grassSurface);

            // Warm pale limestone and muted path stone are major compositional masses in the
            // reference. Keep production triplanar/normal sampling, but make the presentation
            // clean enough that the rounded voxel geometry remains the dominant signal.
            SetMaterialPresentation(Mat.TerrainLimestone,
                new Color(0.685f, 0.615f, 0.430f), 0.08f, 0.08f, 0.80f, 0.010f);
            SetMaterialPresentation(Mat.TerrainPathStone,
                new Color(0.565f, 0.500f, 0.310f), 0.06f, 0.04f, 0.90f, 0.008f);
            SetMaterialPresentation(Mat.Sand,
                new Color(0.515f, 0.475f, 0.265f), 0.05f, 0.03f, 0.92f, 0.006f);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sampling.x, sampling.y, sampling.z, 0.045f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(surface.x, 0.045f, 0.90f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.008f, 0.004f, 0.008f);
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

            // This pass is deliberately presentation-only now. The old version raised the final
            // ground by up to seven voxels AFTER rocks, path stones and flowers were authored,
            // burying much of the actual detail we wanted to see. Macro geometry belongs in the
            // base height field, before props are placed.
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

            // Coherent domain-warped bands create broad ecological fields. Crucially, material
            // choice is NOT independently hashed per voxel; that was the source of the noisy
            // carpet in run 63.
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
            if (z < 60 && fromPath > 26)
            {
                float mossField = math.sin(x * 0.055f + z * 0.031f)
                                + 0.55f * math.sin(x * 0.024f - z * 0.047f + 1.2f);
                if (mossField > 0.72f) return Coatings.Moss;
            }
            return Coatings.None;
        }
    }
}
