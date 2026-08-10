using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// URP renderer feature that injects continuous GPU-authored surface meshes for voxel display.
    ///
    /// Shaders, compute shaders, and textures are serialized references rather than <c>Shader.Find</c>
    /// or <c>Resources.Load</c>: a renderer feature is an asset, and an asset reference is the only
    /// form that survives a build without a Resources folder or a stripped shader.
    /// </summary>
    public class VoxelRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("Off by default: continuous voxel surface rendering.")]
        [SerializeField] private bool m_Enabled;

        [Header("Continuous GPU surface")]
        [Tooltip("SmoothSurface.compute")]
        [SerializeField] private ComputeShader m_SurfaceExtraction;

        [Tooltip("Hidden/VoxelEngine/SmoothSurface")]
        [SerializeField] private Shader m_SurfaceShader;

        // TEMP: density generation still lives in BrickRaymarch.compute.
        [Tooltip("BrickRaymarch.compute (only CSBuildDensity kernel)")]
        [SerializeField] private ComputeShader m_DensityCompute;

        [Header("Stylized surface textures")]
        [SerializeField] private Texture2D m_StoneTexture;
        [SerializeField] private Texture2D m_WoodTexture;
        [SerializeField] private Texture2D m_SandTexture;
        [SerializeField] private Texture2D m_RockTexture;
        [SerializeField] private Texture2D m_SlateTexture;
        [SerializeField] private Texture2D m_GrassTexture;
        [SerializeField] private Texture2D m_DirtTexture;
        [SerializeField] private Texture2D m_DarkStoneTexture;

        [Header("Stylized surface normals")]
        [SerializeField] private Texture2D m_StoneNormal;
        [SerializeField] private Texture2D m_WoodNormal;
        [SerializeField] private Texture2D m_SandNormal;
        [SerializeField] private Texture2D m_RockNormal;
        [SerializeField] private Texture2D m_SlateNormal;
        [SerializeField] private Texture2D m_GrassNormal;
        [SerializeField] private Texture2D m_DirtNormal;
        [SerializeField] private Texture2D m_DarkStoneNormal;

        [Header("Authored sky")]
        [SerializeField] private Texture2D m_SkyTexture;

        [SerializeField] private RenderPassEvent m_Event = RenderPassEvent.BeforeRenderingTransparents;

        [Header("Presentation (DeviceTierBudget)")]
        [SerializeField] private float m_RenderScale = 1.0f;

        [Tooltip("Metres per voxel. device-matrix.md: 10 cm.")]
        [SerializeField] private float m_VoxelSize = 0.1f;

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
            m_Pass.Setup(m_SurfaceExtraction, m_SurfaceShader, m_DensityCompute,
                         m_StoneTexture, m_WoodTexture, m_SandTexture,
                         m_RockTexture, m_SlateTexture, m_GrassTexture, m_DirtTexture,
                         m_StoneNormal, m_WoodNormal, m_SandNormal, m_RockNormal,
                         m_SlateNormal, m_GrassNormal, m_DirtNormal,
                         m_DarkStoneTexture, m_DarkStoneNormal, m_SkyTexture);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_Enabled || m_Pass == null) return;

            m_Pass.renderPassEvent = m_Event;
            m_Pass.Enabled = m_Enabled;
            m_Pass.RenderScale = m_RenderScale;
            m_Pass.VoxelSize = m_VoxelSize;

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
