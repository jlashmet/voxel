using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>Shared vertex contract produced by the unified solid surface extractor.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SmoothSurfaceVertex
    {
        public Vector3 Position;
        public Vector3 Normal;

        /// <summary>
        /// Base material in bits 0..7, coating in 8..15, style in 16..23, flags in 24..31.
        /// </summary>
        public uint Material;

        public uint Active;
        public const int Stride = 32;
    }
}
