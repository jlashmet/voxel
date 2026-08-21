using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Draws voxel geometry as derived meshes through one raster architecture.
    ///
    /// One feature-aware extracted surface owns all solid voxel geometry. Liquid voxels use a
    /// dedicated exposed-water surface cache because their topology and shading are distinct.
    /// </summary>
    public sealed class VoxelRenderPass : ScriptableRenderPass, IDisposable
    {
        private const string k_PassName = "VoxelEngine.ContinuousSurface";

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
        private static readonly int s_MaterialAlbedo = Shader.PropertyToID("_MaterialAlbedo");
        private static readonly int s_MaterialSampling = Shader.PropertyToID("_MaterialSampling");
        private static readonly int s_MaterialSurface = Shader.PropertyToID("_MaterialSurface");
        private static readonly int s_MaterialVariation = Shader.PropertyToID("_MaterialVariation");
        private static readonly int s_CoatingTint = Shader.PropertyToID("_CoatingTint");
        private static readonly int s_CoatingSampling = Shader.PropertyToID("_CoatingSampling");
        private static readonly int s_CoatingResponse = Shader.PropertyToID("_CoatingResponse");
        private static readonly int s_SurfacePattern = Shader.PropertyToID("_SurfacePattern");
        private static readonly int s_SurfaceJointColour = Shader.PropertyToID("_SurfaceJointColour");
        private static readonly int s_SurfaceDetailResponse = Shader.PropertyToID("_SurfaceDetailResponse");
        private static readonly int s_AlbedoTextures = Shader.PropertyToID("_AlbedoTextures");
        private static readonly int s_NormalTextures = Shader.PropertyToID("_NormalTextures");
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_VoxelSize = Shader.PropertyToID("_VoxelSize");
        private static readonly int s_DebugCoverage = Shader.PropertyToID("_DebugCoverage");
        private static readonly int s_CameraPosition = Shader.PropertyToID("_CameraPosition");
        private static readonly int s_WaterTime = Shader.PropertyToID("_WaterTime");
        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private const int SolidDrawBufferCount = 3;

        private VoxelSurfaceScheduler _scheduler;
        // Draw staging is bounded by the fixed arena args capacities. Allocate once with the
        // render pass; camera motion may change counts but can never resize managed arrays.
        private readonly CpuWaterSurfaceChunkCache.Entry[] _waterDrawEntries =
            new CpuWaterSurfaceChunkCache.Entry[CpuWaterSurfaceChunkCache.ArenaDrawCapacity];
        private readonly GraphicsBuffer[] _solidDrawCommands =
            new GraphicsBuffer[SolidDrawBufferCount];
        private readonly SurfaceDrawMetadata[] _solidDrawMetadataData =
            new SurfaceDrawMetadata[VoxelSurfaceScheduler.SurfaceArenaDrawCapacity];
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _solidDrawCommandData =
            new GraphicsBuffer.IndirectDrawIndexedArgs[VoxelSurfaceScheduler.SurfaceArenaDrawCapacity];
        private readonly SurfaceDrawMetadata[] _publishedSolidDrawMetadataData =
            new SurfaceDrawMetadata[VoxelSurfaceScheduler.SurfaceArenaDrawCapacity];
        private readonly MaterialPropertyBlock[] _solidDrawProperties =
            new MaterialPropertyBlock[SolidDrawBufferCount];
        private GraphicsBuffer _preparedSolidDrawCommands;
        private MaterialPropertyBlock _preparedSolidDrawProperties;
        private Camera _preparedSolidDrawCamera;
        private int _preparedSolidDrawCount;
        private int _publishedSolidDrawCount;

        private Material _surfaceMaterial;
        private Material _waterMaterial;
        private readonly MaterialPropertyBlock _waterProperties = new();
        private Texture2DArray _albedoTextures;
        private Texture2DArray _normalTextures;
        private Texture2D _skyTexture;

        public float RenderScale { get; set; } = 1f;
        public float VoxelSize { get; set; } = 0.1f;
        public bool Enabled { get; set; } = true;
        public VoxelSurfaceMetrics Metrics => _scheduler != null ? _scheduler.Metrics : default;

        public VoxelRenderPass()
        {
            for (int i = 0; i < SolidDrawBufferCount; i++)
                _solidDrawProperties[i] = new MaterialPropertyBlock();
            VoxelRenderBridge.RegisterWorldReleaseHandler(ReleaseWorldResources);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        public void Setup(Shader surfaceShader = null,
                          Shader waterShader = null,
                          Texture2D stoneTexture = null,
                          Texture2D woodTexture = null, Texture2D sandTexture = null,
                          Texture2D rockTexture = null, Texture2D slateTexture = null,
                          Texture2D grassTexture = null, Texture2D dirtTexture = null,
                          Texture2D stoneNormal = null, Texture2D woodNormal = null,
                          Texture2D sandNormal = null, Texture2D rockNormal = null,
                          Texture2D slateNormal = null, Texture2D grassNormal = null,
                          Texture2D dirtNormal = null, Texture2D darkStoneTexture = null,
                          Texture2D darkStoneNormal = null, Texture2D skyTexture = null)
        {
            CoreUtils.Destroy(_surfaceMaterial);
            CoreUtils.Destroy(_waterMaterial);
            CoreUtils.Destroy(_albedoTextures);
            CoreUtils.Destroy(_normalTextures);
            _surfaceMaterial = surfaceShader != null
                ? CoreUtils.CreateEngineMaterial(surfaceShader) : null;
            _waterMaterial = waterShader != null
                ? CoreUtils.CreateEngineMaterial(waterShader) : null;

            _skyTexture = skyTexture;

            if (_surfaceMaterial != null)
            {
                _albedoTextures = VoxelPresentationCatalogue.BuildTextureArray(new[]
                {
                    stoneTexture, woodTexture, sandTexture, rockTexture, slateTexture,
                    grassTexture, dirtTexture, darkStoneTexture,
                }, false);
                _normalTextures = VoxelPresentationCatalogue.BuildTextureArray(new[]
                {
                    stoneNormal, woodNormal, sandNormal, rockNormal, slateNormal,
                    grassNormal, dirtNormal, darkStoneNormal,
                }, true);
            }

            if (_waterMaterial != null && skyTexture != null)
                _waterMaterial.SetTexture("_SkyTexture", skyTexture);
        }

        private class SurfaceFrameData
        {
            public TextureHandle CameraColor;
            public TextureHandle CameraDepth;
            public float VoxelSize;
            public Material Material;
            public Material WaterMaterial;
            public MaterialPropertyBlock WaterProperties;
            public Texture2DArray AlbedoTextures;
            public Texture2DArray NormalTextures;
            public Color BaseColor;
            public Vector4 SunDirection;
            public Vector4 SkyHorizon;
            public Vector4 SkyZenith;
            public Vector4 CameraPosition;
            public float WaterTime;
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
            public CpuWaterSurfaceChunkCache.Entry[] WaterEntries;
            public int WaterEntryCount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Register on actual execution, not feature construction. Projects can contain several
            // renderer-data assets; the fidelity gate must inspect the pass URP really invoked.
            VoxelRenderBridge.RegisterActivePass(this);
            VoxelRenderBridge.SurfacePassRecordCount++;
            if (!Enabled)
            {
                _preparedSolidDrawCount = 0;
                VoxelRenderBridge.LastSurfacePassState = "disabled";
                return;
            }
            if (!VoxelRenderBridge.TryGetWorld(out var world))
            {
                _preparedSolidDrawCount = 0;
                VoxelRenderBridge.LastSurfacePassState = "invalid-world";
                return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (camera.cameraType == CameraType.Preview)
            {
                VoxelRenderBridge.LastSurfacePassState = "preview-camera";
                return;
            }

            if (_surfaceMaterial == null)
            {
                _preparedSolidDrawCount = 0;
                VoxelRenderBridge.LastSurfacePassState = "missing-material";
                return;
            }
            if (!VoxelRenderBridge.SurfaceBuildEnabled)
            {
                _preparedSolidDrawCount = 0;
                VoxelRenderBridge.LastSurfacePassState = "waiting-for-atomic-world";
                return;
            }

            // World teardown deliberately leaves the large native/GPU scheduler fully released.
            // Recreate it only once a valid world is actually ready to render, so Metal never has
            // to retire one arena while teardown eagerly allocates the next world's replacement.
            _scheduler ??= new VoxelSurfaceScheduler();

            VoxelRenderBridge.LastSurfacePassState = VoxelRenderBridge.VerboseSurfaceDiagnostics
                ? $"preparing-{camera.cameraType}" : "preparing";
            IReadOnlyList<CpuTransvoxelChunkCache.Entry> transvoxelVisible =
                Array.Empty<CpuTransvoxelChunkCache.Entry>();
            IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> waterVisible =
                Array.Empty<CpuWaterSurfaceChunkCache.Entry>();
            _scheduler.SolidBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.SolidBuildBudgetMs);
            _scheduler.SolidUploadBudgetBytes = Math.Max(0, VoxelRenderBridge.SolidUploadBudgetBytes);
            _scheduler.SolidUploadSliceBytes = Math.Max(0, VoxelRenderBridge.SolidUploadSliceBytes);
            _scheduler.SolidUploadWorkerBudget = Math.Max(0, VoxelRenderBridge.SolidUploadWorkerBudget);
            _scheduler.SolidUploadBudgetMs = Math.Max(0.0, VoxelRenderBridge.SolidUploadBudgetMs);
            _scheduler.ConvergenceBudgetScale = Math.Max(
                1.0, VoxelRenderBridge.SurfaceConvergenceBudgetScale);
            _scheduler.MaxVoxelRingRadiusMetres = Math.Max(
                0f, VoxelRenderBridge.SurfaceMaxVoxelRingRadiusMetres);
            _scheduler.SurfaceDiscoveryBudgetMs = Math.Max(
                0.0, VoxelRenderBridge.SurfaceDiscoveryBudgetMs);
            _scheduler.LodEnabled = VoxelRenderBridge.SurfaceLodEnabled;
            _scheduler.MaxResidentChunksPerRing = Math.Max(
                1, VoxelRenderBridge.SurfaceMaxResidentChunksPerRing);
            _scheduler.MaxConcurrentBuildsConverging = Math.Max(
                1, VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverging);
            _scheduler.MaxConcurrentBuildsConverged = Math.Max(
                0, VoxelRenderBridge.SurfaceMaxConcurrentBuildsConverged);
            _scheduler.SolidArenaMaxActiveLeases = Math.Max(
                1, VoxelRenderBridge.SolidArenaMaxActiveLeases);
            _scheduler.WaterBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.WaterBuildBudgetMs);
            _scheduler.Prepare(world.Storage, in world.Palette,
                               in world.SurfaceCatalogueView, in world.CoatingCatalogueView,
                               world.ProfileBlocks, VoxelRenderBridge.Changes,
                               camera, VoxelSize, Time.frameCount);
            VoxelRenderBridge.SurfaceMetrics = _scheduler.Metrics;
            VoxelRenderBridge.DescribeRings = _scheduler.DescribeRingResidency;
            VoxelRenderBridge.SurfaceReappearances = () => _scheduler.TotalReappearances;
            transvoxelVisible = _scheduler.VisibleSolids;
            waterVisible = _scheduler.VisibleWater;
            if (VoxelRenderBridge.VerboseSurfaceDiagnostics)
            {
                VoxelRenderBridge.LastSurfacePassState =
                    $"feature-aware resident={VoxelRenderBridge.SurfaceMetrics.SolidResidentChunks}/"
                  + $"{VoxelRenderBridge.SurfaceMetrics.SolidKnownChunks} "
                  + $"dirty={VoxelRenderBridge.SurfaceMetrics.SolidDirtyChunks} "
                  + $"visible={VoxelRenderBridge.SurfaceMetrics.VisibleSolidChunks} "
                  + $"missingVisible={VoxelRenderBridge.SurfaceMetrics.MissingVisibleSolidChunks} "
                  + $"jobs={VoxelRenderBridge.SurfaceMetrics.RunningSolidJobs} "
                  + $"prepare.p95={VoxelRenderBridge.SurfaceMetrics.SchedulerPrepareTiming.P95Ms:0.00}ms "
                  + $"discover.p95={VoxelRenderBridge.SurfaceMetrics.SurfaceDiscoveryTiming.P95Ms:0.00}ms "
                  + $"select.p95={VoxelRenderBridge.SurfaceMetrics.BuildSelectionTiming.P95Ms:0.00}ms "
                  + $"visibility.p95={VoxelRenderBridge.SurfaceMetrics.VisibilityTiming.P95Ms:0.00}ms "
                  + $"queue.p95={VoxelRenderBridge.SurfaceMetrics.QueueLatencyTiming.P95Ms:0.0}ms "
                  + $"build.p95={VoxelRenderBridge.SurfaceMetrics.BuildLatencyTiming.P95Ms:0.0}ms "
                  + $"snapshot.p95={VoxelRenderBridge.SurfaceMetrics.SnapshotTiming.P95Ms:0.00}ms "
                  + $"compact.p95={VoxelRenderBridge.SurfaceMetrics.TopologyCompactTiming.P95Ms:0.00}ms "
                  + $"merge.p95={VoxelRenderBridge.SurfaceMetrics.FacetedMergeTiming.P95Ms:0.00}ms "
                  + $"upload.p95={VoxelRenderBridge.SurfaceMetrics.UploadTiming.P95Ms:0.00}ms";
            }
            else
            {
                VoxelRenderBridge.LastSurfacePassState = "feature-aware";
            }

            if (transvoxelVisible.Count > VoxelSurfaceScheduler.SurfaceArenaDrawCapacity)
                throw new InvalidOperationException(
                    "Visible solid draw count exceeded the fixed arena draw capacity.");
            PrepareSolidMultiDraw(transvoxelVisible, camera);

            if (waterVisible.Count > _waterDrawEntries.Length)
                throw new InvalidOperationException(
                    "Visible water draw count exceeded the fixed arena draw capacity.");
            for (int i = 0; i < waterVisible.Count; i++)
                _waterDrawEntries[i] = waterVisible[i];

            using var builder = renderGraph.AddUnsafePass(k_PassName, out SurfaceFrameData data);

            data.Material = _surfaceMaterial;
            data.WaterMaterial = _waterMaterial;
            data.WaterProperties = _waterProperties;
            data.CameraColor = resourceData.activeColorTexture;
            data.CameraDepth = resourceData.activeDepthTexture;
            data.SunDirection = VoxelRenderBridge.SunDirection;
            data.SkyHorizon = VoxelRenderBridge.SkyHorizon;
            data.SkyZenith = VoxelRenderBridge.SkyZenith;
            Vector3 cameraPosition = camera.transform.position;
            data.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y,
                                              cameraPosition.z, 1f);
            data.WaterTime = Time.time;
            data.CutawayMinVoxel = VoxelRenderBridge.CutawayMinVoxel;
            data.CutawayMaxVoxel = VoxelRenderBridge.CutawayMaxVoxel;
            data.CutawayEnabled = VoxelRenderBridge.CutawayEnabled;
            data.AlbedoTextures = _albedoTextures;
            data.NormalTextures = _normalTextures;
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
            data.WaterEntries = _waterDrawEntries;
            data.WaterEntryCount = waterVisible.Count;

            builder.UseTexture(data.CameraColor, AccessFlags.ReadWrite);
            builder.UseTexture(data.CameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<SurfaceFrameData>(static (passData, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                ctx.cmd.SetRenderTarget(passData.CameraColor, passData.CameraDepth);

                if (VoxelRenderBridge.WaterRenderEnabled
                    && passData.WaterMaterial != null && passData.WaterEntryCount > 0)
                {
                    cmd.SetGlobalVector(s_CameraPosition, passData.CameraPosition);
                    cmd.SetGlobalVector(s_SunDirection, passData.SunDirection);
                    cmd.SetGlobalVector(s_SkyHorizon, passData.SkyHorizon);
                    cmd.SetGlobalVector(s_SkyZenith, passData.SkyZenith);
                    cmd.SetGlobalFloat(s_WaterTime, passData.WaterTime);
                    passData.WaterProperties.Clear();
                    for (int i = 0; i < passData.WaterEntryCount; i++)
                        passData.WaterEntries[i].Draw(cmd, passData.WaterMaterial,
                                                      passData.WaterProperties);
                }
            });
        }

        private void PrepareSolidMultiDraw(
            IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible, Camera camera)
        {
            EnsureSolidDrawBuffers();
            int slot = Time.frameCount % SolidDrawBufferCount;
            GraphicsBuffer commandBuffer = _solidDrawCommands[slot];
            MaterialPropertyBlock properties = _solidDrawProperties[slot];

            int drawCount = 0;
            bool unchanged = true;
            for (int i = 0; i < visible.Count; i++)
            {
                if (!visible[i].TryGetDrawMetadata(out SurfaceDrawMetadata metadata))
                    continue;
                if (drawCount >= _publishedSolidDrawCount
                    || !SameDrawMetadata(metadata,
                        _publishedSolidDrawMetadataData[drawCount]))
                    unchanged = false;
                _solidDrawMetadataData[drawCount] = metadata;
                _solidDrawCommandData[drawCount] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = metadata.IndexCount,
                    instanceCount = 1u,
                    startIndex = metadata.IndexStart,
                    // Stored native index values are already arena-global. Unity 6000.5 Metal
                    // does not reflect baseVertexIndex into procedural SV_VertexID, and other
                    // backends must not add the arena base a second time.
                    baseVertexIndex = 0u,
                    startInstance = 0u,
                };
                drawCount++;
            }
            unchanged &= drawCount == _publishedSolidDrawCount;

            if (drawCount == 0)
            {
                _publishedSolidDrawCount = 0;
                _preparedSolidDrawCamera = camera;
                _preparedSolidDrawCount = 0;
                return;
            }

            if (!unchanged)
            {
                commandBuffer.SetData(_solidDrawCommandData, 0, 0, drawCount);
                Array.Copy(_solidDrawMetadataData,
                    _publishedSolidDrawMetadataData, drawCount);
                _publishedSolidDrawCount = drawCount;
                _preparedSolidDrawCommands = commandBuffer;
            }

            PopulateSolidProperties(properties);
            _preparedSolidDrawProperties = properties;
            _preparedSolidDrawCamera = camera;
            _preparedSolidDrawCount = drawCount;
        }

        private static bool SameDrawMetadata(
            SurfaceDrawMetadata left, SurfaceDrawMetadata right) =>
            left.IndexStart == right.IndexStart
            && left.VertexStart == right.VertexStart
            && left.IndexCount == right.IndexCount;

        private void PopulateSolidProperties(MaterialPropertyBlock properties)
        {
            properties.Clear();
            properties.SetVectorArray(s_MaterialAlbedo,
                VoxelPresentationCatalogue.MaterialAlbedo);
            properties.SetVectorArray(s_MaterialSampling,
                VoxelPresentationCatalogue.MaterialSampling);
            properties.SetVectorArray(s_MaterialSurface,
                VoxelPresentationCatalogue.MaterialSurface);
            properties.SetVectorArray(s_MaterialVariation,
                VoxelPresentationCatalogue.MaterialVariation);
            properties.SetVectorArray(s_CoatingTint,
                VoxelPresentationCatalogue.CoatingTint);
            properties.SetVectorArray(s_CoatingSampling,
                VoxelPresentationCatalogue.CoatingSampling);
            properties.SetVectorArray(s_CoatingResponse,
                VoxelPresentationCatalogue.CoatingResponse);
            properties.SetVectorArray(s_SurfacePattern,
                VoxelPresentationCatalogue.SurfacePattern);
            properties.SetVectorArray(s_SurfaceJointColour,
                VoxelPresentationCatalogue.SurfaceJointColour);
            properties.SetVectorArray(s_SurfaceDetailResponse,
                VoxelPresentationCatalogue.SurfaceDetailResponse);
            properties.SetTexture(s_AlbedoTextures, _albedoTextures);
            properties.SetTexture(s_NormalTextures, _normalTextures);
            properties.SetColor(s_BaseColor, VoxelRenderBridge.SurfaceDebugTint);
            properties.SetVector(s_SunDirection, VoxelRenderBridge.SunDirection);
            properties.SetVector(s_SkyHorizon, VoxelRenderBridge.SkyHorizon);
            properties.SetVector(s_SkyZenith, VoxelRenderBridge.SkyZenith);
            properties.SetFloat(s_VoxelSize, VoxelSize);
            properties.SetFloat(s_DebugCoverage,
                VoxelRenderBridge.SurfaceDebugTint == Color.white ? 0f : 1f);
            properties.SetInt(s_CutawayEnabled,
                VoxelRenderBridge.CutawayEnabled ? 1 : 0);
            properties.SetVector(s_CutawayMinVoxel,
                VoxelRenderBridge.CutawayMinVoxel);
            properties.SetVector(s_CutawayMaxVoxel,
                VoxelRenderBridge.CutawayMaxVoxel);
            int localLightCount = Mathf.Min(20,
                VoxelRenderBridge.LocalLights?.Length ?? 0,
                VoxelRenderBridge.LocalLightColours?.Length ?? 0);
            properties.SetInt(s_LocalLightCount, localLightCount);
            if (localLightCount > 0)
            {
                properties.SetVectorArray(s_LocalLights,
                    VoxelRenderBridge.LocalLights);
                properties.SetVectorArray(s_LocalLightColours,
                    VoxelRenderBridge.LocalLightColours);
            }
            properties.SetInt(s_FlashlightEnabled,
                VoxelRenderBridge.FlashlightEnabled ? 1 : 0);
            properties.SetVector(s_FlashlightPosition,
                VoxelRenderBridge.FlashlightPosition);
            properties.SetVector(s_FlashlightDirection,
                VoxelRenderBridge.FlashlightDirection.normalized);
            Color flashlight = VoxelRenderBridge.FlashlightColour.linear;
            properties.SetVector(s_FlashlightColour,
                new Vector4(flashlight.r, flashlight.g, flashlight.b,
                    VoxelRenderBridge.FlashlightIntensity));
            properties.SetFloat(s_FlashlightRange,
                VoxelRenderBridge.FlashlightRange);
            properties.SetFloat(s_FlashlightInnerCos,
                VoxelRenderBridge.FlashlightInnerCos);
            properties.SetFloat(s_FlashlightOuterCos,
                VoxelRenderBridge.FlashlightOuterCos);
            properties.SetBuffer(s_SurfaceVertices,
                _scheduler.SolidGeometryVertices);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!Enabled || _surfaceMaterial == null || _preparedSolidDrawCount <= 0
                || camera != _preparedSolidDrawCamera
                || _preparedSolidDrawCommands == null)
                return;

            float diameter = VoxelSurfaceScheduler.MaxVoxelRingRadiusMetresDefault * 2f + 256f;
            var renderParams = new RenderParams(_surfaceMaterial)
            {
                camera = camera,
                worldBounds = new Bounds(camera.transform.position,
                    Vector3.one * diameter),
                matProps = _preparedSolidDrawProperties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles,
                _scheduler.SolidGeometryIndices, _preparedSolidDrawCommands,
                _preparedSolidDrawCount);
        }

        private void EnsureSolidDrawBuffers()
        {
            if (_solidDrawCommands[0] != null) return;
            for (int i = 0; i < SolidDrawBufferCount; i++)
            {
                _solidDrawCommands[i] = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    VoxelSurfaceScheduler.SurfaceArenaDrawCapacity,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
            }
        }

        private void ReleaseSolidDrawMetadata()
        {
            for (int i = 0; i < SolidDrawBufferCount; i++)
            {
                _solidDrawCommands[i]?.Release();
                _solidDrawCommands[i] = null;
            }
            _preparedSolidDrawCommands = null;
            _preparedSolidDrawProperties = null;
            _preparedSolidDrawCamera = null;
            _preparedSolidDrawCount = 0;
            _publishedSolidDrawCount = 0;
        }

        private void ReleaseWorldResources()
        {
            if (_scheduler == null) return;

            // Dispose is deliberately synchronous here: world teardown is a lifecycle boundary,
            // not the frame path. Completing ready/running jobs and releasing every Storage pin
            // before the application disposes its Storage backing is the ownership contract.
            // Leave the scheduler null after disposal. Reallocating its persistent native buffers
            // and shared ComputeBuffers here overlaps the old Metal resources' deferred retirement
            // with the next arena and turns repeated scene loads into process-wide memory growth.
            _scheduler.Dispose();
            _scheduler = null;
            ReleaseSolidDrawMetadata();
            Array.Clear(_waterDrawEntries, 0, _waterDrawEntries.Length);
        }

        public void Dispose()
        {
            VoxelRenderBridge.UnregisterActivePass(this);
            VoxelRenderBridge.UnregisterWorldReleaseHandler(ReleaseWorldResources);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _scheduler?.Dispose();
            _scheduler = null;
            ReleaseSolidDrawMetadata();
            CoreUtils.Destroy(_surfaceMaterial);
            CoreUtils.Destroy(_waterMaterial);
            CoreUtils.Destroy(_albedoTextures);
            CoreUtils.Destroy(_normalTextures);
            _surfaceMaterial = null;
            _waterMaterial = null;
            _albedoTextures = null;
            _normalTextures = null;
        }

    }
}
