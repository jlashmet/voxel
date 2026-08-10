using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// Draws voxel geometry as continuous GPU-authored surface meshes, one per resident chunk.
    ///
    /// The current recovery path deliberately uses one extraction resolution only. The previous
    /// two-level path switched independently generated 10 cm and 20 cm meshes without stitching
    /// their shared boundary, which produced visible cracks. A second level should only return
    /// once the transition itself is watertight.
    /// </summary>
    public sealed class VoxelRenderPass : ScriptableRenderPass, IDisposable
    {
        private const string k_PassName = "VoxelEngine.ContinuousSurface";

        private static readonly int s_DensityRegionWindow = Shader.PropertyToID("g_RegionWindow");
        private static readonly int s_DensityBrickRefs = Shader.PropertyToID("g_BrickRefs");
        private static readonly int s_DensityBrickVoxels = Shader.PropertyToID("g_BrickVoxels");
        private static readonly int s_BrickDensity = Shader.PropertyToID("g_BrickDensity");
        private static readonly int s_DensityJobs = Shader.PropertyToID("g_DensityJobs");
        private static readonly int s_DensityJobCount = Shader.PropertyToID("g_DensityJobCount");
        private static readonly int s_DensityWindowOrigin = Shader.PropertyToID("g_WindowOrigin");
        private static readonly int s_DensityWindowX = Shader.PropertyToID("g_WindowX");
        private static readonly int s_DensityWindowY = Shader.PropertyToID("g_WindowY");
        private static readonly int s_DensityWindowZ = Shader.PropertyToID("g_WindowZ");
        private static readonly int s_DensityTerrainSeed = Shader.PropertyToID("g_TerrainSeed");
        private static readonly int s_DensityFarBaseHeight = Shader.PropertyToID("g_FarBaseHeight");

        private static readonly int s_MaterialRegionWindow = Shader.PropertyToID("_RegionWindow");
        private static readonly int s_MaterialBrickRefs = Shader.PropertyToID("_BrickRefs");
        private static readonly int s_MaterialBrickVoxels = Shader.PropertyToID("_BrickVoxels");
        private static readonly int s_WindowOrigin = Shader.PropertyToID("_WindowOrigin");
        private static readonly int s_WindowX = Shader.PropertyToID("_WindowX");
        private static readonly int s_WindowY = Shader.PropertyToID("_WindowY");
        private static readonly int s_WindowZ = Shader.PropertyToID("_WindowZ");
        private static readonly int s_CutawayEnabled = Shader.PropertyToID("_CutawayEnabled");
        private static readonly int s_CutawayMinVoxel = Shader.PropertyToID("_CutawayMinVoxel");
        private static readonly int s_CutawayMaxVoxel = Shader.PropertyToID("_CutawayMaxVoxel");
        private static readonly int s_LocalLightCount = Shader.PropertyToID("_LocalLightCount");
        private static readonly int s_LocalLights = Shader.PropertyToID("_LocalLights");
        private static readonly int s_LocalLightColours = Shader.PropertyToID("_LocalLightColours");
        private static readonly int s_FlashlightEnabled = Shader.PropertyToID("_FlashlightEnabled");
        private static readonly int s_FlashlightPosition = Shader.PropertyToID("_FlashlightPosition");
        private static readonly int s_FlashlightDirection = Shader.PropertyToID("_FlashlightDirection");
        private static readonly int s_FlashlightColour = Shader.PropertyToID("_FlashlightColour");
        private static readonly int s_FlashlightRange = Shader.PropertyToID("_FlashlightRange");
        private static readonly int s_FlashlightInnerCos = Shader.PropertyToID("_FlashlightInnerCos");
        private static readonly int s_FlashlightOuterCos = Shader.PropertyToID("_FlashlightOuterCos");

        private static readonly int s_SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int s_SkyHorizon = Shader.PropertyToID("_SkyHorizon");
        private static readonly int s_SkyZenith = Shader.PropertyToID("_SkyZenith");
        private static readonly int s_MaterialColours = Shader.PropertyToID("_MaterialColours");
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_VoxelSize = Shader.PropertyToID("_VoxelSize");
        private static readonly int s_DebugCoverage = Shader.PropertyToID("_DebugCoverage");

        private const int MaxIndicesPerChunk = 78336;
        private const int SurfaceArenaSlots = 512;

        private readonly VoxelGpuBuffers _buffers = new();
        // Recovery LOD: keep the existing 18^3 extraction lattice/arena footprint but cover
        // 128 authoritative voxels (12.8 m) per chunk instead of 32 (3.2 m). The old layout
        // needed 256 mesh slots per 51.2 m terrain region, so the 512-slot arena could not even
        // represent the nine regions preloaded around the showcase castle. 16 bricks at step 8
        // still gives 128 / 8 + 2 = 18 lattice samples, while covering 16x more ground per slot.
        private readonly GpuSurfaceChunkCache _surfaceCache = new(16, 8)
        {
            MaxBuildsPerFrame = 16,
            MaxResidentChunks = SurfaceArenaSlots,
            MaxIndicesPerChunk = MaxIndicesPerChunk
        };
        private GpuSurfaceArena _surfaceArena;

        private ComputeShader _surfaceExtraction;
        private ComputeShader _densityCompute;
        private int _densityKernel = -1;
        private Material _surfaceMaterial;
        private readonly MaterialPropertyBlock _surfaceProperties = new();
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
        public bool Enabled { get; set; } = true;

        public int LastBricksUploaded => _buffers.LastBricksUploaded;
        public int ResidentSlots => _buffers.ResidentSlots;
        public VoxelGpuBuffers Buffers => _buffers;

        private void EnsureSurfaceArena()
        {
            if (_surfaceArena is { IsCreated: true }) return;

            _surfaceArena?.Dispose();
            int cells = _surfaceCache.GridSamplesPerAxis - 1;
            int cellsPerChunk = cells * cells * cells;
            _surfaceArena = new GpuSurfaceArena(SurfaceArenaSlots, cellsPerChunk,
                                                _surfaceCache.MaxIndicesPerChunk);
            _surfaceCache.Arena = _surfaceArena;
        }

        public void Setup(ComputeShader surfaceExtraction = null,
                          Shader surfaceShader = null,
                          Texture2D stoneTexture = null,
                          Texture2D woodTexture = null, Texture2D sandTexture = null,
                          Texture2D rockTexture = null, Texture2D slateTexture = null,
                          Texture2D grassTexture = null, Texture2D dirtTexture = null,
                          Texture2D stoneNormal = null, Texture2D woodNormal = null,
                          Texture2D sandNormal = null, Texture2D rockNormal = null,
                          Texture2D slateNormal = null, Texture2D grassNormal = null,
                          Texture2D dirtNormal = null, Texture2D darkStoneTexture = null,
                          Texture2D darkStoneNormal = null, Texture2D skyTexture = null,
                          ComputeShader densityCompute = null)
        {
            _surfaceExtraction = surfaceExtraction;
            _densityCompute = densityCompute;
            _densityKernel = densityCompute != null && densityCompute.HasKernel("CSBuildDensity")
                ? densityCompute.FindKernel("CSBuildDensity") : -1;

            CoreUtils.Destroy(_surfaceMaterial);
            _surfaceMaterial = surfaceShader != null
                ? CoreUtils.CreateEngineMaterial(surfaceShader) : null;

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

            if (_surfaceMaterial != null)
            {
                _surfaceMaterial.SetTexture("_StoneTexture", stoneTexture);
                _surfaceMaterial.SetTexture("_WoodTexture", woodTexture);
                _surfaceMaterial.SetTexture("_SandTexture", sandTexture);
                _surfaceMaterial.SetTexture("_RockTexture", rockTexture);
                _surfaceMaterial.SetTexture("_SlateTexture", slateTexture);
                _surfaceMaterial.SetTexture("_GrassTexture", grassTexture);
                _surfaceMaterial.SetTexture("_DirtTexture", dirtTexture);
                _surfaceMaterial.SetTexture("_DarkStoneTexture", darkStoneTexture);
                _surfaceMaterial.SetTexture("_StoneNormal", stoneNormal);
                _surfaceMaterial.SetTexture("_WoodNormal", woodNormal);
                _surfaceMaterial.SetTexture("_SandNormal", sandNormal);
                _surfaceMaterial.SetTexture("_RockNormal", rockNormal);
                _surfaceMaterial.SetTexture("_SlateNormal", slateNormal);
                _surfaceMaterial.SetTexture("_GrassNormal", grassNormal);
                _surfaceMaterial.SetTexture("_DirtNormal", dirtNormal);
                _surfaceMaterial.SetTexture("_DarkStoneNormal", darkStoneNormal);
            }
        }

        private class SurfaceComputeFrameData
        {
            public ComputeShader SurfaceExtraction;
            public ComputeShader DensityCompute;
            public int DensityKernel;
            public TextureHandle CameraColor;
            public TextureHandle CameraDepth;
            public VoxelGpuBuffers Buffers;
            public float VoxelSize;
            public GpuSurfaceChunkCache SurfaceCache;
            public Material Material;
            public MaterialPropertyBlock Properties;
            public Vector4[] MaterialColours;
            public Color BaseColor;
            public Vector4 SunDirection;
            public Vector4 SkyHorizon;
            public Vector4 SkyZenith;
            public Vector4 WindowOrigin;
            public Vector4 CutawayMinVoxel;
            public Vector4 CutawayMaxVoxel;
            public bool CutawayEnabled;
            public int LocalLightCount;
            public Vector4[] LocalLights;
            public Vector4[] LocalLightColours;
            public bool FlashlightEnabled;
            public Vector4 FlashlightPosition;
            public Vector4 FlashlightDirection;
            public Vector4 FlashlightColour;
            public float FlashlightRange;
            public float FlashlightInnerCos;
            public float FlashlightOuterCos;
            public GpuSurfaceChunkCache.Entry[] VisibleEntries;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Enabled) return;
            if (!VoxelRenderBridge.TryGetWorld(out var world)) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (camera.cameraType == CameraType.Preview) return;

            _buffers.Sync(ref world.Table, ref world.Pool, world.CameraRegion,
                          VoxelRenderBridge.RegionsNeedingUpload);

            if (_surfaceExtraction == null || _surfaceMaterial == null) return;

            EnsureSurfaceArena();
            _surfaceCache.InvalidateSurfaceBricks(_buffers.LastSurfaceWorldBricks);
            _surfaceCache.InvalidateDensityBricks(_buffers.LastDensityWorldBricks);
            _surfaceCache.Prepare(camera, VoxelSize, Time.frameCount);
            _surfaceCache.CollectVisible(camera, VoxelSize, Time.frameCount);

            int visibleChunks = _surfaceCache.Visible.Count;
            var visibleEntries = new GpuSurfaceChunkCache.Entry[visibleChunks];
            for (int i = 0; i < visibleChunks; i++)
                visibleEntries[i] = _surfaceCache.Visible[i];

            using var builder = renderGraph.AddUnsafePass(k_PassName, out SurfaceComputeFrameData data);

            data.Material = _surfaceMaterial;
            data.Properties = _surfaceProperties;
            data.Buffers = _buffers;
            data.CameraColor = resourceData.activeColorTexture;
            data.CameraDepth = resourceData.activeDepthTexture;
            data.DensityCompute = _densityCompute;
            data.DensityKernel = _densityKernel;
            data.SunDirection = VoxelRenderBridge.SunDirection;
            data.SkyHorizon = VoxelRenderBridge.SkyHorizon;
            data.SkyZenith = VoxelRenderBridge.SkyZenith;
            data.WindowOrigin = new Vector4(_buffers.WindowOrigin.x,
                                            _buffers.WindowOrigin.y,
                                            _buffers.WindowOrigin.z, 0f);
            data.CutawayMinVoxel = VoxelRenderBridge.CutawayMinVoxel;
            data.CutawayMaxVoxel = VoxelRenderBridge.CutawayMaxVoxel;
            data.CutawayEnabled = VoxelRenderBridge.CutawayEnabled;
            data.MaterialColours = VoxelRenderBridge.MaterialColours;
            data.LocalLightCount = Mathf.Min(20,
                VoxelRenderBridge.LocalLights?.Length ?? 0,
                VoxelRenderBridge.LocalLightColours?.Length ?? 0);
            data.LocalLights = VoxelRenderBridge.LocalLights;
            data.LocalLightColours = VoxelRenderBridge.LocalLightColours;
            data.FlashlightEnabled = VoxelRenderBridge.FlashlightEnabled;
            data.FlashlightPosition = VoxelRenderBridge.FlashlightPosition;
            data.FlashlightDirection = VoxelRenderBridge.FlashlightDirection.normalized;
            Color flashlight = VoxelRenderBridge.FlashlightColour.linear;
            data.FlashlightColour = new Vector4(flashlight.r, flashlight.g,
                                                flashlight.b,
                                                VoxelRenderBridge.FlashlightIntensity);
            data.FlashlightRange = VoxelRenderBridge.FlashlightRange;
            data.FlashlightInnerCos = VoxelRenderBridge.FlashlightInnerCos;
            data.FlashlightOuterCos = VoxelRenderBridge.FlashlightOuterCos;
            data.BaseColor = VoxelRenderBridge.SurfaceDebugTint;
            data.VoxelSize = VoxelSize;
            data.SurfaceExtraction = _surfaceExtraction;
            data.SurfaceCache = _surfaceCache;
            data.VisibleEntries = visibleEntries;

            // This pass preserves the camera contents and appends opaque voxel geometry to them,
            // so both attachments are read/write resources. Binding only color left the shader's
            // ZWrite/ZTest with no camera depth attachment, allowing underground surfaces and
            // later chunk draws to appear through nearer terrain.
            builder.UseTexture(data.CameraColor, AccessFlags.ReadWrite);
            builder.UseTexture(data.CameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<SurfaceComputeFrameData>(static (passData, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                if (passData.DensityCompute != null && passData.DensityKernel >= 0
                    && passData.Buffers.DensityJobCount > 0)
                {
                    cmd.SetComputeBufferParam(passData.DensityCompute, passData.DensityKernel,
                                              s_DensityRegionWindow, passData.Buffers.WindowBuffer);
                    cmd.SetComputeBufferParam(passData.DensityCompute, passData.DensityKernel,
                                              s_DensityBrickRefs, passData.Buffers.BrickRefBuffer);
                    cmd.SetComputeBufferParam(passData.DensityCompute, passData.DensityKernel,
                                              s_DensityBrickVoxels, passData.Buffers.VoxelBuffer);
                    cmd.SetComputeBufferParam(passData.DensityCompute, passData.DensityKernel,
                                              s_BrickDensity, passData.Buffers.DensityBuffer);
                    cmd.SetComputeBufferParam(passData.DensityCompute, passData.DensityKernel,
                                              s_DensityJobs, passData.Buffers.DensityJobBuffer);
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityJobCount,
                                           passData.Buffers.DensityJobCount);
                    cmd.SetComputeVectorParam(passData.DensityCompute, s_DensityWindowOrigin,
                                              passData.WindowOrigin);
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityWindowX,
                                           VoxelGpuBuffers.WindowX);
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityWindowY,
                                           VoxelGpuBuffers.WindowY);
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityWindowZ,
                                           VoxelGpuBuffers.WindowZ);
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityTerrainSeed,
                                           unchecked((int)VoxelRenderBridge.TerrainSeed));
                    cmd.SetComputeIntParam(passData.DensityCompute, s_DensityFarBaseHeight,
                                           VoxelRenderBridge.FarBaseHeight);
                    cmd.DispatchCompute(passData.DensityCompute, passData.DensityKernel,
                                        passData.Buffers.DensityJobCount, 1, 1);
                }

                passData.SurfaceCache.RecordScheduled(cmd, passData.SurfaceExtraction,
                                                      passData.Buffers, passData.VoxelSize);

                passData.Properties.SetVectorArray(s_MaterialColours, passData.MaterialColours);
                passData.Properties.SetColor(s_BaseColor, passData.BaseColor);
                passData.Properties.SetVector(s_SunDirection, passData.SunDirection);
                passData.Properties.SetVector(s_SkyHorizon, passData.SkyHorizon);
                passData.Properties.SetVector(s_SkyZenith, passData.SkyZenith);
                passData.Properties.SetFloat(s_VoxelSize, passData.VoxelSize);
                passData.Properties.SetFloat(s_DebugCoverage,
                    passData.BaseColor == Color.white ? 0f : 1f);
                passData.Properties.SetBuffer(s_MaterialRegionWindow,
                                              passData.Buffers.WindowBuffer);
                passData.Properties.SetBuffer(s_MaterialBrickRefs,
                                              passData.Buffers.BrickRefBuffer);
                passData.Properties.SetBuffer(s_MaterialBrickVoxels,
                                              passData.Buffers.VoxelBuffer);
                passData.Properties.SetVector(s_WindowOrigin, passData.WindowOrigin);
                passData.Properties.SetInt(s_WindowX, VoxelGpuBuffers.WindowX);
                passData.Properties.SetInt(s_WindowY, VoxelGpuBuffers.WindowY);
                passData.Properties.SetInt(s_WindowZ, VoxelGpuBuffers.WindowZ);
                passData.Properties.SetInt(s_CutawayEnabled, passData.CutawayEnabled ? 1 : 0);
                passData.Properties.SetVector(s_CutawayMinVoxel, passData.CutawayMinVoxel);
                passData.Properties.SetVector(s_CutawayMaxVoxel, passData.CutawayMaxVoxel);
                passData.Properties.SetInt(s_LocalLightCount, passData.LocalLightCount);
                if (passData.LocalLightCount > 0)
                {
                    passData.Properties.SetVectorArray(s_LocalLights, passData.LocalLights);
                    passData.Properties.SetVectorArray(s_LocalLightColours,
                                                       passData.LocalLightColours);
                }
                passData.Properties.SetInt(s_FlashlightEnabled,
                    passData.FlashlightEnabled ? 1 : 0);
                passData.Properties.SetVector(s_FlashlightPosition, passData.FlashlightPosition);
                passData.Properties.SetVector(s_FlashlightDirection, passData.FlashlightDirection);
                passData.Properties.SetVector(s_FlashlightColour, passData.FlashlightColour);
                passData.Properties.SetFloat(s_FlashlightRange, passData.FlashlightRange);
                passData.Properties.SetFloat(s_FlashlightInnerCos, passData.FlashlightInnerCos);
                passData.Properties.SetFloat(s_FlashlightOuterCos, passData.FlashlightOuterCos);

                ctx.cmd.SetRenderTarget(passData.CameraColor, passData.CameraDepth);

                for (int i = 0; i < passData.VisibleEntries.Length; i++)
                    passData.VisibleEntries[i].Extractor.Draw(cmd, passData.Material,
                                                              passData.Properties);
            });
        }

        public void Dispose()
        {
            _surfaceCache.Dispose();
            _surfaceArena?.Dispose();
            _surfaceArena = null;
            _buffers.Dispose();
            CoreUtils.Destroy(_surfaceMaterial);
            _surfaceMaterial = null;
        }
    }
}
