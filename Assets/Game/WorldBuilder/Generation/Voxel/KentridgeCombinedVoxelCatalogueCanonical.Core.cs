using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Unity.Collections;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    internal static partial class KentridgeCombinedVoxelCatalogueCanonical
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan settlement = SettlementVoxelPlan.Resolve(seed, in settings);
            bool isKentridge = settlement.Theme.Id == Content.Kentridge.KentridgeDefinition.Id;
            bool organicKentridge = isKentridge && settlement.Routes.Count > 0;
            WorldRoadNetwork organicRoadNetwork = organicKentridge
                ? KentridgeWorldRoadNetwork.Build(settlement, seed, settings)
                : null;
            var stageList = new List<FeatureCatalogue>(organicKentridge ? 8 : (isKentridge ? 25 : 9));

            Add(stageList, KentridgeGroundCoverCatalogue.Build(seed, settings, Allocator.Temp));

            if (isKentridge && !organicKentridge)
            {
                Add(stageList, KentridgeDistrictTerraceCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeTerraceSurfaceCorrectionCatalogue.Build(seed, settings, Allocator.Temp));
            }

            // Legacy authored streets retain their established public-space ordering. Organic Kentridge
            // reuses one solved road network for physical rasterization, but defers that write until
            // after plot landforms so road grading remains authoritative wherever the wider grading
            // envelope overlaps a plot pad.
            if (!organicKentridge)
                Add(stageList, KentridgeDirectedTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp));

            if (isKentridge && !organicKentridge)
            {
                Add(stageList, KentridgeProcessionalClimbCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeUrbanCirculationCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeVerticalConnectorCatalogue.Build(seed, settings, Allocator.Temp));
            }

            // Plot-driven stages remain valid for both layouts because they derive from SettlementPlan.Plots.
            Add(stageList, KentridgeTerraceSupportCatalogue.Build(seed, settings, Allocator.Temp));
            Add(stageList, KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(seed, settings, Allocator.Temp));

            if (organicKentridge)
                Add(stageList, WorldRoadNetworkVoxelCatalogue.Build(
                    organicRoadNetwork, settings, Allocator.Temp, precedence: 20));

            if (isKentridge && !organicKentridge)
                Add(stageList, KentridgeUrbanSidewalkCatalogue.Build(seed, settings, Allocator.Temp));
            if (!organicKentridge)
                Add(stageList, KentridgeFrontagePathCatalogue.Build(seed, settings, Allocator.Temp));

            Add(stageList, KentridgeMarketPiazzaCatalogue.Build(seed, settings, Allocator.Temp));

            if (isKentridge && !organicKentridge)
            {
                Add(stageList, KentridgeCivicForecourtCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeStreetDressingCatalogue.Build(seed, settings, Allocator.Temp));
            }

            Add(stageList, KentridgeVerticalPlacementAdapter.BuildPlotDressing(seed, settings, Allocator.Temp));
            if (organicKentridge)
                Add(stageList, KentridgeTownDressingCatalogue.Build(seed, settings, Allocator.Temp));
            else
                Add(stageList, KentridgeVerticalPlacementAdapter.BuildTownDressing(seed, settings, Allocator.Temp));

            // These anonymous massing/access passes encode the retired cross-street/block skeleton.
            // Organic Kentridge deliberately omits them until they can consume inferred route topology.
            if (isKentridge && !organicKentridge)
            {
                AddReserved(stageList, KentridgeUrbanCourtCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeVerticalFrontageCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeFrontageAlignedUrbanFabricCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeVerticalGalleryCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeUpperSkybridgeCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeUrbanAccessCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
                AddReserved(stageList, KentridgeHillsideArchitectureCatalogue.Build(seed, settings, Allocator.Temp), settlement, settings);
            }

            Add(stageList, KentridgeSharedStructureVoxelCatalogue.Build(seed, settings, Allocator.Temp));

            if (isKentridge && !organicKentridge)
                Add(stageList, KentridgeAnchorUndercroftCatalogue.Build(seed, settings, Allocator.Temp));

            FeatureCatalogue[] stages = stageList.ToArray();

            try
            {
                int definitions = 0, rules = 0, parameters = 0, anchors = 0, slots = 0;
                int programs = 0, materials = 0, placements = 0, overrides = 0;
                for (int i = 0; i < stages.Length; i++)
                {
                    FeatureCatalogue stage = stages[i];
                    definitions += stage.Definitions.Length;
                    rules += stage.Rules.Length;
                    parameters += stage.Parameters.Length;
                    anchors += stage.Anchors.Length;
                    slots += stage.Slots.Length;
                    programs += stage.Program.Length;
                    materials += stage.Materials.Length;
                    placements += stage.ExplicitPlacements.Length;
                    overrides += stage.ParameterOverrides.Length;
                }

                FeatureCatalogue result = FeatureCatalogueBuilder.Allocate(
                    definitions, rules, parameters, anchors, slots, programs,
                    materials, placements, overrides, allocator);
                int d = 0, r = 0, p = 0, a = 0, s = 0;
                int code = 0, m = 0, e = 0, o = 0;
                for (int i = 0; i < stages.Length; i++)
                    Append(in stages[i], ref result,
                        ref d, ref r, ref p, ref a, ref s,
                        ref code, ref m, ref e, ref o);

                CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref result);
                if (load != CatalogueLoadResult.Ok)
                {
                    result.Dispose();
                    throw new InvalidOperationException(
                        "Combined Kentridge catalogue failed validation: " + load);
                }
                return result;
            }
            finally
            {
                for (int i = 0; i < stages.Length; i++)
                    if (stages[i].IsCreated) stages[i].Dispose();
            }
        }

        private static void Add(List<FeatureCatalogue> stages, FeatureCatalogue stage) =>
            stages.Add(stage);

        private static void AddReserved(
            List<FeatureCatalogue> stages,
            FeatureCatalogue stage,
            SettlementPlan settlement,
            VoxelWorldGenSettings settings) =>
            stages.Add(KentridgeNamedPlotReservationCatalogue.Apply(
                stage, settlement, settings));
    }
}
