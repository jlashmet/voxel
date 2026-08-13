using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _tonalOverlayApplied;

        private void Start()
        {
            VoxelRenderBridge.SunDirection = new Vector3(-0.18f, 0.95f, -0.25f).normalized;
            ApplyTonalOverlay();
        }

        private void ApplyTonalOverlay()
        {
            if (!_built || _tonalOverlayApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 1_500_000);
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

        private static byte GroundToneMaterial(int x, int z)
        {
            if (z < 45)
                return Mat.Moss;

            if (z > 125)
            {
                float depth = math.saturate((z - 125f) / 420f);
                float warmChance = math.lerp(0.16f, 0.52f, depth);
                if (Hash01(x, z) < warmChance)
                    return Mat.Sand;
            }

            return Mat.Grass;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 5 && fromPath > 18)
                return Coatings.Moss;
            if (z < 55 && fromPath > 34)
                return Coatings.Moss;
            if (z < 105 && fromPath > 82)
                return Coatings.Moss;
            return Coatings.None;
        }
    }
}
