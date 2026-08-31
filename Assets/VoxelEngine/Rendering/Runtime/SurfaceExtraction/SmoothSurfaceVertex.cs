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
        /// Water extraction uses the generic topology flags below only for water vertices; the
        /// low-byte material identity remains opaque and unchanged.
        /// </summary>
        public uint Material;

        public uint Active;

        public const uint BaseMaterialMask = 0x000000FFu;
        public const uint WaterLipFlag = 0x01000000u;
        public const uint WaterImpactFlag = 0x02000000u;
        public const uint WaterEdgeFlag = 0x04000000u;
        public const int Stride = 32;
    }
}
