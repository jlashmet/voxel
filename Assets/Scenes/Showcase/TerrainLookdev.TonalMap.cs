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
            VoxelRenderBridge.SunDirection = new Vector3(-0.18f, 0.95f, -0.25f).normalized;
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
            SetTurfPresentation(TerrainTurfNear, new Color(0.27f, 0.34f, 0.15f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfMid, new Color(0.40f, 0.43f, 0.20f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfFar, new Color(0.53f, 0.51f, 0.25f), sourceSampling, sourceSurface);
        }

        private static void SetTurfPresentation(byte material, Color colour,
            Vector4 sourceSampling, Vector4 sourceSurface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] =
                new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sourceSampling.x, sourceSampling.y, sourceSampling.z, 0.05f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(sourceSurface.x, 0.025f, 0.82f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.006f, 0f, 0.004f);
        }

        private void ApplyTonalOverlay()
        {
            if (!_built || _tonalOverlayApplied) return;
            var writer = new VoxelBrush(_table, _pool, in _palette, 1_500_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int top = HeightVoxel(x, z);
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

        private static byte GroundToneMaterial(int x, int z)
        {
            if (z < 20) return TerrainTurfNear;
            if (z < 165) return TerrainTurfMid;
            if (z < 225)
            {
                float farBlend = math.saturate((z - 165f) / 60f);
                return Hash01(x, z) < farBlend ? TerrainTurfFar : TerrainTurfMid;
            }
            return TerrainTurfFar;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 0 && fromPath > 14) return Coatings.Moss;
            return Coatings.None;
        }
    }
}
