using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
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

        /// <summary>
        /// Bits 8..15 carry ambient-occlusion strength. Bits 24..31 carry a reusable
        /// Transvoxel transition-face tag: zero for ordinary geometry, face+1 for seams.
        /// The active six-bit face mask is supplied per draw so LOD changes do not remesh.
        /// </summary>
        public uint Active;
        public const int TransitionTagShift = 24;
        public const uint TransitionTagMask = 0xFF000000u;
        public const int Stride = 32;
    }
}
