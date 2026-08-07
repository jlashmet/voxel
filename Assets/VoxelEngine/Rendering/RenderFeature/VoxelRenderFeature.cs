using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// URP renderer feature that injects the brickmap raymarch.
    ///
    /// The compute shader is a serialized reference rather than a <c>Shader.Find</c> or a
    /// <c>Resources.Load</c>: a renderer feature is an asset, and an asset reference is the only
    /// form that survives a build without a Resources folder or a stripped shader.
    /// </summary>
    public class VoxelRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private bool m_Enabled = true;

        [Header("Raymarch")]
        [Tooltip("BrickRaymarch.compute")]
        [SerializeField] private ComputeShader m_Raymarch;

        [SerializeField] private RenderPassEvent m_Event = RenderPassEvent.BeforeRenderingTransparents;

        [Header("Presentation (DeviceTierBudget)")]
        [SerializeField] private float m_RenderScale = 1.0f;

        [Tooltip("Metres per voxel. device-matrix.md: 10 cm.")]
        [SerializeField] private float m_VoxelSize = 0.1f;

        [Tooltip("How far rays march before giving up on the mip-0 brickmap.")]
        [SerializeField] private float m_MaxDistance = 400f;

        [Tooltip("Step budget per ray. device-matrix.md: 256 on PC, 128 on Mobile-HE.")]
        [SerializeField] private int m_MaxSteps = 256;

        private VoxelRenderPass m_Pass;

        public override void Create()
        {
            m_Pass = new VoxelRenderPass();
            m_Pass.Setup(m_Raymarch);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_Enabled || m_Pass == null) return;

            m_Pass.renderPassEvent = m_Event;
            m_Pass.Enabled = m_Enabled;
            m_Pass.RenderScale = m_RenderScale;
            m_Pass.VoxelSize = m_VoxelSize;
            m_Pass.MaxDistance = m_MaxDistance;
            m_Pass.MaxSteps = m_MaxSteps;

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing) => m_Pass?.Dispose();

        /// <summary>Bricks uploaded to the GPU on the most recent frame.</summary>
        public int LastBricksUploaded => m_Pass?.LastBricksUploaded ?? 0;

        /// <summary>Region slots currently mirrored on the GPU.</summary>
        public int ResidentSlots => m_Pass?.ResidentSlots ?? 0;
    }
}
