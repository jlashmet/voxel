using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Showcase-owned player validation for feature-aware vertical residency. It takes a real
    /// production-authored Showcase catalogue entry, moves only that fixture across a vertical
    /// region boundary, and proves ordinary ShowcaseWorld streaming publishes the upper authored
    /// layer while horizontal interest remains the configured radius-one column set.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Feature Residency Probe")]
    [DisallowMultipleComponent]
    public sealed class FeatureResidencyValidationProbe : MonoBehaviour
    {
        private const uint Seed = 0x46524553u;
        private const double StreamingBudgetMs = 5000.0;
        private const int MaximumStreamingSteps = 64;

        private ShowcaseWorld _world;
        private string _status = "building production-authored feature fixture";
        private string _metrics = string.Empty;

        private void Start()
        {
            FeatureCatalogue catalogue = default;
            var timer = Stopwatch.StartNew();
            try
            {
#pragma warning disable CS0618
                catalogue = ShowcaseCatalogue.Build(Seed, Allocator.Persistent);
#pragma warning restore CS0618
                Require(catalogue.IsCreated, "production Showcase catalogue was not created");

                PrepareBoundaryCrossingPlacement(
                    ref catalogue,
                    out ExplicitPlacement placement,
                    out FeatureDefinition definition,
                    out int3 upperRegion,
                    out int3 footprint);

                int presentationLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
                var presentationMetres = new float3(
                    (placement.Position.x + footprint.x / 2f) * ShowcaseWorld.VoxelSize,
                    placement.Position.y * ShowcaseWorld.VoxelSize,
                    (placement.Position.z + footprint.z / 2f) * ShowcaseWorld.VoxelSize);
                int3 presentationRegion = new int3(
                    upperRegion.x,
                    presentationLayer,
                    upperRegion.z);

                Require(upperRegion.y > presentationLayer,
                    "fixture did not cross a vertical region boundary");
                Require(upperRegion.x == presentationRegion.x && upperRegion.z == presentationRegion.z,
                    "vertical-residency fixture must remain in one horizontal residency column");

                _world = new ShowcaseWorld(
                    Seed,
                    brickPoolCapacity: 131072,
                    loadRadiusRegions: 1,
                    unloadRadiusRegions: 2);
                _world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default;

                _world.GenerateRegionBlocking(presentationRegion);
                Require(_world.IsCurrentDemandContentSettled(presentationMetres),
                    "lower presentation layer did not establish the readiness discriminator");
                Require(!_world.IsGenerated(upperRegion),
                    "upper authored region was already resident before ordinary streaming");
                Require(!_world.IsPresentationColumnContentSettled(presentationMetres),
                    "column readiness incorrectly ignored the absent authored upper layer");

                int featureVoxelsBefore = _world.FeatureVoxelsBuilt;
                int steps = 0;
                while ((!_world.IsGenerated(upperRegion)
                        || !_world.IsPresentationColumnContentSettled(presentationMetres))
                       && steps++ < MaximumStreamingSteps)
                {
                    _world.StepStreaming(presentationMetres, StreamingBudgetMs);
                }

                Require(_world.IsGenerated(upperRegion),
                    "ordinary streaming did not generate the authored upper region");
                Require(_world.IsPresentationColumnContentSettled(presentationMetres),
                    "column readiness did not converge after upper-layer publication");
                Require(_world.FeatureVoxelsBuilt > featureVoxelsBefore,
                    "upper-region transition did not include real feature rasterization");
                Require(_world.ReadStorage.TryAcquireRegion(upperRegion, out RegionReadView _),
                    "upper authored layer is absent from the authoritative presentation read source");

                timer.Stop();
                long managed = Profiler.GetTotalAllocatedMemoryLong();
                long reserved = Profiler.GetTotalReservedMemoryLong();
                int verticalExtra = upperRegion.y - presentationLayer;
                _metrics =
                    $"feature {definition.Name}  lower y {presentationLayer}  upper y {upperRegion.y}\n" +
                    $"steps {steps}  feature voxels +{_world.FeatureVoxelsBuilt - featureVoxelsBefore}  horizontal radius 1";
                Debug.Log(
                    "FEATURE_RESIDENCY_COST " +
                    $"seconds={timer.Elapsed.TotalSeconds:F3} steps={steps} " +
                    $"feature_voxels_delta={_world.FeatureVoxelsBuilt - featureVoxelsBefore} " +
                    $"vertical_extra_layers={verticalExtra} horizontal_radius=1 " +
                    $"managed_bytes={managed} reserved_bytes={reserved}");
                Debug.Log(
                    "FEATURE_RESIDENCY_VALIDATION ready: " +
                    $"definition={definition.Name} presentation_region={presentationRegion} upper_region={upperRegion} " +
                    $"same_horizontal_column=true upper_readable=true column_settled=true");
                _status = "ready — ordinary streaming published the authored upper layer";
            }
            catch (Exception ex)
            {
                _status = "FAILED — " + ex.Message;
                Debug.LogError("FEATURE_RESIDENCY_VALIDATION FAILED: " + ex);
            }
            finally
            {
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private static void PrepareBoundaryCrossingPlacement(
            ref FeatureCatalogue catalogue,
            out ExplicitPlacement selectedPlacement,
            out FeatureDefinition selectedDefinition,
            out int3 selectedUpperRegion,
            out int3 selectedFootprint)
        {
            selectedPlacement = default;
            selectedDefinition = default;
            selectedUpperRegion = default;
            selectedFootprint = default;
            int selectedPlacementIndex = -1;
            int bestHeight = 1;

            for (var ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.Definitions.Length) continue;
                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                if (definition.Footprint.y <= bestHeight || definition.ProgramLength <= 0) continue;

                int placementStart = math.max(0, rule.ExplicitOffset);
                int placementEnd = math.min(
                    rule.ExplicitOffset + rule.ExplicitCount,
                    catalogue.ExplicitPlacements.Length);
                if (placementStart >= placementEnd) continue;

                selectedPlacementIndex = placementStart;
                selectedDefinition = definition;
                bestHeight = definition.Footprint.y;
            }

            Require(selectedPlacementIndex >= 0,
                "production Showcase catalogue contains no explicit vertically authored feature");

            ExplicitPlacement placement = catalogue.ExplicitPlacements[selectedPlacementIndex];
            int regionEdge = 1 << VoxelGrid.RegionVoxelEdgeLog2;
            int currentLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            placement.Position.y = (currentLayer + 1) * regionEdge - 1;
            catalogue.ExplicitPlacements[selectedPlacementIndex] = placement;

            int3 footprint = selectedDefinition.Footprint;
            if ((placement.Orientation & 1) != 0)
                footprint = new int3(footprint.z, footprint.y, footprint.x);

            int lowerLayer = placement.Position.y >> VoxelGrid.RegionVoxelEdgeLog2;
            int upperLayer = (placement.Position.y + footprint.y - 1)
                             >> VoxelGrid.RegionVoxelEdgeLog2;
            Require(upperLayer > lowerLayer,
                "selected production feature did not cross the deterministic boundary");

            int centreX = placement.Position.x + footprint.x / 2;
            int centreZ = placement.Position.z + footprint.z / 2;
            selectedPlacement = placement;
            selectedFootprint = footprint;
            selectedUpperRegion = new int3(
                centreX >> VoxelGrid.RegionVoxelEdgeLog2,
                upperLayer,
                centreZ >> VoxelGrid.RegionVoxelEdgeLog2);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void OnDestroy()
        {
            _world?.Dispose();
            _world = null;
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(18, 18, 700, 110), "Showcase Feature Residency · Vertical Authored Layer");
            GUI.Label(new Rect(32, 48, 660, 24), _status);
            GUI.Label(new Rect(32, 72, 660, 50), _metrics);
        }
    }
}
