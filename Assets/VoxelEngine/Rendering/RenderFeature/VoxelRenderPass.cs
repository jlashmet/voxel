using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// Dispatches the brickmap raymarch and composites the result into the camera colour target.
    ///
    /// There is no geometry: the world reaches the screen as a compute dispatch that walks the
    /// same brick storage collision and replication use (Constitution Principle II). Cost scales
    /// with rays and steps, not with surface area, which is what makes distance affordable.
    ///
    /// The parameters this pass carries — render scale, step budget, detail radius — are
    /// presentation-only and come from DeviceTierBudget. Nothing here feeds world state.
    /// </summary>
    public sealed class VoxelRenderPass : ScriptableRenderPass, IDisposable
    {
        private const string k_PassName = "VoxelEngine.BrickRaymarch";

        private static readonly int s_RegionWindow = Shader.PropertyToID("g_RegionWindow");
        private static readonly int s_BrickRefs = Shader.PropertyToID("g_BrickRefs");
        private static readonly int s_BrickVoxels = Shader.PropertyToID("g_BrickVoxels");
        private static readonly int s_Colour = Shader.PropertyToID("g_Colour");
        private static readonly int s_InvViewProj = Shader.PropertyToID("g_InvViewProj");
        private static readonly int s_CameraPos = Shader.PropertyToID("g_CameraPos");
        private static readonly int s_WindowOrigin = Shader.PropertyToID("g_WindowOrigin");
        private static readonly int s_SunDirection = Shader.PropertyToID("g_SunDirection");
        private static readonly int s_SkyHorizon = Shader.PropertyToID("g_SkyHorizon");
        private static readonly int s_SkyZenith = Shader.PropertyToID("g_SkyZenith");
        private static readonly int s_TargetSize = Shader.PropertyToID("g_TargetSize");
        private static readonly int s_VoxelSize = Shader.PropertyToID("g_VoxelSize");
        private static readonly int s_MaxDistance = Shader.PropertyToID("g_MaxDistance");
        private static readonly int s_MaxSteps = Shader.PropertyToID("g_MaxSteps");
        private static readonly int s_WindowX = Shader.PropertyToID("g_WindowX");
        private static readonly int s_WindowY = Shader.PropertyToID("g_WindowY");
        private static readonly int s_WindowZ = Shader.PropertyToID("g_WindowZ");
        private static readonly int s_MaterialColours = Shader.PropertyToID("g_MaterialColours");
        private static readonly int s_StoneTexture = Shader.PropertyToID("g_StoneTexture");
        private static readonly int s_WoodTexture = Shader.PropertyToID("g_WoodTexture");
        private static readonly int s_SandTexture = Shader.PropertyToID("g_SandTexture");
        private static readonly int s_RockTexture = Shader.PropertyToID("g_RockTexture");
        private static readonly int s_SlateTexture = Shader.PropertyToID("g_SlateTexture");
        private static readonly int s_GrassTexture = Shader.PropertyToID("g_GrassTexture");
        private static readonly int s_DirtTexture = Shader.PropertyToID("g_DirtTexture");
        private static readonly int s_DarkStoneTexture = Shader.PropertyToID("g_DarkStoneTexture");
        private static readonly int s_StoneNormal = Shader.PropertyToID("g_StoneNormal");
        private static readonly int s_WoodNormal = Shader.PropertyToID("g_WoodNormal");
        private static readonly int s_SandNormal = Shader.PropertyToID("g_SandNormal");
        private static readonly int s_RockNormal = Shader.PropertyToID("g_RockNormal");
        private static readonly int s_SlateNormal = Shader.PropertyToID("g_SlateNormal");
        private static readonly int s_GrassNormal = Shader.PropertyToID("g_GrassNormal");
        private static readonly int s_DirtNormal = Shader.PropertyToID("g_DirtNormal");
        private static readonly int s_DarkStoneNormal = Shader.PropertyToID("g_DarkStoneNormal");
        private static readonly int s_SkyTexture = Shader.PropertyToID("g_SkyTexture");
        private static readonly int s_DebugMode = Shader.PropertyToID("g_DebugMode");
        private static readonly int s_TerrainSeed = Shader.PropertyToID("g_TerrainSeed");
        private static readonly int s_FarDistance = Shader.PropertyToID("g_FarDistance");
        private static readonly int s_FarBaseHeight = Shader.PropertyToID("g_FarBaseHeight");
        private static readonly int s_FarEnabled = Shader.PropertyToID("g_FarEnabled");
        private static readonly int s_CutawayEnabled = Shader.PropertyToID("g_CutawayEnabled");
        private static readonly int s_CutawayMinVoxel = Shader.PropertyToID("g_CutawayMinVoxel");
        private static readonly int s_CutawayMaxVoxel = Shader.PropertyToID("g_CutawayMaxVoxel");
        private static readonly int s_LocalLightCount = Shader.PropertyToID("g_LocalLightCount");
        private static readonly int s_LocalLights = Shader.PropertyToID("g_LocalLights");
        private static readonly int s_LocalLightColours = Shader.PropertyToID("g_LocalLightColours");

        private readonly VoxelGpuBuffers _buffers = new();
        private ComputeShader _raymarch;
        private int _kernel = -1;
        private Texture2D _stoneTexture;
        private Texture2D _woodTexture;
        private Texture2D _sandTexture;
        private Texture2D _rockTexture;
        private Texture2D _slateTexture;
        private Texture2D _grassTexture;
        private Texture2D _dirtTexture;
        private Texture2D _darkStoneTexture;
        private Texture2D _stoneNormal;
        private Texture2D _woodNormal;
        private Texture2D _sandNormal;
        private Texture2D _rockNormal;
        private Texture2D _slateNormal;
        private Texture2D _grassNormal;
        private Texture2D _dirtNormal;
        private Texture2D _darkStoneNormal;
        private Texture2D _skyTexture;

        public float RenderScale { get; set; } = 1f;
        public float VoxelSize { get; set; } = 0.1f;
        public float MaxDistance { get; set; } = 400f;
        public int MaxSteps { get; set; } = 256;
        public bool Enabled { get; set; } = true;

        /// <summary>Bricks uploaded on the most recent frame — surfaced for the HUD.</summary>
        public int LastBricksUploaded => _buffers.LastBricksUploaded;

        public int ResidentSlots => _buffers.ResidentSlots;

        /// <summary>
        /// The GPU mirror this pass owns. Public so a test can force allocation and then assert
        /// the memory comes back — the leak that took a machine down was invisible from outside.
        /// </summary>
        public VoxelGpuBuffers Buffers => _buffers;

        public void Setup(ComputeShader raymarch, Texture2D stoneTexture = null,
                          Texture2D woodTexture = null, Texture2D sandTexture = null,
                          Texture2D rockTexture = null, Texture2D slateTexture = null,
                          Texture2D grassTexture = null, Texture2D dirtTexture = null,
                          Texture2D stoneNormal = null, Texture2D woodNormal = null,
                          Texture2D sandNormal = null, Texture2D rockNormal = null,
                          Texture2D slateNormal = null, Texture2D grassNormal = null,
                          Texture2D dirtNormal = null, Texture2D darkStoneTexture = null,
                          Texture2D darkStoneNormal = null, Texture2D skyTexture = null)
        {
            _raymarch = raymarch;
            _stoneTexture = stoneTexture;
            _woodTexture = woodTexture;
            _sandTexture = sandTexture;
            _rockTexture = rockTexture;
            _slateTexture = slateTexture;
            _grassTexture = grassTexture;
            _dirtTexture = dirtTexture;
            _stoneNormal = stoneNormal;
            _woodNormal = woodNormal;
            _sandNormal = sandNormal;
            _rockNormal = rockNormal;
            _slateNormal = slateNormal;
            _grassNormal = grassNormal;
            _dirtNormal = dirtNormal;
            _darkStoneTexture = darkStoneTexture;
            _darkStoneNormal = darkStoneNormal;
            _skyTexture = skyTexture;
            _kernel = raymarch != null && raymarch.HasKernel("CSBrickRaymarch")
                ? raymarch.FindKernel("CSBrickRaymarch")
                : -1;
        }

        private class PassData
        {
            public ComputeShader Raymarch;
            public int Kernel;
            public VoxelGpuBuffers Buffers;
            public TextureHandle Colour;
            public TextureHandle CameraColour;
            public Matrix4x4 InvViewProj;
            public Vector3 CameraPos;
            public Vector3 WindowOrigin;
            public Vector4 TargetSize;
            public float VoxelSize;
            public float MaxDistance;
            public int MaxSteps;
            public int GroupsX;
            public int GroupsY;
            public Texture2D StoneTexture;
            public Texture2D WoodTexture;
            public Texture2D SandTexture;
            public Texture2D RockTexture;
            public Texture2D SlateTexture;
            public Texture2D GrassTexture;
            public Texture2D DirtTexture;
            public Texture2D DarkStoneTexture;
            public Texture2D StoneNormal;
            public Texture2D WoodNormal;
            public Texture2D SandNormal;
            public Texture2D RockNormal;
            public Texture2D SlateNormal;
            public Texture2D GrassNormal;
            public Texture2D DirtNormal;
            public Texture2D DarkStoneNormal;
            public Texture2D SkyTexture;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Enabled || _raymarch == null || _kernel < 0) return;
            if (!VoxelRenderBridge.TryGetWorld(out var world)) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            if (camera.cameraType == CameraType.Preview) return;

            // The GPU mirror is refreshed while recording rather than inside the render function:
            // it uploads through ComputeBuffer.SetData, which is immediate and has no place in a
            // deferred command list.
            _buffers.Sync(ref world.Table, ref world.Pool, world.CameraRegion,
                          VoxelRenderBridge.RegionsNeedingUpload);

            var desc = cameraData.cameraTargetDescriptor;
            int width = Mathf.Max(1, Mathf.RoundToInt(desc.width * RenderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(desc.height * RenderScale));

            var resourceData = frameData.Get<UniversalResourceData>();

            var colourDesc = new TextureDesc(width, height)
            {
                format = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                // The presentation tier may raymarch above native resolution. Bilinear resolve
                // turns that extra coverage into stable sub-pixel edges instead of point-sampled
                // stair steps when the compute target is composited back to the camera.
                filterMode = FilterMode.Bilinear,
                clearBuffer = false,
                name = "_VoxelRaymarch",
            };

            using var builder = renderGraph.AddUnsafePass<PassData>(k_PassName, out var passData);

            passData.Raymarch = _raymarch;
            passData.Kernel = _kernel;
            passData.Buffers = _buffers;
            passData.Colour = renderGraph.CreateTexture(colourDesc);
            passData.CameraColour = resourceData.activeColorTexture;
            passData.InvViewProj = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true)
                                    * camera.worldToCameraMatrix).inverse;
            passData.CameraPos = camera.transform.position;
            passData.WindowOrigin = new Vector3(_buffers.WindowOrigin.x, _buffers.WindowOrigin.y,
                                                _buffers.WindowOrigin.z);
            passData.TargetSize = new Vector4(width, height, 1f / width, 1f / height);
            passData.VoxelSize = VoxelSize;
            passData.MaxDistance = MaxDistance;
            passData.MaxSteps = MaxSteps;
            passData.GroupsX = (width + 7) / 8;
            passData.GroupsY = (height + 7) / 8;
            passData.StoneTexture = _stoneTexture;
            passData.WoodTexture = _woodTexture;
            passData.SandTexture = _sandTexture;
            passData.RockTexture = _rockTexture;
            passData.SlateTexture = _slateTexture;
            passData.GrassTexture = _grassTexture;
            passData.DirtTexture = _dirtTexture;
            passData.DarkStoneTexture = _darkStoneTexture;
            passData.StoneNormal = _stoneNormal;
            passData.WoodNormal = _woodNormal;
            passData.SandNormal = _sandNormal;
            passData.RockNormal = _rockNormal;
            passData.SlateNormal = _slateNormal;
            passData.GrassNormal = _grassNormal;
            passData.DirtNormal = _dirtNormal;
            passData.DarkStoneNormal = _darkStoneNormal;
            passData.SkyTexture = _skyTexture;

            builder.UseTexture(passData.Colour, AccessFlags.Write);
            builder.UseTexture(passData.CameraColour, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<PassData>(static (data, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                cmd.SetComputeBufferParam(data.Raymarch, data.Kernel, s_RegionWindow, data.Buffers.WindowBuffer);
                cmd.SetComputeBufferParam(data.Raymarch, data.Kernel, s_BrickRefs, data.Buffers.BrickRefBuffer);
                cmd.SetComputeBufferParam(data.Raymarch, data.Kernel, s_BrickVoxels, data.Buffers.VoxelBuffer);
                cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_Colour, data.Colour);
                if (data.StoneTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_StoneTexture, data.StoneTexture);
                if (data.WoodTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_WoodTexture, data.WoodTexture);
                if (data.SandTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_SandTexture, data.SandTexture);
                if (data.RockTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_RockTexture, data.RockTexture);
                if (data.SlateTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_SlateTexture, data.SlateTexture);
                if (data.GrassTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_GrassTexture, data.GrassTexture);
                if (data.DirtTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_DirtTexture, data.DirtTexture);
                if (data.DarkStoneTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_DarkStoneTexture,
                                               data.DarkStoneTexture);
                if (data.StoneNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_StoneNormal, data.StoneNormal);
                if (data.WoodNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_WoodNormal, data.WoodNormal);
                if (data.SandNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_SandNormal, data.SandNormal);
                if (data.RockNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_RockNormal, data.RockNormal);
                if (data.SlateNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_SlateNormal, data.SlateNormal);
                if (data.GrassNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_GrassNormal, data.GrassNormal);
                if (data.DirtNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_DirtNormal, data.DirtNormal);
                if (data.DarkStoneNormal != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_DarkStoneNormal,
                                               data.DarkStoneNormal);
                if (data.SkyTexture != null)
                    cmd.SetComputeTextureParam(data.Raymarch, data.Kernel, s_SkyTexture,
                                               data.SkyTexture);

                cmd.SetComputeMatrixParam(data.Raymarch, s_InvViewProj, data.InvViewProj);
                cmd.SetComputeVectorParam(data.Raymarch, s_CameraPos, data.CameraPos);
                cmd.SetComputeVectorParam(data.Raymarch, s_WindowOrigin, data.WindowOrigin);
                cmd.SetComputeVectorParam(data.Raymarch, s_SunDirection, VoxelRenderBridge.SunDirection);
                cmd.SetComputeVectorParam(data.Raymarch, s_SkyHorizon, VoxelRenderBridge.SkyHorizon);
                cmd.SetComputeVectorParam(data.Raymarch, s_SkyZenith, VoxelRenderBridge.SkyZenith);
                cmd.SetComputeVectorParam(data.Raymarch, s_TargetSize, data.TargetSize);
                cmd.SetComputeFloatParam(data.Raymarch, s_VoxelSize, data.VoxelSize);
                cmd.SetComputeFloatParam(data.Raymarch, s_MaxDistance, data.MaxDistance);
                cmd.SetComputeIntParam(data.Raymarch, s_MaxSteps, data.MaxSteps);
                cmd.SetComputeIntParam(data.Raymarch, s_WindowX, VoxelGpuBuffers.WindowX);
                cmd.SetComputeIntParam(data.Raymarch, s_WindowY, VoxelGpuBuffers.WindowY);
                cmd.SetComputeIntParam(data.Raymarch, s_WindowZ, VoxelGpuBuffers.WindowZ);
                cmd.SetComputeIntParam(data.Raymarch, s_DebugMode, VoxelRenderBridge.DebugMode);
                cmd.SetComputeIntParam(data.Raymarch, s_TerrainSeed, unchecked((int)VoxelRenderBridge.TerrainSeed));
                cmd.SetComputeFloatParam(data.Raymarch, s_FarDistance, VoxelRenderBridge.FarDistance);
                cmd.SetComputeIntParam(data.Raymarch, s_FarBaseHeight, VoxelRenderBridge.FarBaseHeight);
                cmd.SetComputeIntParam(data.Raymarch, s_FarEnabled, VoxelRenderBridge.FarFieldEnabled ? 1 : 0);
                cmd.SetComputeIntParam(data.Raymarch, s_CutawayEnabled,
                                       VoxelRenderBridge.CutawayEnabled ? 1 : 0);
                cmd.SetComputeVectorParam(data.Raymarch, s_CutawayMinVoxel,
                                          VoxelRenderBridge.CutawayMinVoxel);
                cmd.SetComputeVectorParam(data.Raymarch, s_CutawayMaxVoxel,
                                          VoxelRenderBridge.CutawayMaxVoxel);
                cmd.SetComputeVectorArrayParam(data.Raymarch, s_MaterialColours, VoxelRenderBridge.MaterialColours);
                int localLightCount = Mathf.Min(20, VoxelRenderBridge.LocalLights?.Length ?? 0,
                                                VoxelRenderBridge.LocalLightColours?.Length ?? 0);
                cmd.SetComputeIntParam(data.Raymarch, s_LocalLightCount, localLightCount);
                if (localLightCount > 0)
                {
                    cmd.SetComputeVectorArrayParam(data.Raymarch, s_LocalLights,
                                                   VoxelRenderBridge.LocalLights);
                    cmd.SetComputeVectorArrayParam(data.Raymarch, s_LocalLightColours,
                                                   VoxelRenderBridge.LocalLightColours);
                }

                // Seed the target with what URP has drawn so far, so rays that miss composite
                // as "unchanged" rather than as this shader's idea of the sky.
                Blitter.BlitCameraTexture(cmd, data.CameraColour, data.Colour);

                cmd.DispatchCompute(data.Raymarch, data.Kernel, data.GroupsX, data.GroupsY, 1);

                Blitter.BlitCameraTexture(cmd, data.Colour, data.CameraColour);
            });
        }

        public void Dispose() => _buffers.Dispose();
    }
}
