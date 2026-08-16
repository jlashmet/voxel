using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Persistent render-residency identity for one logical surface chunk. Geometry build
    /// workspaces are reusable; they may publish only while the slot generation captured at
    /// admission still matches this object. Recycling a slot therefore invalidates every stale
    /// in-flight result without waiting for it or relying on coordinate identity alone.
    /// </summary>
    internal struct SurfaceChunkSlot
    {
        public int3 Coordinate { get; private set; }
        public uint Generation { get; private set; }

        public void Reinitialize(int3 coordinate, uint generation)
        {
            Coordinate = coordinate;
            Generation = generation == 0 ? 1u : generation;
        }

        public void Retire()
        {
            Coordinate = default;
            Generation = 0;
        }
    }
}
