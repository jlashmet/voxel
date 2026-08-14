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
            VoxelRenderBridge.SunDirection = new Vector3(-0.08f, 0.96f, -0.26f).normalized;
            ApplyTonalOverlay();
            ApplyVisibleDetails();
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(TerrainTurfNear, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, default, SurfaceStyles.Smooth, weather);

            Vector4 sampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 surface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];

            // Keep neighbouring turf tones fairly close. Large jumps between these rows used to
            // turn low-frequency classification into visible rectangular colour slabs.
            SetTurfPresentation(TerrainTurfNear, new Color(0.205f, 0.270f, 0.125f), sampling, surface);
            SetTurfPresentation(TerrainTurfMid,  new Color(0.315f, 0.365f, 0.165f), sampling, surface);
            SetTurfPresentation(TerrainTurfFar,  new Color(0.430f, 0.445f, 0.215f), sampling, surface);

            // The authored cushions and any uncovered base terrain use these semantic rows too.
            SetTurfPresentation(Mat.Grass, new Color(0.300f, 0.355f, 0.155f), sampling, surface);
            SetTurfPresentation(Mat.Moss,  new Color(0.180f, 0.245f, 0.105f), sampling, surface);

            Vector4 sandSampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Sand];
            Vector4 sandSurface = VoxelPresentationCatalogue.MaterialSurface[Mat.Sand];
            VoxelPresentationCatalogue.MaterialAlbedo[Mat.Sand] = new Vector4(0.53f, 0.49f, 0.27f, 1f);
            VoxelPresentationCatalogue.MaterialSampling[Mat.Sand] = new Vector4(sandSampling.x, sandSampling.y, sandSampling.z, 0.10f);
            VoxelPresentationCatalogue.MaterialSurface[Mat.Sand] = new Vector4(sandSurface.x, 0.05f, 0.88f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[Mat.Sand] = new Vector4(0.68f, 0.025f, 0.02f, 0.020f);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            // Let the production triplanar texture contribute real micro detail instead of
            // flattening the surface to a debug-like solid colour.
            VoxelPresentationCatalogue.MaterialSampling[material] = new Vector4(sampling.x, sampling.y, sampling.z, 0.16f);
            VoxelPresentationCatalogue.MaterialSurface[material] = new Vector4(surface.x, 0.10f, 0.90f, 0f);
            // Fine + macro variation are evaluated continuously in world space by the production
            // smooth-surface shader, so they break repetition without creating region boundaries.
            VoxelPresentationCatalogue.MaterialVariation[material] = new Vector4(0.68f, 0.030f, 0.025f, 0.028f);
        }

        private void ApplyTonalOverlay()
        {
            if (!_built || _tonalOverlayApplied) return;
            var writer = new VoxelBrush(_table, _pool, in _palette, 2_200_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int baseTop = HeightVoxel(x, z);
                int relief = ReferenceReliefVoxels(x, z);
                int top = baseTop + relief;
                if (relief > 0) writer.FillColumnBulk(x, baseTop + 1, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, GroundToneMaterial(x, z), SurfaceStyles.Smooth, GroundToneCoating(x, z));
            }
            if (writer.BudgetExceeded) throw new System.InvalidOperationException("Terrain tonal overlay exceeded voxel authoring budget.");
            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _tonalOverlayApplied = true;
        }

        private static int FinalTerrainTopVoxel(int x, int z)
        {
            return HeightVoxel(x, z) + ReferenceReliefVoxels(x, z);
        }

        private static int ReferenceReliefVoxels(int x, int z)
        {
            float fromPath = math.abs(x - PathCenterVoxel(z));
            float centre = math.saturate(1f - fromPath / 82f);
            float distanceFade = 1f - 0.35f * math.saturate((z - 250f) / 230f);
            float relief = 3.8f * centre * centre * distanceFade;
            relief += 2.2f * SoftHill(x, z, -78, 62, 150, 158);
            relief += 1.6f * SoftHill(x, z, 104, 155, 170, 190);
            relief += 1.8f * SoftHill(x, z, -72, 302, 180, 205);
            relief += 1.2f * SoftHill(x, z, 118, 390, 188, 220);
            relief = math.min(relief, 7f);

            // Dither the unavoidable integer height quantisation. Deterministic stochastic
            // rounding turns long contour rings into small irregular transitions while preserving
            // the same expected terrain height and the same voxel storage/rendering path.
            int whole = (int)math.floor(relief);
            float fraction = relief - whole;
            return whole + (Hash01(x * 13 + 7, z * 17 - 11) < fraction ? 1 : 0);
        }

        private static float SoftHill(int x, int z, int cx, int cz, int rx, int rz)
        {
            float dx = (x - cx) / (float)rx;
            float dz = (z - cz) / (float)rz;
            float q = dx * dx + dz * dz;
            if (q >= 1f) return 0f;
            float t = 1f - q;
            return t * t * (3f - 2f * t);
        }

        private static byte GroundToneMaterial(int x, int z)
        {
            float depth = math.saturate((z + 70f) / 630f);

            // A gently domain-warped field biases the palette, but selection is dithered per voxel.
            // This preserves broad warm/cool drift without the continent-sized hard-edged islands
            // that made the previous render look like a debug splat map.
            float warpX = math.sin(z * 0.031f + 0.7f) * 9.0f;
            float warpZ = math.sin(x * 0.027f - 1.1f) * 11.0f;
            float field = 0.55f * math.sin((x + warpX) * 0.021f + (z + warpZ) * 0.014f)
                        + 0.30f * math.sin((x - warpZ) * 0.043f - (z + warpX) * 0.029f + 1.8f);

            float warm = math.saturate(0.14f + depth * 0.34f + field * 0.10f);
            float dark = math.saturate(0.24f - depth * 0.16f - field * 0.08f);
            float pick = Hash01(x * 29 + 31, z * 37 - 19);

            if (pick < dark) return TerrainTurfNear;
            if (pick > 1f - warm) return TerrainTurfFar;
            return TerrainTurfMid;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 35 && fromPath > 22 && Hash01(x * 7, z * 11) < 0.34f)
                return Coatings.Moss;
            return Coatings.None;
        }
    }
}
