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
            _palette.Register(TerrainTurfNear, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);

            Vector4 sourceSampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 sourceSurface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];
            SetTurfPresentation(TerrainTurfNear, new Color(0.23f, 0.25f, 0.13f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfMid, new Color(0.45f, 0.43f, 0.23f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfFar, new Color(0.70f, 0.59f, 0.35f), sourceSampling, sourceSurface);
        }

        private static void SetTurfPresentation(byte material, Color colour,
            Vector4 sourceSampling, Vector4 sourceSurface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] =
                new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sourceSampling.x, sourceSampling.y, sourceSampling.z, 0.04f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(sourceSurface.x, 0.02f, 0.84f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.004f, 0f, 0.003f);
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
                if (relief > 0)
                    writer.FillColumnBulk(x, baseTop + 1, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, GroundToneMaterial(x, z), SurfaceStyles.Smooth,
                    GroundToneCoating(x, z));
            }
            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain tonal overlay exceeded voxel authoring budget.");
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
            float fromPath = math.abs(x - PathCenterVoxel(z));
            float side = math.clamp((x - PathCenterVoxel(z)) / 150f, -1f, 1f);
            float macro = math.sin(x * 0.018f + z * 0.010f)
                        + 0.65f * math.sin(x * 0.010f - z * 0.016f + 1.7f)
                        + 0.35f * math.sin((x + z) * 0.007f - 0.8f);

            // Foreground: the reference has a darker centre route framed by brighter shoulders.
            if (depth < 0.28f)
            {
                float centreBias = math.saturate(1f - fromPath / 58f);
                return macro < 0.05f + centreBias * 0.85f ? TerrainTurfNear : TerrainTurfMid;
            }

            // Midground: its left bank is generally warmer/brighter while the right bank carries
            // the largest dark vegetation masses. Preserve that broad asymmetry without speckle.
            if (depth < 0.60f)
            {
                float darkCut = -0.76f + side * 0.62f;
                float brightCut = 0.72f + side * 0.72f;
                if (macro < darkCut) return TerrainTurfNear;
                if (macro > brightCut) return TerrainTurfFar;
                return TerrainTurfMid;
            }

            // The upper third of the source is predominantly bright yellow-green; isolated darker
            // structure should come from terrain and rocks rather than broad dark turf regions.
            return TerrainTurfFar;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 10 && fromPath > 22) return Coatings.Moss;
            return Coatings.None;
        }
    }
}
