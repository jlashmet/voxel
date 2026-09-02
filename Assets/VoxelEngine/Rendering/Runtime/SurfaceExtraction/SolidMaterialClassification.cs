using Unity.Burst;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Burst-safe projection of renderer presentation semantics used by solid extraction.
    /// Material IDs remain opaque: application composition installs the current water mask and
    /// every CPU/Burst/GPU staging path consumes the same classification.
    /// </summary>
    internal static class SolidMaterialClassification
    {
        private struct Context { }
        private struct WaterMaskKey { }

        private static readonly SharedStatic<uint> s_WaterMaterialMask =
            SharedStatic<uint>.GetOrCreate<Context, WaterMaskKey>();
        private static readonly int s_GpuWaterMaterialMask =
            Shader.PropertyToID("_SolidWaterMaterialMask");

        internal static uint WaterMaterialMask => s_WaterMaterialMask.Data;

        internal static void SetWaterMaterialMask(uint waterMaterialMask)
        {
            s_WaterMaterialMask.Data = waterMaterialMask;
            // This is shared presentation classification, not scene policy. The compute mesher
            // consumes the same mask through its shader uniform, so CPU/Burst and GPU occupancy
            // cannot silently disagree when composition changes which materials are water.
            Shader.SetGlobalInt(s_GpuWaterMaterialMask, unchecked((int)waterMaterialMask));
        }

        internal static bool IsSolid(byte material)
        {
            if (material == 0) return false;
            uint logical = material;
            return logical >= 32u || (s_WaterMaterialMask.Data & (1u << (int)logical)) == 0u;
        }
    }
}
