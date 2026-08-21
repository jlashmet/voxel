using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
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
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceDrawMetadata =
            Shader.PropertyToID("_SurfaceDrawMetadata");
        private static readonly int s_SurfaceDrawBase = Shader.PropertyToID("_SurfaceDrawBase");

        // Four buckets per power of two keep padded vertex work below 25% while collapsing
        // hundreds of chunk submissions into at most a few dozen instanced draws.
        private const int SolidDrawBucketCount = 128;
        private const int SolidDrawMetadataBufferCount = 3;

        private VoxelSurfaceScheduler _scheduler;
        // Draw staging is bounded by the fixed arena args capacities. Allocate once with the
        // render pass; camera motion may change counts but can never resize managed arrays.
        private readonly CpuTransvoxelChunkCache.Entry[] _transvoxelDrawEntries =
            new CpuTransvoxelChunkCache.Entry[VoxelSurfaceScheduler.SurfaceArenaDrawCapacity];
        private readonly CpuWaterSurfaceChunkCache.Entry[] _waterDrawEntries =
            new CpuWaterSurfaceChunkCache.Entry[CpuWaterSurfaceChunkCache.ArenaDrawCapacity];
        private readonly int[] _solidDrawBucketCounts = new int[SolidDrawBucketCount];
        private readonly int[] _solidDrawBucketStarts = new int[SolidDrawBucketCount];
        private readonly int[] _solidDrawBucketCursors = new int[SolidDrawBucketCount];
        private readonly int[] _solidDrawBucketVertexCounts = new int[SolidDrawBucketCount];
        private readonly ComputeBuffer[] _solidDrawMetadata =
            new ComputeBuffer[SolidDrawMetadataBufferCount];
        private ComputeBuffer _activeSolidDrawMetadata;

        private Material _surfaceMaterial;
        private Material _waterMaterial;
        private readonly MaterialPropertyBlock _surfaceProperties = new();
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
            VoxelRenderBridge.RegisterWorldReleaseHandler(ReleaseWorldResources);
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
            public MaterialPropertyBlock Properties;
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
            public CpuTransvoxelChunkCache.Entry[] TransvoxelEntries;
            public ComputeBuffer SurfaceVertices;
            public ComputeBuffer SurfaceIndices;
            public ComputeBuffer SolidDrawMetadata;
            public int[] SolidDrawBucketCounts;
            public int[] SolidDrawBucketStarts;
            public int[] SolidDrawBucketVertexCounts;
            public int TransvoxelEntryCount;
            public double SolidStagingMs;
            public int VisibleSolidCount;
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
                VoxelRenderBridge.LastSurfacePassState = "disabled";
                return;
            }
            if (!VoxelRenderBridge.TryGetWorld(out var world))
            {
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
                VoxelRenderBridge.LastSurfacePassState = "missing-material";
                return;
            }
            if (!VoxelRenderBridge.SurfaceBuildEnabled)
            {
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

            if (transvoxelVisible.Count > _transvoxelDrawEntries.Length)
                throw new InvalidOperationException(
                    "Visible solid draw count exceeded the fixed arena draw capacity.");
            long solidStagingStart = VoxelSolidRenderTelemetry.Timestamp();
            for (int i = 0; i < transvoxelVisible.Count; i++)
                _transvoxelDrawEntries[i] = transvoxelVisible[i];
            int solidDrawCount = PrepareSolidDrawBatches(transvoxelVisible);
            double solidStagingMs =
                VoxelSolidRenderTelemetry.ElapsedMilliseconds(solidStagingStart);

            if (waterVisible.Count > _waterDrawEntries.Length)
                throw new InvalidOperationException(
                    "Visible water draw count exceeded the fixed arena draw capacity.");
            for (int i = 0; i < waterVisible.Count; i++)
                _waterDrawEntries[i] = waterVisible[i];

            using var builder = renderGraph.AddUnsafePass(k_PassName, out SurfaceFrameData data);

            data.Material = _surfaceMaterial;
            data.WaterMaterial = _waterMaterial;
            data.Properties = _surfaceProperties;
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
            data.TransvoxelEntries = _transvoxelDrawEntries;
            data.SurfaceVertices = _scheduler.SolidGeometryVertices;
            data.SurfaceIndices = _scheduler.SolidGeometryIndices;
            data.SolidDrawMetadata = _activeSolidDrawMetadata;
            data.SolidDrawBucketCounts = _solidDrawBucketCounts;
            data.SolidDrawBucketStarts = _solidDrawBucketStarts;
            data.SolidDrawBucketVertexCounts = _solidDrawBucketVertexCounts;
            data.TransvoxelEntryCount = solidDrawCount;
            data.SolidStagingMs = solidStagingMs;
            data.VisibleSolidCount = transvoxelVisible.Count;
            data.WaterEntries = _waterDrawEntries;
            data.WaterEntryCount = waterVisible.Count;

            builder.UseTexture(data.CameraColor, AccessFlags.ReadWrite);
            builder.UseTexture(data.CameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<SurfaceFrameData>(static (passData, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                // Per-draw MaterialPropertyBlocks are copied into the command buffer for every
                // draw. Everything below is identical for the whole pass — ten vector arrays, two
                // texture arrays and the lighting/cutaway constants — so binding it per chunk meant
                // re-uploading the entire block ~1,400 times a frame once the arena grew large
                // enough to actually cover the view. Bind it once as global state; per-chunk
                // offsets now live in the compact metadata table used by the instanced batches.
                cmd.SetGlobalVectorArray(s_MaterialAlbedo,
                    VoxelPresentationCatalogue.MaterialAlbedo);
                cmd.SetGlobalVectorArray(s_MaterialSampling,
                    VoxelPresentationCatalogue.MaterialSampling);
                cmd.SetGlobalVectorArray(s_MaterialSurface,
                    VoxelPresentationCatalogue.MaterialSurface);
                cmd.SetGlobalVectorArray(s_MaterialVariation,
                    VoxelPresentationCatalogue.MaterialVariation);
                cmd.SetGlobalVectorArray(s_CoatingTint,
                    VoxelPresentationCatalogue.CoatingTint);
                cmd.SetGlobalVectorArray(s_CoatingSampling,
                    VoxelPresentationCatalogue.CoatingSampling);
                cmd.SetGlobalVectorArray(s_CoatingResponse,
                    VoxelPresentationCatalogue.CoatingResponse);
                cmd.SetGlobalVectorArray(s_SurfacePattern,
                    VoxelPresentationCatalogue.SurfacePattern);
                cmd.SetGlobalVectorArray(s_SurfaceJointColour,
                    VoxelPresentationCatalogue.SurfaceJointColour);
                cmd.SetGlobalVectorArray(s_SurfaceDetailResponse,
                    VoxelPresentationCatalogue.SurfaceDetailResponse);
                cmd.SetGlobalTexture(s_AlbedoTextures, passData.AlbedoTextures);
                cmd.SetGlobalTexture(s_NormalTextures, passData.NormalTextures);
                cmd.SetGlobalColor(s_BaseColor, passData.BaseColor);
                cmd.SetGlobalVector(s_SunDirection, passData.SunDirection);
                cmd.SetGlobalVector(s_SkyHorizon, passData.SkyHorizon);
                cmd.SetGlobalVector(s_SkyZenith, passData.SkyZenith);
                cmd.SetGlobalFloat(s_VoxelSize, passData.VoxelSize);
                cmd.SetGlobalFloat(s_DebugCoverage,
                    passData.BaseColor == Color.white ? 0f : 1f);
                cmd.SetGlobalInteger(s_CutawayEnabled, passData.CutawayEnabled ? 1 : 0);
                cmd.SetGlobalVector(s_CutawayMinVoxel, passData.CutawayMinVoxel);
                cmd.SetGlobalVector(s_CutawayMaxVoxel, passData.CutawayMaxVoxel);
                cmd.SetGlobalInteger(s_LocalLightCount, passData.LocalLightCount);
                if (passData.LocalLightCount > 0)
                {
                    cmd.SetGlobalVectorArray(s_LocalLights, passData.LocalLights);
                    cmd.SetGlobalVectorArray(s_LocalLightColours,
                                                       passData.LocalLightColours);
                }
                cmd.SetGlobalInteger(s_FlashlightEnabled,
                    passData.FlashlightEnabled ? 1 : 0);
                cmd.SetGlobalVector(s_FlashlightPosition, passData.FlashlightPosition);
                cmd.SetGlobalVector(s_FlashlightDirection, passData.FlashlightDirection);
                cmd.SetGlobalVector(s_FlashlightColour, passData.FlashlightColour);
                cmd.SetGlobalFloat(s_FlashlightRange, passData.FlashlightRange);
                cmd.SetGlobalFloat(s_FlashlightInnerCos, passData.FlashlightInnerCos);
                cmd.SetGlobalFloat(s_FlashlightOuterCos, passData.FlashlightOuterCos);

                long solidSubmissionStart = VoxelSolidRenderTelemetry.Timestamp();

                // Same arena for every solid chunk, so bind it once rather than in each draw.
                if (passData.SurfaceVertices != null)
                    cmd.SetGlobalBuffer(s_SurfaceVertices, passData.SurfaceVertices);
                if (passData.SurfaceIndices != null)
                    cmd.SetGlobalBuffer(s_SurfaceIndices, passData.SurfaceIndices);
                if (passData.SolidDrawMetadata != null)
                    cmd.SetGlobalBuffer(s_SurfaceDrawMetadata, passData.SolidDrawMetadata);

                ctx.cmd.SetRenderTarget(passData.CameraColor, passData.CameraDepth);

                int solidSubmissionCalls = 0;
                for (int bucket = 0; bucket < SolidDrawBucketCount; bucket++)
                {
                    int instanceCount = passData.SolidDrawBucketCounts[bucket];
                    if (instanceCount == 0) continue;
                    cmd.SetGlobalInt(s_SurfaceDrawBase,
                        passData.SolidDrawBucketStarts[bucket]);
                    cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 0,
                        MeshTopology.Triangles,
                        passData.SolidDrawBucketVertexCounts[bucket], instanceCount);
                    solidSubmissionCalls++;
                }
                VoxelSolidRenderTelemetry.Record(
                    passData.SolidStagingMs,
                    VoxelSolidRenderTelemetry.ElapsedMilliseconds(solidSubmissionStart),
                    passData.VisibleSolidCount, solidSubmissionCalls);

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

        private int PrepareSolidDrawBatches(
            IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible)
        {
            EnsureSolidDrawMetadata();
            _activeSolidDrawMetadata =
                _solidDrawMetadata[Time.frameCount % SolidDrawMetadataBufferCount];
            Array.Clear(_solidDrawBucketCounts, 0, _solidDrawBucketCounts.Length);
            Array.Clear(_solidDrawBucketVertexCounts, 0,
                _solidDrawBucketVertexCounts.Length);

            int drawCount = 0;
            for (int i = 0; i < visible.Count; i++)
            {
                if (!visible[i].TryGetDrawMetadata(out SurfaceDrawMetadata metadata))
                    continue;
                int bucket = SolidDrawBucket((int)metadata.IndexCount);
                _solidDrawBucketCounts[bucket]++;
                _solidDrawBucketVertexCounts[bucket] = Math.Max(
                    _solidDrawBucketVertexCounts[bucket], (int)metadata.IndexCount);
                drawCount++;
            }

            int start = 0;
            for (int bucket = 0; bucket < SolidDrawBucketCount; bucket++)
            {
                _solidDrawBucketStarts[bucket] = start;
                _solidDrawBucketCursors[bucket] = start;
                start += _solidDrawBucketCounts[bucket];
            }

            if (drawCount == 0) return 0;
            NativeArray<SurfaceDrawMetadata> destination =
                _activeSolidDrawMetadata.BeginWrite<SurfaceDrawMetadata>(0, drawCount);
            for (int i = 0; i < visible.Count; i++)
            {
                if (!visible[i].TryGetDrawMetadata(out SurfaceDrawMetadata metadata))
                    continue;
                int bucket = SolidDrawBucket((int)metadata.IndexCount);
                destination[_solidDrawBucketCursors[bucket]++] = metadata;
            }
            _activeSolidDrawMetadata.EndWrite<SurfaceDrawMetadata>(drawCount);
            return drawCount;
        }

        internal static int SolidDrawBucket(int indexCount)
        {
            uint count = (uint)Math.Max(1, indexCount);
            int exponent = 31 - math.lzcnt(count);
            uint lower = 1u << exponent;
            int subdivision = (int)Math.Min(3u, ((count - lower) * 4u) / lower);
            return Math.Min(SolidDrawBucketCount - 1, exponent * 4 + subdivision);
        }

        private void EnsureSolidDrawMetadata()
        {
            if (_solidDrawMetadata[0] != null) return;
            for (int i = 0; i < SolidDrawMetadataBufferCount; i++)
            {
                _solidDrawMetadata[i] = new ComputeBuffer(
                    VoxelSurfaceScheduler.SurfaceArenaDrawCapacity,
                    sizeof(uint) * 4, ComputeBufferType.Structured,
                    ComputeBufferMode.SubUpdates);
            }
        }

        private void ReleaseSolidDrawMetadata()
        {
            for (int i = 0; i < SolidDrawMetadataBufferCount; i++)
            {
                _solidDrawMetadata[i]?.Release();
                _solidDrawMetadata[i] = null;
            }
            _activeSolidDrawMetadata = null;
        }

        private void ReleaseWorldResources()
        {
            VoxelSolidRenderTelemetry.Reset();
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
            Array.Clear(_transvoxelDrawEntries, 0, _transvoxelDrawEntries.Length);
            Array.Clear(_waterDrawEntries, 0, _waterDrawEntries.Length);
        }

        public void Dispose()
        {
            VoxelRenderBridge.UnregisterActivePass(this);
            VoxelRenderBridge.UnregisterWorldReleaseHandler(ReleaseWorldResources);
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
