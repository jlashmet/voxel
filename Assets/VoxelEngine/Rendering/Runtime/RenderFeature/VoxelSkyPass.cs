using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Replaces URP's normal skybox pixels with the authored voxel-world panorama.
    ///
    /// The old raymarch renderer owned every pixel and painted sky on ray misses. Mesh
    /// rasterization does not own background pixels, so this is an explicit cheap full-screen
    /// raster pass. It runs after the normal skybox and its shader passes only at untouched far
    /// depth, so opaque scene geometry is never overwritten.
    /// </summary>
    public sealed class VoxelSkyPass : ScriptableRenderPass, IDisposable
    {
        private const string k_PassName = "VoxelEngine.AuthoredSky";

        private static readonly int s_InvViewProj = Shader.PropertyToID("_InvViewProj");
        private static readonly int s_CameraPosition = Shader.PropertyToID("_CameraPosition");
        private static readonly int s_SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int s_SkyHorizon = Shader.PropertyToID("_SkyHorizon");
        private static readonly int s_SkyZenith = Shader.PropertyToID("_SkyZenith");
        private static readonly int s_CloudParams = Shader.PropertyToID("_CloudParams");
        private static readonly int s_CloudColour = Shader.PropertyToID("_CloudColour");
        private static readonly int s_CloudShadow = Shader.PropertyToID("_CloudShadow");

        private Material _material;
        private readonly MaterialPropertyBlock _properties = new();

        public bool Enabled { get; set; } = true;

        public void Setup(Shader shader, Texture2D skyTexture)
        {
            CoreUtils.Destroy(_material);
            _material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            if (_material != null && skyTexture != null)
                _material.SetTexture("_SkyTexture", skyTexture);
        }

        private class PassData
        {
            public TextureHandle CameraColor;
            public TextureHandle CameraDepth;
            public Material Material;
            public MaterialPropertyBlock Properties;
            public Matrix4x4 InvViewProj;
            public Vector4 CameraPosition;
            public Vector4 SunDirection;
            public Vector4 SkyHorizon;
            public Vector4 SkyZenith;
            public Vector4 CloudParams;
            public Vector4 CloudColour;
            public Vector4 CloudShadow;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Enabled) return;
            if (_material == null)
            {
                // No sky this frame means the camera clear shows through. Count it so a black
                // frame can be attributed instead of guessed at.
                VoxelRenderBridge.SkyPassMissingMaterialCount++;
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            if (camera == null || camera.cameraType == CameraType.Preview) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            using var builder = renderGraph.AddUnsafePass(k_PassName, out PassData data);

            data.CameraColor = resourceData.activeColorTexture;
            data.CameraDepth = resourceData.activeDepthTexture;
            data.Material = _material;
            data.Properties = _properties;
            data.InvViewProj = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, true)
                                * camera.worldToCameraMatrix).inverse;
            Vector3 cameraPosition = camera.transform.position;
            data.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y,
                                              cameraPosition.z, 1f);
            data.SunDirection = VoxelRenderBridge.SunDirection;
            data.SkyHorizon = VoxelRenderBridge.SkyHorizon;
            data.SkyZenith = VoxelRenderBridge.SkyZenith;
            data.CloudParams = new Vector4(VoxelRenderBridge.CloudScale,
                                           VoxelRenderBridge.CloudCoverage,
                                           VoxelRenderBridge.CloudDriftSpeed,
                                           VoxelRenderBridge.CloudOpacity);
            Color cloudLit = VoxelRenderBridge.CloudColour.linear;
            data.CloudColour = new Vector4(cloudLit.r, cloudLit.g, cloudLit.b, 1f);
            Color cloudDark = VoxelRenderBridge.CloudShadowColour.linear;
            data.CloudShadow = new Vector4(cloudDark.r, cloudDark.g, cloudDark.b, 1f);

            builder.UseTexture(data.CameraColor, AccessFlags.Write);
            // Unsafe SetRenderTarget binds the depth attachment just like the proven world pass.
            // Declare the attachment conservatively even though the shader has ZWrite Off.
            builder.UseTexture(data.CameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<PassData>(static (passData, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                passData.Properties.Clear();
                passData.Properties.SetMatrix(s_InvViewProj, passData.InvViewProj);
                passData.Properties.SetVector(s_CameraPosition, passData.CameraPosition);
                passData.Properties.SetVector(s_SunDirection, passData.SunDirection);
                passData.Properties.SetVector(s_SkyHorizon, passData.SkyHorizon);
                passData.Properties.SetVector(s_SkyZenith, passData.SkyZenith);
                passData.Properties.SetVector(s_CloudParams, passData.CloudParams);
                passData.Properties.SetVector(s_CloudColour, passData.CloudColour);
                passData.Properties.SetVector(s_CloudShadow, passData.CloudShadow);

                ctx.cmd.SetRenderTarget(passData.CameraColor, passData.CameraDepth);
                cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 0,
                                   MeshTopology.Triangles, 3, 1, passData.Properties);
            });
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
