using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// Draws the authored voxel-world panorama before opaque geometry.
    ///
    /// The old raymarch renderer owned every pixel and therefore painted the sky whenever a ray
    /// missed. Mesh rasterization does not own background pixels, so sky is now an explicit cheap
    /// full-screen raster pass. Keeping it before opaques prevents it from overwriting ordinary
    /// Unity scene geometry while preserving the old panorama + procedural gradient presentation.
    /// </summary>
    public sealed class VoxelSkyPass : ScriptableRenderPass, IDisposable
    {
        private const string k_PassName = "VoxelEngine.AuthoredSky";

        private static readonly int s_InvViewProj = Shader.PropertyToID("_InvViewProj");
        private static readonly int s_CameraPosition = Shader.PropertyToID("_CameraPosition");
        private static readonly int s_SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int s_SkyHorizon = Shader.PropertyToID("_SkyHorizon");
        private static readonly int s_SkyZenith = Shader.PropertyToID("_SkyZenith");

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
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Enabled || _material == null) return;

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

            builder.UseTexture(data.CameraColor, AccessFlags.Write);
            builder.UseTexture(data.CameraDepth, AccessFlags.Read);
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

                // Use the same color+depth target binding overload as the proven voxel surface
                // pass. The shader has ZWrite Off, so depth is merely bound, never changed.
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
