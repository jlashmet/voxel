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
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(TerrainTurfNear, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, default, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, default, SurfaceStyles.Smooth, weather);
            Vector4 sampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 surface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];
            SetTurfPresentation(TerrainTurfNear, new Color(0.18f, 0.25f, 0.095f), sampling, surface);
            SetTurfPresentation(TerrainTurfMid, new Color(0.38f, 0.43f, 0.17f), sampling, surface);
            SetTurfPresentation(TerrainTurfFar, new Color(0.62f, 0.59f, 0.27f), sampling, surface);
        }

        private static void SetTurfPresentation(byte material, Color colour, Vector4 sampling, Vector4 surface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] = new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] = new Vector4(sampling.x, sampling.y, sampling.z, 0.04f);
            VoxelPresentationCatalogue.MaterialSurface[material] = new Vector4(surface.x, 0.02f, 0.84f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] = new Vector4(0.68f, 0.004f, 0f, 0.003f);
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
            return Mathf.RoundToInt(math.min(relief, 7f));
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
            float macro = math.sin(x * 0.018f + z * 0.010f)
                        + 0.65f * math.sin(x * 0.010f - z * 0.016f + 1.7f)
                        + 0.35f * math.sin((x + z) * 0.007f - 0.8f);
            if (depth < 0.28f) return macro < 0.18f ? TerrainTurfNear : TerrainTurfMid;
            if (depth < 0.60f)
            {
                if (macro < -0.72f) return TerrainTurfNear;
                if (macro > 0.58f) return TerrainTurfFar;
                return TerrainTurfMid;
            }
            return macro < -0.78f ? TerrainTurfMid : TerrainTurfFar;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            return z < 10 && fromPath > 22 ? Coatings.Moss : Coatings.None;
        }
    }
}
