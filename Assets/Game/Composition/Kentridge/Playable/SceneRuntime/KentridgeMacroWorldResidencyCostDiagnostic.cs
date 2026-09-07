using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only cost evidence for feature-aware vertical residency. It observes the real
    /// standalone player's resident-region set and compares it with the exact terrain/camera
    /// baseline used by ShowcaseWorld. It never changes residency, streaming, generation, or
    /// presentation state.
    /// </summary>
    [DefaultExecutionOrder(20010)]
    internal sealed class KentridgeMacroWorldResidencyCostDiagnostic : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const int SurveyHorizontalOffsetDm = 60;
        private const float CameraMatchToleranceMetres = 2f;
        private const int MaxSurfaceLayersPerColumn = 3;
        private const int TerrainSurfaceMarginVoxels = 8;

        private static readonly FieldInfo s_WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private Survey[] _surveys;
        private KentridgePlayableSlice _slice;
        private ShowcaseWorld _world;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro Residency Cost Diagnostic");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldResidencyCostDiagnostic>();
        }

        private void Awake()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            _surveys = new[]
            {
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.Moordell),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.Rossdam),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.FairyVillage),
                BuildSurvey(physical, MountingForceTopDownWorldDefinition.OrcVillage)
            };
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null || _surveys == null) return;

            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            if (_slice == null || s_WorldField == null) return;
            _world ??= s_WorldField.GetValue(_slice) as ShowcaseWorld;
            if (_world == null) return;

            Vector3 position = camera.transform.position;
            for (var surveyIndex = 0; surveyIndex < _surveys.Length; surveyIndex++)
            {
                Survey survey = _surveys[surveyIndex];
                if (_reported.Contains(survey.Label)) continue;

                float dx = position.x - survey.CameraDm.X * DmToMetres;
                float dz = position.z - survey.CameraDm.Y * DmToMetres;
                if (dx * dx + dz * dz > CameraMatchToleranceMetres * CameraMatchToleranceMetres)
                    continue;
                if (!IsSettlementContentSettled(survey)) return;

                ReportResidency(survey.Label, position);
                _reported.Add(survey.Label);
                return;
            }
        }

        private bool IsSettlementContentSettled(Survey survey)
        {
            for (var i = 0; i < survey.Buildings.Length; i++)
            {
                Int2 centreDm = survey.Buildings[i].CentreDm;
                int ground = TerrainSampler.HeightAt(centreDm.X, centreDm.Y, Seed);
                var worldPoint = new Vector3(
                    centreDm.X * DmToMetres,
                    ground * DmToMetres,
                    centreDm.Y * DmToMetres);
                if (!_world.IsPresentationColumnContentSettled(worldPoint)) return false;
            }
            return true;
        }

        private void ReportResidency(string label, Vector3 cameraMetres)
        {
            IRegionReadSource reads = _world.ReadStorage;
            using NativeArray<int3> resident = reads.GetResidentRegionCoords(Allocator.Temp);
            int3 centre = ShowcaseWorld.RegionAt(cameraMetres);
            int radius = _world.LoadRadiusRegions;
            int expectedHorizontalColumns = 0;
            int residentInRadius = 0;
            int baselineResident = 0;
            int featureVerticalExtra = 0;
            var extraLayersByColumn = new Dictionary<int2, int>();

            for (var dx = -radius; dx <= radius; dx++)
            for (var dz = -radius; dz <= radius; dz++)
                if (dx * dx + dz * dz <= radius * radius)
                    expectedHorizontalColumns++;

            for (var i = 0; i < resident.Length; i++)
            {
                int3 region = resident[i];
                int dx = region.x - centre.x;
                int dz = region.z - centre.z;
                if (dx * dx + dz * dz > radius * radius) continue;

                residentInRadius++;
                ComputeSurfaceLayerSpan(region.x, region.z, out int minLayer, out int maxLayer);
                if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                    maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                bool baseline = region.y >= minLayer && region.y <= maxLayer;
                if (!baseline && region.y == centre.y) baseline = true;
                if (baseline)
                {
                    baselineResident++;
                    continue;
                }

                featureVerticalExtra++;
                var column = new int2(region.x, region.z);
                extraLayersByColumn.TryGetValue(column, out int count);
                extraLayersByColumn[column] = count + 1;
            }

            int maxExtraPerColumn = 0;
            foreach (KeyValuePair<int2, int> pair in extraLayersByColumn)
                maxExtraPerColumn = math.max(maxExtraPerColumn, pair.Value);

            Debug.Log(
                $"MACROEVIDENCE residency-cost target={label} loadRadius={radius} " +
                $"horizontalColumns={expectedHorizontalColumns} totalResidentSnapshot={resident.Length} " +
                $"residentInRadius={residentInRadius} baselineResident={baselineResident} " +
                $"featureVerticalExtra={featureVerticalExtra} extraColumns={extraLayersByColumn.Count} " +
                $"maxExtraPerColumn={maxExtraPerColumn}");
        }

        private static void ComputeSurfaceLayerSpan(
            int regionX,
            int regionZ,
            out int minLayer,
            out int maxLayer)
        {
            int originX = regionX * ShowcaseWorld.RegionVoxelEdge;
            int originZ = regionZ * ShowcaseWorld.RegionVoxelEdge;
            int lowest = int.MaxValue;
            int highest = int.MinValue;
            int step = ShowcaseWorld.RegionVoxelEdge / 8;

            for (var z = 0; z <= ShowcaseWorld.RegionVoxelEdge; z += step)
            for (var x = 0; x <= ShowcaseWorld.RegionVoxelEdge; x += step)
            {
                int height = TerrainSampler.HeightAt(originX + x, originZ + z, Seed);
                lowest = math.min(lowest, height);
                highest = math.max(highest, height);
            }

            // ShowcaseWorld expands the sampled surface by one 8-voxel storage brick.
            lowest -= TerrainSurfaceMarginVoxels;
            highest += TerrainSurfaceMarginVoxels;
            minLayer = lowest >> VoxelGrid.RegionVoxelEdgeLog2;
            maxLayer = highest >> VoxelGrid.RegionVoxelEdgeLog2;
            if (minLayer < 0) minLayer = 0;
            if (maxLayer < minLayer) maxLayer = minLayer;
        }

        private static Survey BuildSurvey(TopDownWorldPhysicalPlan physical, string nodeId)
        {
            if (!physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement))
                throw new InvalidOperationException("Residency diagnostic has no settlement '" + nodeId + "'.");

            var buildings = new TopDownWorldBuildingBlockoutPlan[settlement.Buildings.Count];
            TopDownWorldBuildingBlockoutPlan first = settlement.Buildings[0];
            buildings[0] = first;
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                buildings[i] = building;
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            var focusDm = new Int2((minX + maxX) / 2, (minZ + maxZ) / 2);
            return new Survey(
                nodeId,
                new Int2(focusDm.X + SurveyHorizontalOffsetDm, focusDm.Y + SurveyHorizontalOffsetDm),
                buildings);
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = null;
            string path = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return false;
            string json = System.IO.File.ReadAllText(path);
            const string key = "\"validationProfile\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int colon = json.IndexOf(':', keyIndex + key.Length);
            int firstQuote = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int secondQuote = firstQuote >= 0 ? json.IndexOf('"', firstQuote + 1) : -1;
            if (firstQuote < 0 || secondQuote <= firstQuote) return false;
            profile = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            return true;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        private readonly struct Survey
        {
            public string Label { get; }
            public Int2 CameraDm { get; }
            public TopDownWorldBuildingBlockoutPlan[] Buildings { get; }

            public Survey(string label, Int2 cameraDm, TopDownWorldBuildingBlockoutPlan[] buildings)
            {
                Label = label;
                CameraDm = cameraDm;
                Buildings = buildings;
            }
        }
    }
}
