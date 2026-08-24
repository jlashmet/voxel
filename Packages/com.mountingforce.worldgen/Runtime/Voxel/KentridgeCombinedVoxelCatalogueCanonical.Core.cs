using System;
using System.Collections.Generic;
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
            var stageList = new List<FeatureCatalogue>(isKentridge ? 25 : 9);

            Add(stageList, KentridgeGroundCoverCatalogue.Build(seed, settings, Allocator.Temp));
            if (isKentridge)
            {
                Add(stageList, KentridgeDistrictTerraceCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeTerraceSurfaceCorrectionCatalogue.Build(seed, settings, Allocator.Temp));
            }
            Add(stageList, KentridgeDirectedTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp));
            if (isKentridge)
            {
                Add(stageList, KentridgeProcessionalClimbCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeUrbanCirculationCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeVerticalConnectorCatalogue.Build(seed, settings, Allocator.Temp));
            }
            Add(stageList, KentridgeTerraceSupportCatalogue.Build(seed, settings, Allocator.Temp));
            Add(stageList, KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(seed, settings, Allocator.Temp));
            if (isKentridge)
                Add(stageList, KentridgeUrbanSidewalkCatalogue.Build(seed, settings, Allocator.Temp));
            Add(stageList, KentridgeFrontagePathCatalogue.Build(seed, settings, Allocator.Temp));
            Add(stageList, KentridgeMarketPiazzaCatalogue.Build(seed, settings, Allocator.Temp));
            if (isKentridge)
            {
                Add(stageList, KentridgeCivicForecourtCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeStreetDressingCatalogue.Build(seed, settings, Allocator.Temp));
            }
            Add(stageList, KentridgeVerticalPlacementAdapter.BuildPlotDressing(seed, settings, Allocator.Temp));
            Add(stageList, KentridgeVerticalPlacementAdapter.BuildTownDressing(seed, settings, Allocator.Temp));
            if (isKentridge)
            {
                Add(stageList, KentridgeUrbanCourtCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeVerticalFrontageCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeFrontageAlignedUrbanFabricCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeVerticalGalleryCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeUpperSkybridgeCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeUrbanAccessCatalogue.Build(seed, settings, Allocator.Temp));
                Add(stageList, KentridgeHillsideArchitectureCatalogue.Build(seed, settings, Allocator.Temp));
            }
            Add(stageList, KentridgeSharedStructureVoxelCatalogue.Build(seed, settings, Allocator.Temp));

            // Undercrofts are authored around Kentridge's named pub and warehouse rather than a
            // settlement plan, so they remain part of the Kentridge-only stage sequence.
            if (isKentridge)
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
    }
}
