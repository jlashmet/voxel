using System;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Module-owned standalone proof for the top-down physical world path used by the shipped
    /// Kentridge composition. The scene supplies only deterministic inputs, camera motion and
    /// assertions; planning, reservation validation, voxel realization, storage, streaming and
    /// rendering all use production systems.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WorldBuilderTopDownPhysicalWorldValidation : MonoBehaviour
    {
        private const float DecimetresToMetres = 0.1f;
        private const int StableReadyFrames = 4;

        private readonly struct SurveyTarget
        {
            public readonly string Label;
            public readonly Int2 CentreDm;

            public SurveyTarget(string label, Int2 centreDm)
            {
                Label = label;
                CentreDm = centreDm;
            }
        }

        [SerializeField] private uint m_Seed = 0x4B454E54u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

        private ShowcaseWorld _world;
        private SurveyTarget[] _targets;
        private int _targetIndex;
        private int _stableFrames;
        private bool _ready;
        private bool _complete;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            BuildProductionWorld();
        }

        private void Update()
        {
            if (!_ready || _complete || _world == null) return;

            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
            SurveyTarget target = _targets[_targetIndex];
            Vector3 focus = SurfacePoint(target.CentreDm, 4f);
            var targetRegion = ShowcaseWorld.RegionAt(focus);
            bool hasPublishedCoverage = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
            if (!_world.IsGenerated(targetRegion) || !hasPublishedCoverage)
            {
                _stableFrames = 0;
                return;
            }

            _stableFrames++;
            if (_stableFrames < StableReadyFrames) return;

            Debug.Log(
                "WORLDBUILDER_MACRO_PHYSICAL_RENDER target=" + target.Label +
                " PASS coverage=" + hasPublishedCoverage);

            _targetIndex++;
            _stableFrames = 0;
            if (_targetIndex >= _targets.Length)
            {
                _complete = true;
                Debug.Log("WORLDBUILDER_MACRO_PHYSICAL_VALIDATION PASS");
                return;
            }

            PlaceCamera(_targets[_targetIndex]);
        }

        private void OnDisable()
        {
            _ready = false;
            _complete = false;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
            _targets = null;
        }

        private void BuildProductionWorld()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(m_Seed);
            TopDownWorldPhysicalIntentSpec intent = KentridgeTopDownWorldPhysicalIntent.Build();
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                intent,
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);
            TopDownWorldPhysicalReservationAdapter.Validate(physical);

            _targets = BuildTargets(physical);
            VoxelWorldGenSettings settings = BuildSettings();

            FeatureCatalogue macro = default;
            FeatureCatalogue water = default;
            FeatureCatalogue combined = default;
            try
            {
                macro = TopDownWorldPhysicalVoxelCatalogue.Build(
                    layout,
                    intent,
                    KentridgeDefinition.TownCentreDm,
                    MountingForceTopDownWorldDefinition.CellSizeDm,
                    settings,
                    Allocator.Temp,
                    includeWaterBodies: false);
                water = TopDownWorldWaterBodyVoxelCatalogue.Build(
                    physical,
                    m_Seed,
                    settings,
                    Allocator.Temp);
                if (!water.IsCreated)
                    throw new InvalidOperationException(
                        "The recovered macro world no longer realizes its authored WaterBody region.");

                combined = SettlementCatalogueCombiner.Combine(
                    Allocator.Persistent,
                    macro,
                    water);
                ValidateCatalogue(combined, physical);

                _world = new ShowcaseWorld(
                    m_Seed,
                    m_BrickPoolCapacity,
                    m_LoadRadiusRegions,
                    m_UnloadRadiusRegions);
                _world.ConfigureGeneratedContentForGameplay(combined);
                combined = default;
            }
            finally
            {
                if (macro.IsCreated) macro.Dispose();
                if (water.IsCreated) water.Dispose();
                if (combined.IsCreated) combined.Dispose();
            }

            RenderingComposition.ResetSurfacePassDiagnostics(
                "worldbuilder-topdown-physical-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(
                m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);
            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld,
                _world.Changes,
                _world.Seed,
                farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _targetIndex = 0;
            _stableFrames = 0;
            _complete = false;
            PlaceCamera(_targets[0]);
            _ready = true;

            Debug.Log(
                "WORLDBUILDER_MACRO_PHYSICAL_READY settlements=" + physical.Settlements.Count +
                " routes=" + physical.Routes.Count +
                " buildings=" + physical.BuildingCount +
                " targets=" + _targets.Length);
        }

        private static SurveyTarget[] BuildTargets(TopDownWorldPhysicalPlan physical)
        {
            if (physical == null) throw new ArgumentNullException(nameof(physical));
            if (physical.Settlements.Count < 2 || physical.Routes.Count == 0 || physical.BuildingCount == 0)
                throw new InvalidOperationException(
                    "Top-down physical validation requires a multi-settlement routed world with buildings.");

            if (!physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Moordell,
                    out TopDownWorldSettlementPlan moordell))
                throw new InvalidOperationException("Recovered macro world has no Moordell settlement.");
            if (!physical.TryGetSettlement(
                    MountingForceTopDownWorldDefinition.Rossdam,
                    out TopDownWorldSettlementPlan rossdam))
                throw new InvalidOperationException("Recovered macro world has no Rossdam settlement.");

            TopDownWorldRegionPlan water = null;
            for (var i = 0; i < physical.Regions.Count; i++)
            {
                if (physical.Regions[i].Spec.Kind != TopDownWorldRegionKind.WaterBody) continue;
                water = physical.Regions[i];
                break;
            }
            if (water == null)
                throw new InvalidOperationException("Recovered macro world has no WaterBody region.");

            return new[]
            {
                new SurveyTarget("moordell", SettlementFocus(moordell)),
                new SurveyTarget("rossdam", SettlementFocus(rossdam)),
                new SurveyTarget("water", water.CentreDm),
            };
        }

        private static Int2 SettlementFocus(TopDownWorldSettlementPlan settlement) =>
            settlement.Buildings.Count > 0
                ? settlement.Buildings[0].CentreDm
                : settlement.CentreDm;

        private static void ValidateCatalogue(
            FeatureCatalogue catalogue,
            TopDownWorldPhysicalPlan physical)
        {
            if (!catalogue.IsCreated)
                throw new InvalidOperationException("Top-down physical catalogue was not created.");

            int roads = CountDefinitions(catalogue, "macro-road-");
            int moordell = CountDefinitions(
                catalogue,
                "macro-town-building-" + MountingForceTopDownWorldDefinition.Moordell + "-");
            int rossdam = CountDefinitions(
                catalogue,
                "macro-town-building-" + MountingForceTopDownWorldDefinition.Rossdam + "-");
            int water = CountDefinitions(
                catalogue,
                TopDownWorldWaterBodyVoxelCatalogue.DefinitionPrefix);
            if (roads == 0 || moordell == 0 || rossdam == 0 || water == 0)
                throw new InvalidOperationException(
                    "Production macro catalogue is missing required road/town/water realization: " +
                    $"roads={roads} moordell={moordell} rossdam={rossdam} water={water}.");

            Debug.Log(
                "WORLDBUILDER_MACRO_PHYSICAL_CATALOGUE PASS definitions=" + catalogue.Definitions.Length +
                " placements=" + catalogue.ExplicitPlacements.Length +
                " roads=" + roads +
                " moordell=" + moordell +
                " rossdam=" + rossdam +
                " water=" + water +
                " relaxations=" + physical.ConstraintRelaxationCount);
        }

        private static int CountDefinitions(FeatureCatalogue catalogue, string prefix)
        {
            int count = 0;
            for (var i = 0; i < catalogue.Definitions.Length; i++)
            {
                string name = catalogue.Definitions[i].Name.ToString();
                if (name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        private void PlaceCamera(SurveyTarget target)
        {
            Vector3 focus = SurfacePoint(target.CentreDm, 5f);
            transform.position = focus + new Vector3(-24f, 24f, -24f);
            Vector3 direction = focus - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private Vector3 SurfacePoint(Int2 point, float heightMetres)
        {
            int ground = TerrainSampler.HeightAt(point.X, point.Y, m_Seed);
            return new Vector3(
                point.X * DecimetresToMetres,
                ground * DecimetresToMetres + heightMetres,
                point.Y * DecimetresToMetres);
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 20,
                masonry: 18,
                darkMasonry: 6,
                timber: 2,
                glass: 4,
                warmWindow: 15,
                roofTile: 8,
                slate: 7,
                cloth: 9,
                moss: 14,
                water: 11,
                roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
