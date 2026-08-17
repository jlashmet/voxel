using System;
using Unity.Collections;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    internal static partial class KentridgeCombinedVoxelCatalogueCanonical
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue[] stages =
            {
                KentridgeGroundCoverCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeDistrictTerraceCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeTerraceSurfaceCorrectionCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeDirectedTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeProcessionalClimbCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeUrbanCirculationCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalConnectorCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeTerraceSupportCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(seed, settings, Allocator.Temp),
                KentridgeUrbanSidewalkCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeFrontagePathCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeMarketPiazzaCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeCivicForecourtCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeStreetDressingCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildPlotDressing(seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildTownDressing(seed, settings, Allocator.Temp),
                KentridgeUrbanCourtCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalFrontageCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeFrontageAlignedUrbanFabricCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalGalleryCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeUpperSkybridgeCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeAnchorUndercroftCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeUrbanAccessCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeHillsideArchitectureCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeSemanticGrammarVoxelCatalogue.Build(seed, settings, Allocator.Temp),
            };

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
    }
}
