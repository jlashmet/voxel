using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>Compacts deterministic per-cell topology lanes away from the render thread.</summary>
    [BurstCompile]
    internal struct TransvoxelCompactJob : IJob
    {
        [ReadOnly] public NativeStream.Reader Input;
        public NativeList<SmoothSurfaceVertex> Vertices;
        public NativeList<uint> Indices;
        public NativeArray<int> OverflowCell;

        public void Execute()
        {
            Vertices.Clear();
            Indices.Clear();
            OverflowCell[0] = -1;
            for (int cell = 0; cell < Input.ForEachCount; cell++)
            {
                int itemCount = Input.BeginForEachIndex(cell);
                if (itemCount < 3)
                {
                    Input.EndForEachIndex();
                    continue;
                }
                byte overflow = Input.Read<byte>();
                int vertexCount = Input.Read<byte>();
                int indexCount = Input.Read<byte>();
                if (overflow != 0)
                {
                    OverflowCell[0] = cell;
                    Input.EndForEachIndex();
                    return;
                }
                uint destinationBase = (uint)Vertices.Length;
                for (int i = 0; i < vertexCount; i++) Vertices.Add(Input.Read<SmoothSurfaceVertex>());
                for (int i = 0; i < indexCount; i++) Indices.Add(destinationBase + Input.Read<byte>());
                Input.EndForEachIndex();
            }
        }
    }
}
