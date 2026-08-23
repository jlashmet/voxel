using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Compacts deterministic topology lanes away from the render thread. A boundary lane may
    /// contain more than one cell record because it also owns cells from the chunk's negative
    /// shell; each record carries its own vertex/index counts and is appended independently.
    /// </summary>
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
                int remaining = Input.BeginForEachIndex(cell);
                while (remaining >= 3)
                {
                    byte overflow = Input.Read<byte>();
                    int vertexCount = Input.Read<byte>();
                    int indexCount = Input.Read<byte>();
                    remaining -= 3;

                    if (overflow != 0 || remaining < vertexCount + indexCount)
                    {
                        OverflowCell[0] = cell;
                        Input.EndForEachIndex();
                        return;
                    }

                    uint destinationBase = (uint)Vertices.Length;
                    for (int i = 0; i < vertexCount; i++)
                        Vertices.Add(Input.Read<SmoothSurfaceVertex>());
                    for (int i = 0; i < indexCount; i++)
                        Indices.Add(destinationBase + Input.Read<byte>());
                    remaining -= vertexCount + indexCount;
                }

                // A partial header can only come from a malformed producer. Treat it like the
                // existing per-cell overflow path instead of publishing truncated geometry.
                if (remaining != 0)
                {
                    OverflowCell[0] = cell;
                    Input.EndForEachIndex();
                    return;
                }
                Input.EndForEachIndex();
            }
        }
    }
}
