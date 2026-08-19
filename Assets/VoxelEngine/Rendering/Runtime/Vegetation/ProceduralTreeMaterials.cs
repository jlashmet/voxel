using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>Shared presentation resources for standing trees and detached tree debris.</summary>
    public static class ProceduralTreeMaterials
    {
        private static Material s_Bark;
        private static Material s_Leaves;
        private static Material s_Impostor;
        private static Material[] s_Shared;
        private static bool s_ReportedMissing;

        public static Material Bark
        {
            get { Ensure(); return s_Bark; }
        }

        public static Material Leaves
        {
            get { Ensure(); return s_Leaves; }
        }

        public static Material Impostor
        {
            get { Ensure(); return s_Impostor; }
        }

        public static Material[] Shared
        {
            get { Ensure(); return s_Shared; }
        }

        public static bool Ensure()
        {
            if (s_Bark != null && s_Leaves != null && s_Impostor != null) return true;

            Shader bark = Shader.Find("VoxelEngine/ProceduralTreeBark");
            Shader leaves = Shader.Find("VoxelEngine/ProceduralTreeLeaves");
            Shader impostor = Shader.Find("VoxelEngine/ProceduralTreeImpostor");
            if (bark == null || leaves == null || impostor == null)
            {
                // Report once. Ensure() runs from a per-frame lighting update and from every
                // vegetation spawn, so logging on each failure cost 33,717 stack-traced errors in
                // twenty seconds of a player build and took the frame rate from 688 to 30 — the
                // diagnostic was an order of magnitude more expensive than the thing it described.
                if (!s_ReportedMissing)
                {
                    s_ReportedMissing = true;
                    if (bark == null) Debug.LogError("Procedural tree bark shader was not found.");
                    if (leaves == null) Debug.LogError("Procedural tree leaf shader was not found.");
                    if (impostor == null)
                        Debug.LogError("Procedural tree impostor shader was not found.");
                }
                return false;
            }

            s_ReportedMissing = false;

            s_Bark = new Material(bark)
            {
                name = "Procedural Tree Bark (Shared Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            s_Leaves = new Material(leaves)
            {
                name = "Procedural Tree Leaves (Shared Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            s_Impostor = new Material(impostor)
            {
                name = "Procedural Tree Impostor (Shared Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            s_Shared = new[] { s_Bark, s_Leaves };
            return true;
        }

        public static void ApplyLighting()
        {
            if (!Ensure()) return;
            Vector3 sun = VoxelRenderBridge.SunDirection;
            Color horizon = VoxelRenderBridge.SkyHorizon;
            Color zenith = VoxelRenderBridge.SkyZenith;

            ApplyLighting(s_Bark, sun, horizon, zenith);
            ApplyLighting(s_Leaves, sun, horizon, zenith);
            ApplyLighting(s_Impostor, sun, horizon, zenith);
        }

        private static void ApplyLighting(Material material, Vector3 sun,
                                          Color horizon, Color zenith)
        {
            material.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0f));
            material.SetColor("_SkyHorizon", horizon);
            material.SetColor("_SkyZenith", zenith);
        }
    }
}
