using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// URP renderer feature for the derived voxel-world presentation.
    ///
    /// World surfaces remain one raster architecture: hard structures, smooth terrain, and water
    /// are derived from the same authoritative sparse voxels. The authored sky is a separate
    /// background pass because mesh rasterization no longer owns camera-miss pixels.
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
        [Tooltip("Hidden/VoxelEngine/WaterSurface")]
        [SerializeField] private Shader m_WaterShader;
        [Tooltip("BrickRaymarch.compute; temporary owner of CSBuildDensity during the surface migration.")]
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
        [Tooltip("Hidden/VoxelEngine/AuthoredSky")]
        [SerializeField] private Shader m_SkyShader;
        [SerializeField] private Texture2D m_SkyTexture;

        [SerializeField] private RenderPassEvent m_Event = RenderPassEvent.BeforeRenderingTransparents;

        [Header("Presentation (DeviceTierBudget)")]
        [SerializeField] private float m_RenderScale = 1.0f;

        [Tooltip("Metres per voxel. device-matrix.md: 10 cm.")]
        [SerializeField] private float m_VoxelSize = 0.1f;

        private VoxelSkyPass m_SkyPass;
        private VoxelRenderPass m_Pass;

        /// <summary>
        /// Called by URP on every domain reload and on every inspector edit of this asset.
        /// All passes own GPU resources/materials and must be disposed before recreation.
        /// </summary>
        public override void Create()
        {
            m_SkyPass?.Dispose();
            m_Pass?.Dispose();

            m_SkyPass = new VoxelSkyPass();
            m_SkyPass.Setup(m_SkyShader, m_SkyTexture);

            m_Pass = new VoxelRenderPass();
            m_Pass.Setup(m_SurfaceExtraction, m_SurfaceShader, m_WaterShader,
                         m_StoneTexture, m_WoodTexture, m_SandTexture,
                         m_RockTexture, m_SlateTexture, m_GrassTexture, m_DirtTexture,
                         m_StoneNormal, m_WoodNormal, m_SandNormal, m_RockNormal,
                         m_SlateNormal, m_GrassNormal, m_DirtNormal,
                         m_DarkStoneTexture, m_DarkStoneNormal, m_SkyTexture,
                         m_DensityCompute);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!m_Enabled || m_Pass == null) return;

            if (m_SkyPass != null)
            {
                // URP's normal skybox runs at BeforeRenderingSkybox. Draw immediately afterward
                // and replace only pixels whose depth is still at the exact far-clear value.
                m_SkyPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                m_SkyPass.Enabled = m_Enabled;
                renderer.EnqueuePass(m_SkyPass);
            }

            m_Pass.renderPassEvent = m_Event;
            m_Pass.Enabled = m_Enabled;
            m_Pass.RenderScale = m_RenderScale;
            m_Pass.VoxelSize = m_VoxelSize;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_SkyPass?.Dispose();
            m_SkyPass = null;
            m_Pass?.Dispose();
            m_Pass = null;
        }

        public int LastBricksUploaded => m_Pass?.LastBricksUploaded ?? 0;
        public int ResidentSlots => m_Pass?.ResidentSlots ?? 0;
        public VoxelRenderPass Pass => m_Pass;
    }
}
