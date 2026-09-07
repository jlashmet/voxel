using System;
using System.Collections.Generic;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Validation
{
    /// <summary>
    /// Module-owned built-player proof for the reusable architecture contract. It consumes the same
    /// registry/provider, Storage, authoring and renderer path as game composition while keeping
    /// validation-specific camera placement outside the production architecture system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ArchitectureFoundationRuntimeValidation : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const uint CanonicalSeed = 0x484F5553u;
        private const uint StructureId = 0x41524348u;
        private const uint SeedStep = 0x9E3779B9u;
        private const int VariantSpacing = 176;
        private const int VariantCount = 4;

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private ArchitectureGenerationResult[] _results = Array.Empty<ArchitectureGenerationResult>();
        private uint[] _seeds = Array.Empty<uint>();
        private float _started;
        private int _phase;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            ConfigurePresentation();

            if (!ArchitectureRegistry.TryGetProvider(
                    GuildHouseArchitectureProvider.ProviderKey,
                    out IArchitectureProvider provider))
                Fail("generic architecture registry did not expose the production guild-house provider");

            ArchitectureStyleProfileDescriptor[] profiles = ArchitectureRegistry.StyleProfiles();
            ArchitectureProviderDescriptor[] providers = ArchitectureRegistry.ProviderDescriptors();
            if (profiles.Length < 6 || providers.Length < 1)
                Fail($"architecture discovery incomplete profiles={profiles.Length} providers={providers.Length}");

            var parameters = new ArchitectureGenerationParameters(
                width: 0,
                depth: 0,
                storeyCount: 2,
                floorHeight: 30,
                roomCount: 5);
            ArchitectureGenerationRequest canonical = Request(CanonicalSeed, int3.zero, in parameters);
            if (!provider.TryPlan(in canonical, out ArchitectureGenerationResult firstPlan, out string firstError))
                Fail("canonical generic plan rejected: " + firstError);
            if (!provider.TryPlan(in canonical, out ArchitectureGenerationResult repeatedPlan, out string repeatError))
                Fail("repeated canonical generic plan rejected: " + repeatError);
            if (firstPlan.Identity.RequestHash != repeatedPlan.Identity.RequestHash ||
                firstPlan.Identity.RealizationHash != repeatedPlan.Identity.RealizationHash)
                Fail("same semantic request did not reproduce identical architecture identity");

            FindFourDistinctVariants(provider, in parameters, out _seeds, out ArchitectureGenerationResult[] planned);
            int distinctFootprints = CountDistinctFootprints(planned);
            if (distinctFootprints < 2)
                Fail("four deterministic seeds did not create meaningful footprint/proportion variation");

            BuildFourVariants(provider, in parameters);
            ValidateCanonicalVoxelTruth(_results[0]);
            FrameAll();
            _started = Time.unscaledTime;

            ArchitecturePresentationCapabilities required =
                ArchitecturePresentationCapabilities.ProductionVoxelOccupancy |
                ArchitecturePresentationCapabilities.ProductionMaterials |
                ArchitecturePresentationCapabilities.ProductionMaterialTextures |
                ArchitecturePresentationCapabilities.SignedDistancePresentation |
                ArchitecturePresentationCapabilities.CollisionFromOccupancy |
                ArchitecturePresentationCapabilities.InteriorShell |
                ArchitecturePresentationCapabilities.TraversableOpenings |
                ArchitecturePresentationCapabilities.MultiStoreyCirculation;
            if ((_results[0].Summary.Capabilities & required) != required)
                Fail($"canonical production summary missing required capabilities: {_results[0].Summary.Capabilities}");

            Debug.Log(
                $"ARCHITECTURE_FOUNDATION_VALIDATION start profiles={profiles.Length} providers={providers.Length} " +
                $"provider={GuildHouseArchitectureProvider.ProviderKey} archetype=adventurers deterministic=true " +
                $"variants={VariantCount} distinctFootprints={distinctFootprints}");
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            float elapsed = Time.unscaledTime - _started;
            if (_phase == 0 && elapsed >= 5f)
            {
                FrameCanonicalExterior();
                Debug.Log(
                    $"ARCHITECTURE_FOUNDATION_VALIDATION canonical seed={_seeds[0]} " +
                    $"requestHash={_results[0].Identity.RequestHash:X16} realizationHash={_results[0].Identity.RealizationHash:X16} " +
                    $"bounds={_results[0].Summary.Min}->{_results[0].Summary.MaxExclusive}");
                _phase = 1;
            }
            else if (_phase == 1 && elapsed >= 10f)
            {
                FrameCanonicalInterior();
                Debug.Log(
                    $"ARCHITECTURE_FOUNDATION_VALIDATION interior walkable=true glass=true sdfBoundary=true " +
                    $"storeys={_results[0].Summary.StoreyCount} stairs={_results[0].Summary.StairCount}");
                _phase = 2;
            }
            else if (_phase == 2 && elapsed >= 15f)
            {
                FrameAll();
                Debug.Log(
                    $"ARCHITECTURE_FOUNDATION_VALIDATION complete seeds={_seeds[0]},{_seeds[1]},{_seeds[2]},{_seeds[3]} " +
                    $"hashes={_results[0].Identity.RealizationHash:X16},{_results[1].Identity.RealizationHash:X16}," +
                    $"{_results[2].Identity.RealizationHash:X16},{_results[3].Identity.RealizationHash:X16} " +
                    "productionPath=true cleanupOwned=true");
                _phase = 3;
            }
        }

        private static ArchitectureGenerationRequest Request(
            uint seed,
            int3 origin,
            in ArchitectureGenerationParameters parameters) =>
            new ArchitectureGenerationRequest(
                "kentridge",
                GuildHouseArchitectureProvider.ProviderKey,
                "adventurers",
                seed,
                StructureId,
                origin,
                parameters);

        private static void FindFourDistinctVariants(
            IArchitectureProvider provider,
            in ArchitectureGenerationParameters parameters,
            out uint[] seeds,
            out ArchitectureGenerationResult[] results)
        {
            seeds = new uint[VariantCount];
            results = new ArchitectureGenerationResult[VariantCount];
            var hashes = new HashSet<ulong>();
            int count = 0;
            for (uint attempt = 0; attempt < 256 && count < VariantCount; attempt++)
            {
                uint seed = attempt == 0 ? CanonicalSeed : unchecked(CanonicalSeed + attempt * SeedStep);
                ArchitectureGenerationRequest request = Request(seed, int3.zero, in parameters);
                if (!provider.TryPlan(in request, out ArchitectureGenerationResult result, out _))
                    continue;
                if (!hashes.Add(result.Identity.RealizationHash))
                    continue;
                seeds[count] = seed;
                results[count] = result;
                count++;
            }

            if (count != VariantCount)
                Fail($"could not discover {VariantCount} distinct production architecture seeds; found {count}");
        }

        private static int CountDistinctFootprints(ArchitectureGenerationResult[] results)
        {
            var keys = new HashSet<long>();
            for (int i = 0; i < results.Length; i++)
            {
                int width = results[i].Summary.MaxExclusive.x - results[i].Summary.Min.x;
                int depth = results[i].Summary.MaxExclusive.z - results[i].Summary.Min.z;
                keys.Add(((long)width << 32) | (uint)depth);
            }
            return keys.Count;
        }

        private void BuildFourVariants(
            IArchitectureProvider provider,
            in ArchitectureGenerationParameters parameters)
        {
            _storage = VoxelEngineBootstrap.CreateStorage(16, 64_000);
            RegisterMaterials(_storage);
            IStructureAuthoringSession authoring = VoxelEngineBootstrap.CreateStructureAuthoring(_storage, 20_000_000);
            _results = new ArchitectureGenerationResult[VariantCount];

            for (int i = 0; i < VariantCount; i++)
            {
                int3 origin = new int3(i * VariantSpacing, 16, 0);
                ArchitectureGenerationRequest request = Request(_seeds[i], origin, in parameters);
                if (!provider.TryAuthor(authoring, in request, out _results[i], out string error))
                    Fail($"variant {i} production authoring failed: {error}");
                if (!_results[i].IsWellFormed || _results[i].Summary.StoreyCount != 2 ||
                    _results[i].Summary.StairCount != 1 || !_results[i].Summary.HasInteriorShell)
                    Fail($"variant {i} returned invalid structural summary");
            }
            if (authoring.BudgetExceeded)
                Fail("four-variant production authoring exceeded validation voxel budget");

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world,
                _storage.Changes,
                CanonicalSeed,
                solidBuildBudgetMs: 12.0,
                waterBuildBudgetMs: 0.0,
                farFieldEnabled: false);

            Debug.Log($"ARCHITECTURE_FOUNDATION_VALIDATION authored voxels={authoring.TotalVoxelsWritten} variants={VariantCount}");
        }

        private void ValidateCanonicalVoxelTruth(in ArchitectureGenerationResult result)
        {
            ArchitectureRealizationSummary summary = result.Summary;
            int width = summary.MaxExclusive.x - summary.Min.x;
            int doorX = summary.Min.x + width / 2;
            int floorY = summary.Min.y;

            // The production player is 1.8 m tall. Probe a 1.8 m-high standing volume from outside
            // through the centered entrance and six voxels into the authoritative shell.
            for (int z = summary.Min.z - 1; z <= summary.Min.z + 6; z++)
            {
                VoxelCell floor = ReadWorldCell(new int3(doorX, floorY + 1, z));
                if (z >= summary.Min.z && floor.BaseMaterialId == VoxelGrid.MaterialEmpty)
                    Fail($"entrance traversal lost its authoritative floor at x={doorX} y={floorY + 1} z={z}");
                for (int y = floorY + 2; y < floorY + 20; y++)
                {
                    VoxelCell cell = ReadWorldCell(new int3(doorX, y, z));
                    if (cell.BaseMaterialId != VoxelGrid.MaterialEmpty)
                        Fail($"entrance/interior standing volume blocked at x={doorX} y={y} z={z} material={cell.BaseMaterialId}");
                }
            }

            int windowWidth = math.max(6, math.min(10, width / 10));
            int leftX = summary.Min.x + width / 4 - windowWidth / 2;
            int windowY = summary.Min.y + 10;
            VoxelCell glass = ReadWorldCell(new int3(leftX + windowWidth / 2, windowY + 2, summary.Min.z + 1));
            if (glass.BaseMaterialId != GameMaterialIds.Glass)
                Fail($"production window opening did not contain Glass; material={glass.BaseMaterialId}");

            int doorLeft = summary.Min.x + (width - 12) / 2;
            int3 canopyMin = new int3(doorLeft - 7, summary.Min.y + 2 + 24 + 3, summary.Min.z - 7);
            bool foundBoundary = false;
            for (int x = canopyMin.x - 2; x < canopyMin.x + 28 && !foundBoundary; x++)
            for (int y = canopyMin.y - 2; y < canopyMin.y + 7 && !foundBoundary; y++)
            for (int z = canopyMin.z - 2; z < canopyMin.z + 13; z++)
            {
                if (ReadWorldCell(new int3(x, y, z)).Boundary.Packed == 0) continue;
                foundBoundary = true;
                break;
            }
            if (!foundBoundary)
                Fail("production curved entrance canopy emitted no signed-distance boundary samples");
        }

        private VoxelCell ReadWorldCell(int3 worldVoxel)
        {
            int3 regionCoord = worldVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
            if (!_storage.Reads.TryAcquireRegion(regionCoord, out RegionReadView view))
                return default;
            int3 local = worldVoxel - (regionCoord << VoxelGrid.RegionVoxelEdgeLog2);
            if (!view.TryReadCell(local, out VoxelCell cell))
                Fail($"Storage read view rejected local voxel {local} for world voxel {worldVoxel}");
            return cell;
        }

        private static void RegisterMaterials(IVoxelStorageRuntime storage)
        {
            MaterialDefinition[] materials = GameMaterialComposition.SimulationDefinitions();
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialDefinition material = materials[i];
                storage.RegisterMaterial(
                    material.MaterialId,
                    material.Hardness,
                    material.DestructionClass,
                    material.DefaultSurfaceStyle,
                    material.AllowedCoatings);
            }
        }

        private void ConfigurePresentation()
        {
            GameMaterialComposition.Install();
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetSky(
                new Color(0.66f, 0.76f, 0.84f, 1f),
                new Color(0.30f, 0.48f, 0.66f, 1f));
            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.48f, 0.80f, -0.35f).normalized,
                new Color(1.0f, 0.91f, 0.74f, 1f),
                new Color(0.45f, 0.53f, 0.60f, 1f));
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.60f, 0.69f, 0.75f, 1f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 320f;
            _camera.allowHDR = false;
        }

        private void FrameAll()
        {
            if (_results.Length != VariantCount) return;
            ArchitectureRealizationSummary first = _results[0].Summary;
            ArchitectureRealizationSummary last = _results[VariantCount - 1].Summary;
            float3 min = (float3)first.Min * VoxelSize;
            float3 max = (float3)last.MaxExclusive * VoxelSize;
            Vector3 focus = new Vector3((min.x + max.x) * 0.5f, min.y + 4.5f, (min.z + max.z) * 0.5f);
            _camera.transform.position = focus + new Vector3(0f, 26f, -52f);
            _camera.fieldOfView = 52f;
            _camera.transform.LookAt(focus);
        }

        private void FrameCanonicalExterior()
        {
            ArchitectureRealizationSummary s = _results[0].Summary;
            Vector3 min = ToWorld(s.Min);
            Vector3 max = ToWorld(s.MaxExclusive);
            Vector3 size = max - min;
            Vector3 focus = min + new Vector3(size.x * 0.5f, size.y * 0.42f, size.z * 0.45f);
            _camera.transform.position = min + new Vector3(size.x * 0.9f, size.y * 0.76f, -20f);
            _camera.fieldOfView = 46f;
            _camera.transform.LookAt(focus);
        }

        private void FrameCanonicalInterior()
        {
            ArchitectureRealizationSummary s = _results[0].Summary;
            int width = s.MaxExclusive.x - s.Min.x;
            Vector3 position = ToWorld(new int3(s.Min.x + width / 2, s.Min.y + 18, s.Min.z + 16));
            Vector3 target = ToWorld(new int3(s.Min.x + width / 2, s.Min.y + 18, s.Min.z + 64));
            _camera.transform.position = position;
            _camera.fieldOfView = 60f;
            _camera.transform.LookAt(target);
        }

        private static Vector3 ToWorld(int3 voxels) =>
            new Vector3(voxels.x * VoxelSize, voxels.y * VoxelSize, voxels.z * VoxelSize);

        private static void Fail(string message)
        {
            Debug.LogError("ARCHITECTURE_FOUNDATION_VALIDATION failure: " + message);
            throw new InvalidOperationException(message);
        }
    }
}
