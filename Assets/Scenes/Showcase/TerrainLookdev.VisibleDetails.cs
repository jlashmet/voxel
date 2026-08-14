using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private bool _visibleDetailsApplied;

        private void ApplyVisibleDetails()
        {
            if (!_built || _visibleDetailsApplied) return;

            var writer = new VoxelBrush(_table, _pool, in _palette, 500_000);
            var rng = new Unity.Mathematics.Random(Seed ^ 0xE47Du);
            for (int i = 0; i < 900; i++)
            {
                int z = rng.NextInt(-52, 500);
                float depth = math.saturate((z + 52f) / 552f);
                if (rng.NextFloat() < depth * 0.22f) continue;

                int x = rng.NextInt(TerrainXMin + 6, TerrainXMax - 6);
                if (z < 210 && math.abs(x - PathCenterVoxel(z)) < 10) continue;

                int top = FinalTerrainTopVoxel(x, z);
                byte flower = Mat.FlowerWhite;
                float colour = rng.NextFloat();
                if (colour > 0.72f && colour <= 0.89f) flower = Mat.FlowerYellow;
                else if (colour > 0.89f && colour <= 0.97f) flower = Mat.FlowerPink;
                else if (colour > 0.97f) flower = Mat.FlowerBlue;

                if (z < 185)
                {
                    writer.SetStyled(x, top + 1, z, Mat.Moss, SurfaceStyles.Smooth);
                    writer.SetStyled(x, top + 2, z, flower, SurfaceStyles.Rounded);
                    if (z < 90 && rng.NextFloat() < 0.34f)
                    {
                        int dx = rng.NextBool() ? 1 : -1;
                        writer.SetStyled(x + dx, top + 2, z, flower, SurfaceStyles.Rounded);
                    }
                }
                else
                {
                    writer.SetStyled(x, top + 1, z, flower, SurfaceStyles.Rounded);
                }
            }

            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _visibleDetailsApplied = true;
        }
    }
}
