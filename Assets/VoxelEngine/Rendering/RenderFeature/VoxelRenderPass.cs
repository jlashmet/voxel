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
        private static readonly int s_DebugMode = Shader.PropertyToID("g_DebugMode");

        private readonly VoxelGpuBuffers _buffers = new();
        private ComputeShader _raymarch;
        private int _kernel = -1;

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

        public void Setup(ComputeShader raymarch)
        {
            _raymarch = raymarch;
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
                cmd.SetComputeVectorArrayParam(data.Raymarch, s_MaterialColours, VoxelRenderBridge.MaterialColours);

                cmd.DispatchCompute(data.Raymarch, data.Kernel, data.GroupsX, data.GroupsY, 1);

                Blitter.BlitCameraTexture(cmd, data.Colour, data.CameraColour);
            });
        }

        public void Dispose() => _buffers.Dispose();
    }
}
