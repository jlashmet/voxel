using System.Runtime.InteropServices;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuBrickCacheRequestView
    {
        internal int OriginX;
        internal int OriginY;
        internal int OriginZ;
        internal int OutputBase;

        internal const int Stride = sizeof(int) * 4;
    }
}
