using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Rendering
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

        private const int GpuDetailBricksPerAxis = 16;
        private const int GpuDetailSourceStep = 4;
        private const int GpuDetailArenaSlots = 16;
        private const int GpuDetailMaxIndicesPerChunk = 131072;

        private const int GpuCoverageBricksPerAxis = 32;
        private const int GpuCoverageSourceStep = 8;
        private const int GpuCoverageArenaSlots = 208;
        private const int GpuCoverageMaxIndicesPerChunk = 48000;
        private const long MaxGpuResidentBytes = 512L * 1024L * 1024L;

        private readonly VoxelSurfaceScheduler _scheduler = new();
        private readonly VoxelGpuBuffers _gpuBuffers = new();
        private readonly GpuSurfaceChunkCache _gpuCoverageSolids =
            new(GpuCoverageBricksPerAxis, GpuCoverageSourceStep)
            {
                MaxBuildsPerFrame = 8,
                MaxResidentChunks = GpuCoverageArenaSlots,
                MaxIndicesPerChunk = GpuCoverageMaxIndicesPerChunk,
            };
        private readonly GpuSurfaceChunkCache _gpuDetailSolids =
            new(GpuDetailBricksPerAxis, GpuDetailSourceStep)
            {
                MaxBuildsPerFrame = 4,
                MaxResidentChunks = GpuDetailArenaSlots,
                MaxIndicesPerChunk = GpuDetailMaxIndicesPerChunk,
            };
        private readonly System.Collections.Generic.List<VoxelChangeRecord> _gpuChanges = new(256);
        private readonly System.Collections.Generic.HashSet<int3> _gpuChangedRegions = new();
        private ulong _gpuChangeCursor;
        private VoxelChangeJournal _gpuJournal;
        private GpuSurfaceArena _gpuCoverageArena;
        private GpuSurfaceArena _gpuDetailArena;
        private ComputeShader _surfaceExtraction;
        private CpuTransvoxelChunkCache.Entry[] _transvoxelDrawEntries =
            Array.Empty<CpuTransvoxelChunkCache.Entry>();
        private CpuWaterSurfaceChunkCache.Entry[] _waterDrawEntries =
            Array.Empty<CpuWaterSurfaceChunkCache.Entry>();
        private GpuSurfaceChunkCache.Entry[] _gpuCoverageDrawEntries =
            Array.Empty<GpuSurfaceChunkCache.Entry>();
        private GpuSurfaceChunkCache.Entry[] _gpuDetailDrawEntries =
            Array.Empty<GpuSurfaceChunkCache.Entry>();

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
        public VoxelSurfaceMetrics Metrics => _scheduler.Metrics;

        public VoxelRenderPass()
        {
            _gpuCoverageSolids.Finer = _gpuDetailSolids;
            _gpuDetailSolids.Coarser = _gpuCoverageSolids;
        }

        public void Setup(ComputeShader surfaceExtraction = null,
                          Shader surfaceShader = null,
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
            _surfaceExtraction = surfaceExtraction;
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
            public int TransvoxelEntryCount;
            public CpuWaterSurfaceChunkCache.Entry[] WaterEntries;
            public int WaterEntryCount;
            public bool UseGpu;
            public ComputeShader SurfaceExtraction;
            public VoxelGpuBuffers GpuBuffers;
            public GpuSurfaceChunkCache GpuCoverageSolids;
            public GpuSurfaceChunkCache.Entry[] GpuCoverageEntries;
            public int GpuCoverageEntryCount;
            public GpuSurfaceChunkCache GpuDetailSolids;
            public GpuSurfaceChunkCache.Entry[] GpuDetailEntries;
            public int GpuDetailEntryCount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
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
            VoxelRenderBridge.LastSurfacePassState = $"preparing-{camera.cameraType}";
            bool useGpu = VoxelRenderBridge.SolidBackend
                       == VoxelRenderBridge.SolidSurfaceBackend.GpuSurfaceNets
                       && _surfaceExtraction != null;
            IReadOnlyList<CpuTransvoxelChunkCache.Entry> transvoxelVisible =
                Array.Empty<CpuTransvoxelChunkCache.Entry>();
            IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> waterVisible =
                Array.Empty<CpuWaterSurfaceChunkCache.Entry>();
            IReadOnlyList<GpuSurfaceChunkCache.Entry> gpuCoverageVisible =
                Array.Empty<GpuSurfaceChunkCache.Entry>();
            IReadOnlyList<GpuSurfaceChunkCache.Entry> gpuDetailVisible =
                Array.Empty<GpuSurfaceChunkCache.Entry>();

            if (useGpu)
            {
                PrepareGpu(ref world, camera);
                gpuCoverageVisible = _gpuCoverageSolids.Visible;
                gpuDetailVisible = _gpuDetailSolids.Visible;
            }
            else
            {
                _scheduler.SolidBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.SolidBuildBudgetMs);
                _scheduler.WaterBuildBudgetMs = Math.Max(0.0, VoxelRenderBridge.WaterBuildBudgetMs);
                _scheduler.Prepare(ref world.Table, ref world.Pool, in world.Palette,
                                   in world.SurfaceCatalogue, in world.CoatingCatalogue,
                                   world.ProfileBlocks, VoxelRenderBridge.Changes,
                                   camera, VoxelSize, Time.frameCount);
                VoxelRenderBridge.SurfaceMetrics = _scheduler.Metrics;
                transvoxelVisible = _scheduler.VisibleSolids;
                waterVisible = _scheduler.VisibleWater;
            }

            EnsureCapacity(ref _transvoxelDrawEntries, transvoxelVisible.Count);
            for (int i = 0; i < transvoxelVisible.Count; i++)
                _transvoxelDrawEntries[i] = transvoxelVisible[i];

            EnsureCapacity(ref _waterDrawEntries, waterVisible.Count);
            for (int i = 0; i < waterVisible.Count; i++)
                _waterDrawEntries[i] = waterVisible[i];
            EnsureCapacity(ref _gpuCoverageDrawEntries, gpuCoverageVisible.Count);
            for (int i = 0; i < gpuCoverageVisible.Count; i++)
                _gpuCoverageDrawEntries[i] = gpuCoverageVisible[i];
            EnsureCapacity(ref _gpuDetailDrawEntries, gpuDetailVisible.Count);
            for (int i = 0; i < gpuDetailVisible.Count; i++)
                _gpuDetailDrawEntries[i] = gpuDetailVisible[i];

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
            data.TransvoxelEntryCount = transvoxelVisible.Count;
            data.WaterEntries = _waterDrawEntries;
            data.WaterEntryCount = waterVisible.Count;
            data.UseGpu = useGpu;
            data.SurfaceExtraction = _surfaceExtraction;
            data.GpuBuffers = _gpuBuffers;
            data.GpuCoverageSolids = _gpuCoverageSolids;
            data.GpuCoverageEntries = _gpuCoverageDrawEntries;
            data.GpuCoverageEntryCount = gpuCoverageVisible.Count;
            data.GpuDetailSolids = _gpuDetailSolids;
            data.GpuDetailEntries = _gpuDetailDrawEntries;
            data.GpuDetailEntryCount = gpuDetailVisible.Count;

            builder.UseTexture(data.CameraColor, AccessFlags.ReadWrite);
            builder.UseTexture(data.CameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<SurfaceFrameData>(static (passData, ctx) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                if (passData.UseGpu)
                {
                    passData.GpuCoverageSolids.RecordScheduled(
                        cmd, passData.SurfaceExtraction, passData.GpuBuffers, passData.VoxelSize);
                    passData.GpuDetailSolids.RecordScheduled(
                        cmd, passData.SurfaceExtraction, passData.GpuBuffers, passData.VoxelSize);
                }

                passData.Properties.SetVectorArray(s_MaterialAlbedo,
                    VoxelPresentationCatalogue.MaterialAlbedo);
                passData.Properties.SetVectorArray(s_MaterialSampling,
                    VoxelPresentationCatalogue.MaterialSampling);
                passData.Properties.SetVectorArray(s_MaterialSurface,
                    VoxelPresentationCatalogue.MaterialSurface);
                passData.Properties.SetVectorArray(s_MaterialVariation,
                    VoxelPresentationCatalogue.MaterialVariation);
                passData.Properties.SetVectorArray(s_CoatingTint,
                    VoxelPresentationCatalogue.CoatingTint);
                passData.Properties.SetVectorArray(s_CoatingSampling,
                    VoxelPresentationCatalogue.CoatingSampling);
                passData.Properties.SetVectorArray(s_CoatingResponse,
                    VoxelPresentationCatalogue.CoatingResponse);
                passData.Properties.SetVectorArray(s_SurfacePattern,
                    VoxelPresentationCatalogue.SurfacePattern);
                passData.Properties.SetVectorArray(s_SurfaceJointColour,
                    VoxelPresentationCatalogue.SurfaceJointColour);
                passData.Properties.SetVectorArray(s_SurfaceDetailResponse,
                    VoxelPresentationCatalogue.SurfaceDetailResponse);
                passData.Properties.SetTexture(s_AlbedoTextures, passData.AlbedoTextures);
                passData.Properties.SetTexture(s_NormalTextures, passData.NormalTextures);
                passData.Properties.SetColor(s_BaseColor, passData.BaseColor);
                passData.Properties.SetVector(s_SunDirection, passData.SunDirection);
                passData.Properties.SetVector(s_SkyHorizon, passData.SkyHorizon);
                passData.Properties.SetVector(s_SkyZenith, passData.SkyZenith);
                passData.Properties.SetFloat(s_VoxelSize, passData.VoxelSize);
                passData.Properties.SetFloat(s_DebugCoverage,
                    passData.BaseColor == Color.white ? 0f : 1f);
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

                if (passData.UseGpu)
                {
                    for (int i = 0; i < passData.GpuCoverageEntryCount; i++)
                        passData.GpuCoverageEntries[i].Extractor.Draw(
                            cmd, passData.Material, passData.Properties);
                    for (int i = 0; i < passData.GpuDetailEntryCount; i++)
                        passData.GpuDetailEntries[i].Extractor.Draw(
                            cmd, passData.Material, passData.Properties);
                }
                else
                {
                    for (int i = 0; i < passData.TransvoxelEntryCount; i++)
                        passData.TransvoxelEntries[i].Draw(cmd, passData.Material,
                                                           passData.Properties);
                }

                if (passData.WaterMaterial != null && passData.WaterEntryCount > 0)
                {
                    passData.WaterProperties.Clear();
                    passData.WaterProperties.SetVector(s_CameraPosition, passData.CameraPosition);
                    passData.WaterProperties.SetVector(s_SunDirection, passData.SunDirection);
                    passData.WaterProperties.SetVector(s_SkyHorizon, passData.SkyHorizon);
                    passData.WaterProperties.SetVector(s_SkyZenith, passData.SkyZenith);
                    passData.WaterProperties.SetFloat(s_WaterTime, passData.WaterTime);
                    for (int i = 0; i < passData.WaterEntryCount; i++)
                        passData.WaterEntries[i].Draw(cmd, passData.WaterMaterial,
                                                      passData.WaterProperties);
                }
            });
        }

        private void PrepareGpu(ref VoxelWorldView world, Camera camera)
        {
            _gpuChangedRegions.Clear();
            VoxelChangeJournal journal = VoxelRenderBridge.Changes;
            if (!ReferenceEquals(journal, _gpuJournal))
            {
                _gpuJournal = journal;
                _gpuChangeCursor = 0;
            }
            if (journal != null)
            {
                bool complete = journal.ReadSince(ref _gpuChangeCursor, _gpuChanges);
                if (!complete)
                {
                    using NativeArray<int3> resident =
                        world.Table.GetResidentCoords(Allocator.Temp);
                    for (int i = 0; i < resident.Length; i++)
                        _gpuChangedRegions.Add(resident[i]);
                }
                else
                {
                    for (int i = 0; i < _gpuChanges.Count; i++)
                        _gpuChangedRegions.Add(_gpuChanges[i].Region);
                }
            }

            int3 cameraVoxel = new(
                Mathf.FloorToInt(camera.transform.position.x / VoxelSize),
                Mathf.FloorToInt(camera.transform.position.y / VoxelSize),
                Mathf.FloorToInt(camera.transform.position.z / VoxelSize));
            int3 cameraRegion = new(
                FloorDiv(cameraVoxel.x, VoxelDimensions.RegionVoxelEdge),
                FloorDiv(cameraVoxel.y, VoxelDimensions.RegionVoxelEdge),
                FloorDiv(cameraVoxel.z, VoxelDimensions.RegionVoxelEdge));
            _gpuBuffers.Sync(ref world.Table, ref world.Pool, cameraRegion, _gpuChangedRegions);
            EnsureGpuArenas();
            _gpuCoverageSolids.InvalidateSurfaceBricks(_gpuBuffers.LastSurfaceWorldBricks);
            _gpuCoverageSolids.InvalidateDensityBricks(_gpuBuffers.LastDensityWorldBricks);
            _gpuDetailSolids.InvalidateSurfaceBricks(_gpuBuffers.LastSurfaceWorldBricks);
            _gpuDetailSolids.InvalidateDensityBricks(_gpuBuffers.LastDensityWorldBricks);

            _gpuCoverageSolids.Prepare(camera, VoxelSize, Time.frameCount);
            _gpuDetailSolids.Prepare(camera, VoxelSize, Time.frameCount);
            // Coverage decides which parents have a complete fine replacement. Collect it first
            // so the detail tier can use that handoff decision in the same frame.
            _gpuCoverageSolids.CollectVisible(camera, VoxelSize, Time.frameCount);
            _gpuDetailSolids.CollectVisible(camera, VoxelSize, Time.frameCount);
            VoxelRenderBridge.SurfaceMetrics = new VoxelSurfaceMetrics(
                _gpuCoverageSolids, _gpuDetailSolids, _gpuChanges.Count,
                _gpuBuffers.LastSurfaceWorldBricks.Count);
            VoxelRenderBridge.LastSurfacePassState =
                $"gpu coverage={_gpuCoverageSolids.ResidentCount}/"
              + $"{_gpuCoverageSolids.KnownCount} dirty={_gpuCoverageSolids.DirtyCount} "
              + $"visible={_gpuCoverageSolids.Visible.Count} "
              + $"missingVisible={_gpuCoverageSolids.MissingVisibleCount}; "
              + $"detail={_gpuDetailSolids.ResidentCount}/"
              + $"{_gpuDetailSolids.KnownCount} dirty={_gpuDetailSolids.DirtyCount} "
              + $"visible={_gpuDetailSolids.Visible.Count}";
        }

        private void EnsureGpuArenas()
        {
            if (_gpuCoverageArena is { IsCreated: true }
                && _gpuDetailArena is { IsCreated: true }) return;

            _gpuCoverageArena?.Dispose();
            _gpuDetailArena?.Dispose();
            int coverageCells = _gpuCoverageSolids.GridSamplesPerAxis - 1;
            int coverageCellsPerChunk = checked(coverageCells * coverageCells * coverageCells);
            int detailCells = _gpuDetailSolids.GridSamplesPerAxis - 1;
            int detailCellsPerChunk = checked(detailCells * detailCells * detailCells);
            long totalBytes = EstimateGpuResidentBytes(_gpuBuffers.ByteSize,
                coverageCellsPerChunk, detailCellsPerChunk);
            if (totalBytes > MaxGpuResidentBytes)
                throw new InvalidOperationException(
                    $"GPU voxel renderer requests {totalBytes / (1024 * 1024)} MiB; " +
                    $"the aggregate hard limit is {MaxGpuResidentBytes / (1024 * 1024)} MiB.");
            _gpuCoverageArena = new GpuSurfaceArena(
                GpuCoverageArenaSlots, coverageCellsPerChunk, GpuCoverageMaxIndicesPerChunk);
            try
            {
                _gpuDetailArena = new GpuSurfaceArena(
                    GpuDetailArenaSlots, detailCellsPerChunk, GpuDetailMaxIndicesPerChunk);
            }
            catch
            {
                _gpuCoverageArena.Dispose();
                _gpuCoverageArena = null;
                throw;
            }
            _gpuCoverageSolids.Arena = _gpuCoverageArena;
            _gpuDetailSolids.Arena = _gpuDetailArena;
        }

        public static long ConfiguredGpuResidentBytesForPool(int poolCapacity)
        {
            int coverageSamples = checked(
                GpuCoverageBricksPerAxis * 8 / GpuCoverageSourceStep + 2);
            int coverageCells = coverageSamples - 1;
            int detailSamples = checked(GpuDetailBricksPerAxis * 8 / GpuDetailSourceStep + 2);
            int detailCells = detailSamples - 1;
            return EstimateGpuResidentBytes(VoxelGpuBuffers.ComputeByteSize(poolCapacity),
                checked(coverageCells * coverageCells * coverageCells),
                checked(detailCells * detailCells * detailCells));
        }

        private static long EstimateGpuResidentBytes(long mirrorBytes,
                                                     int coverageCellsPerChunk,
                                                     int detailCellsPerChunk)
            => checked(mirrorBytes
              + GpuSurfaceArena.ComputeByteSize(GpuCoverageArenaSlots,
                    coverageCellsPerChunk, GpuCoverageMaxIndicesPerChunk / 6 * 6)
              + GpuSurfaceArena.ComputeByteSize(GpuDetailArenaSlots,
                    detailCellsPerChunk, GpuDetailMaxIndicesPerChunk / 6 * 6));

        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

        public void Dispose()
        {
            _scheduler.Dispose();
            _gpuCoverageSolids.Dispose();
            _gpuDetailSolids.Dispose();
            _gpuCoverageArena?.Dispose();
            _gpuDetailArena?.Dispose();
            _gpuCoverageArena = null;
            _gpuDetailArena = null;
            _gpuBuffers.Dispose();
            CoreUtils.Destroy(_surfaceMaterial);
            CoreUtils.Destroy(_waterMaterial);
            CoreUtils.Destroy(_albedoTextures);
            CoreUtils.Destroy(_normalTextures);
            _surfaceMaterial = null;
            _waterMaterial = null;
            _albedoTextures = null;
            _normalTextures = null;
        }

        private static void EnsureCapacity<T>(ref T[] array, int required)
        {
            if (array.Length >= required) return;
            Array.Resize(ref array, math.max(16, math.ceilpow2(required)));
        }
    }
}
