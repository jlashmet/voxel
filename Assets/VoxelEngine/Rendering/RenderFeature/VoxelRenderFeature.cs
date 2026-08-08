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
        [Tooltip("Off by default: the raymarch is not yet verified to render correctly, and a " +
                 "renderer feature runs in the editor as well as in play mode.")]
        [SerializeField] private bool m_Enabled;

        [Header("Raymarch")]
        [Tooltip("BrickRaymarch.compute")]
        [SerializeField] private ComputeShader m_Raymarch;

        [Header("Stylized surface textures")]
        [SerializeField] private Texture2D m_StoneTexture;
        [SerializeField] private Texture2D m_WoodTexture;
        [SerializeField] private Texture2D m_SandTexture;
        [SerializeField] private Texture2D m_RockTexture;
        [SerializeField] private Texture2D m_SlateTexture;
        [SerializeField] private Texture2D m_GrassTexture;
        [SerializeField] private Texture2D m_DirtTexture;

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

        /// <summary>
        /// Called by URP on every domain reload and on every inspector edit of this asset.
        ///
        /// The previous pass must be disposed here. It owns roughly 180 MB of ComputeBuffer,
        /// and ComputeBuffer is not released by the garbage collector — dropping the reference
        /// leaks the memory for the life of the process. Recreating without disposing leaked
        /// that much per script compile, which is enough to exhaust a machine in an afternoon.
        /// </summary>
        public override void Create()
        {
            m_Pass?.Dispose();

            m_Pass = new VoxelRenderPass();
            m_Pass.Setup(m_Raymarch, m_StoneTexture, m_WoodTexture, m_SandTexture,
                         m_RockTexture, m_SlateTexture, m_GrassTexture, m_DirtTexture);
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

        /// <summary>The current pass. Public so a lifecycle test can watch what Create() does.</summary>
        public VoxelRenderPass Pass => m_Pass;
    }
}
