using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Rendering
{
    /// <summary>
    /// Draws voxel geometry as derived meshes through one raster architecture.
    ///
    /// Authored hard bricks use the exact greedy mesher, natural bricks use Transvoxel, and
    /// liquid voxels use a dedicated exposed-water surface cache. Those layers may coexist inside
    /// the same 12.8 m render chunk; geometry semantics, not render-chunk ownership, select the
    /// presentation. The temporary Surface Nets cache remains only as a smooth migration source.
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
        private static readonly int s_CameraPosition = Shader.PropertyToID("_CameraPosition");
        private static readonly int s_WaterTime = Shader.PropertyToID("_WaterTime");

        private const int MaxIndicesPerChunk = 78336;
        private const int SurfaceArenaSlots = 512;
        private const int RecoveryBricksPerChunkAxis = 16;

        private readonly VoxelGpuBuffers _buffers = new();

        // Temporary migration source. It keeps the existing coarse mesh active until the regular
        // Transvoxel cache has completed its first working set. Once the new smooth path is
        // validated this cache/arena and CSBuildDensity dependency can be removed entirely.
        private readonly GpuSurfaceChunkCache _surfaceCache = new(RecoveryBricksPerChunkAxis, 8)
        {
            MaxBuildsPerFrame = 16,
            MaxResidentChunks = SurfaceArenaSlots,
            MaxIndicesPerChunk = MaxIndicesPerChunk
        };

        private readonly CpuTransvoxelChunkCache _transvoxelCache = new();
        private readonly CpuHardSurfaceChunkCache _hardSurfaceCache =
            new(RecoveryBricksPerChunkAxis);
        private readonly CpuWaterSurfaceChunkCache _waterSurfaceCache = new();
        private GpuSurfaceArena _surfaceArena;
        private bool _transvoxelActivated;

        private ComputeShader _surfaceExtraction;
        private ComputeShader _densityCompute;
        private int _densityKernel = -1;
        private Material _surfaceMaterial;
        private Material _waterMaterial;
        private readonly MaterialPropertyBlock _surfaceProperties = new();
        private readonly MaterialPropertyBlock _waterProperties = new();
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
                          Shader waterShader = null,
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
            CoreUtils.Destroy(_waterMaterial);
            _surfaceMaterial = surfaceShader != null
                ? CoreUtils.CreateEngineMaterial(surfaceShader) : null;
            _waterMaterial = waterShader != null
                ? CoreUtils.CreateEngineMaterial(waterShader) : null;

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

            if (_waterMaterial != null && skyTexture != null)
                _waterMaterial.SetTexture("_SkyTexture", skyTexture);
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
            public Material WaterMaterial;
            public MaterialPropertyBlock Properties;
            public MaterialPropertyBlock WaterProperties;
            public Vector4[] MaterialColours;
            public Color BaseColor;
            public Vector4 SunDirection;
            public Vector4 SkyHorizon;
            public Vector4 SkyZenith;
            public Vector4 CameraPosition;
            public float WaterTime;
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
            public CpuTransvoxelChunkCache.Entry[] TransvoxelEntries;
            public CpuHardSurfaceChunkCache.Entry[] HardEntries;
            public CpuWaterSurfaceChunkCache.Entry[] WaterEntries;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Enabled) return;
            if (!VoxelRenderBridge.TryGetWorld(out var world)) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (camera.cameraType == CameraType.Preview) return;

            // Derived caches consume edit invalidation before VoxelGpuBuffers drains the shared set.
            // Their old ready meshes stay visible while replacements are built.
            var dirtyRegions = VoxelRenderBridge.RegionsNeedingUpload;
            _hardSurfaceCache.Sync(ref world.Table, in world.Pool, dirtyRegions,
                                   camera.transform.position, VoxelSize);
            _transvoxelCache.InvalidateDirtyRegions(dirtyRegions);
            _waterSurfaceCache.InvalidateDirtyRegions(dirtyRegions);

            _buffers.Sync(ref world.Table, ref world.Pool, world.CameraRegion, dirtyRegions);

            // The showcase castle predates explicit surface semantics. Bootstrap from authored
            // accent materials, but tag actual structure-looking bricks rather than claiming the
            // surrounding render chunks.
            int newlyTagged = LegacyHardSurfaceClassifier.TagAuthoredSurfaceBricks(
                ref world.Table, in world.Pool, _buffers.LastSurfaceWorldBricks);
            if (newlyTagged > 0)
                _hardSurfaceCache.Sync(ref world.Table, in world.Pool, null,
                                       camera.transform.position, VoxelSize, 12.0);

            if (_surfaceExtraction == null || _surfaceMaterial == null) return;

            EnsureSurfaceArena();

            _transvoxelCache.InvalidateSurfaceBricks(_buffers.LastSurfaceWorldBricks);
            _transvoxelCache.Prepare(ref world.Table, in world.Pool, camera,
                                     VoxelSize, Time.frameCount);
            var transvoxelVisible = _transvoxelCache.CollectVisible(camera, VoxelSize,
                                                                    Time.frameCount);

            _waterSurfaceCache.InvalidateSurfaceBricks(ref world.Table, in world.Pool,
                                                       _buffers.LastSurfaceWorldBricks);
            _waterSurfaceCache.Prepare(ref world.Table, in world.Pool, camera, VoxelSize);
            var waterVisible = _waterSurfaceCache.CollectVisible(camera, VoxelSize);

            if (!_transvoxelActivated
                && _transvoxelCache.KnownCount > 0
                && _transvoxelCache.ResidentCount >= _transvoxelCache.KnownCount
                && _transvoxelCache.DirtyCount == 0)
            {
                _transvoxelActivated = true;
            }

            _surfaceCache.InvalidateSurfaceBricks(_buffers.LastSurfaceWorldBricks);
            _surfaceCache.InvalidateDensityBricks(_buffers.LastDensityWorldBricks);
            _surfaceCache.Prepare(camera, VoxelSize, Time.frameCount);
            _surfaceCache.CollectVisible(camera, VoxelSize, Time.frameCount);
            var hardVisible = _hardSurfaceCache.CollectVisible(camera, VoxelSize);
            // Hard and smooth derived layers may coexist. Surface Nets remains only as a
            // temporary smooth-terrain warmup source until Transvoxel is ready.
            int coarseVisibleCount = 0;
            for (int i = 0; i < _surfaceCache.Visible.Count; i++)
            {
                int3 coordinate = _surfaceCache.Visible[i].Coordinate;
                bool hardReady = _hardSurfaceCache.OwnsRenderedChunk(coordinate);
                bool transvoxelReady = _transvoxelCache.OwnsRenderedChunk(coordinate);
                bool drawTransvoxel = transvoxelReady
                    && (_transvoxelActivated || hardReady);

                if (drawTransvoxel) continue;
                if (hardReady) continue;
                coarseVisibleCount++;
            }

            var visibleEntries = new GpuSurfaceChunkCache.Entry[coarseVisibleCount];
            int coarseWrite = 0;
            for (int i = 0; i < _surfaceCache.Visible.Count; i++)
            {
                var entry = _surfaceCache.Visible[i];
                bool hardReady = _hardSurfaceCache.OwnsRenderedChunk(entry.Coordinate);
                bool transvoxelReady = _transvoxelCache.OwnsRenderedChunk(entry.Coordinate);
                bool drawTransvoxel = transvoxelReady
                    && (_transvoxelActivated || hardReady);
                if (drawTransvoxel || hardReady) continue;
                visibleEntries[coarseWrite++] = entry;
            }

            int transvoxelCount = 0;
            for (int i = 0; i < transvoxelVisible.Count; i++)
            {
                int3 coordinate = transvoxelVisible[i].Coordinate;
                if (_transvoxelActivated
                    || _hardSurfaceCache.OwnsRenderedChunk(coordinate)
)
                    transvoxelCount++;
            }

            var transvoxelEntries = new CpuTransvoxelChunkCache.Entry[transvoxelCount];
            int transvoxelWrite = 0;
            for (int i = 0; i < transvoxelVisible.Count; i++)
            {
                var entry = transvoxelVisible[i];
                if (!_transvoxelActivated
                    && !_hardSurfaceCache.OwnsRenderedChunk(entry.Coordinate)
)
                    continue;
                transvoxelEntries[transvoxelWrite++] = entry;
            }

            var hardEntries = new CpuHardSurfaceChunkCache.Entry[hardVisible.Count];
            for (int i = 0; i < hardVisible.Count; i++) hardEntries[i] = hardVisible[i];

            var waterEntries = new CpuWaterSurfaceChunkCache.Entry[waterVisible.Count];
            for (int i = 0; i < waterVisible.Count; i++) waterEntries[i] = waterVisible[i];

            using var builder = renderGraph.AddUnsafePass(k_PassName, out SurfaceComputeFrameData data);

            data.Material = _surfaceMaterial;
            data.WaterMaterial = _waterMaterial;
            data.Properties = _surfaceProperties;
            data.WaterProperties = _waterProperties;
            data.Buffers = _buffers;
            data.CameraColor = resourceData.activeColorTexture;
            data.CameraDepth = resourceData.activeDepthTexture;
            data.DensityCompute = _densityCompute;
            data.DensityKernel = _densityKernel;
            data.SunDirection = VoxelRenderBridge.SunDirection;
            data.SkyHorizon = VoxelRenderBridge.SkyHorizon;
            data.SkyZenith = VoxelRenderBridge.SkyZenith;
            Vector3 cameraPosition = camera.transform.position;
            data.CameraPosition = new Vector4(cameraPosition.x, cameraPosition.y,
                                              cameraPosition.z, 1f);
            data.WaterTime = Time.time;
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
            data.TransvoxelEntries = transvoxelEntries;
            data.HardEntries = hardEntries;
            data.WaterEntries = waterEntries;

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

                for (int i = 0; i < passData.TransvoxelEntries.Length; i++)
                    passData.TransvoxelEntries[i].Draw(cmd, passData.Material,
                                                       passData.Properties);

                for (int i = 0; i < passData.HardEntries.Length; i++)
                    passData.HardEntries[i].Draw(cmd, passData.Material, passData.Properties);

                if (passData.WaterMaterial != null && passData.WaterEntries.Length > 0)
                {
                    passData.WaterProperties.Clear();
                    passData.WaterProperties.SetVector(s_CameraPosition, passData.CameraPosition);
                    passData.WaterProperties.SetVector(s_SunDirection, passData.SunDirection);
                    passData.WaterProperties.SetVector(s_SkyHorizon, passData.SkyHorizon);
                    passData.WaterProperties.SetVector(s_SkyZenith, passData.SkyZenith);
                    passData.WaterProperties.SetFloat(s_WaterTime, passData.WaterTime);
                    for (int i = 0; i < passData.WaterEntries.Length; i++)
                        passData.WaterEntries[i].Draw(cmd, passData.WaterMaterial,
                                                      passData.WaterProperties);
                }
            });
        }

        public void Dispose()
        {
            _waterSurfaceCache.Dispose();
            _hardSurfaceCache.Dispose();
            _transvoxelCache.Dispose();
            _surfaceCache.Dispose();
            _surfaceArena?.Dispose();
            _surfaceArena = null;
            _buffers.Dispose();
            CoreUtils.Destroy(_surfaceMaterial);
            CoreUtils.Destroy(_waterMaterial);
            _surfaceMaterial = null;
            _waterMaterial = null;
        }
    }
}
