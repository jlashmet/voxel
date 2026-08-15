using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    internal static partial class KentridgeCombinedVoxelCatalogueCanonical
    {
        public static FeatureCatalogue BuildWithHiddenSpaces(
            uint seed,
            VoxelWorldGenSettings settings,
            IReadOnlyList<SiteHiddenSpaceRequest> requests,
            Allocator allocator)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (requests.Count == 0)
                return Build(seed, settings, allocator);

            SettlementPlan plan = KentridgeDefinition.Build(seed);
            IReadOnlyList<KentridgeHiddenSpaceGeometry> geometries =
                KentridgeHiddenSpaceBatchPlanner.Resolve(plan, requests);
            return BuildWithHiddenSpaceGeometry(seed, settings, geometries, allocator);
        }

        public static FeatureCatalogue BuildWithHiddenSpaceGeometry(
            uint seed,
            VoxelWorldGenSettings settings,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> geometries,
            Allocator allocator)
        {
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (geometries.Count == 0)
                return Build(seed, settings, allocator);

            SettlementPlan plan = KentridgeDefinition.Build(seed);
            FeatureCatalogue baseCatalogue = Build(seed, settings, Allocator.Temp);
            FeatureCatalogue hiddenCatalogue = KentridgeHiddenSpaceVoxelCatalogue.Build(
                plan,
                settings,
                geometries,
                Allocator.Temp);

            try
            {
                int definitions = baseCatalogue.Definitions.Length + hiddenCatalogue.Definitions.Length;
                int rules = baseCatalogue.Rules.Length + hiddenCatalogue.Rules.Length;
                int parameters = baseCatalogue.Parameters.Length + hiddenCatalogue.Parameters.Length;
                int anchors = baseCatalogue.Anchors.Length + hiddenCatalogue.Anchors.Length;
                int slots = baseCatalogue.Slots.Length + hiddenCatalogue.Slots.Length;
                int programs = KentridgeShapeProgramCompatibility.CanonicalLength(in baseCatalogue)
                             + KentridgeShapeProgramCompatibility.CanonicalLength(in hiddenCatalogue);
                int materials = baseCatalogue.Materials.Length + hiddenCatalogue.Materials.Length;
                int placements = baseCatalogue.ExplicitPlacements.Length + hiddenCatalogue.ExplicitPlacements.Length;
                int overrides = baseCatalogue.ParameterOverrides.Length + hiddenCatalogue.ParameterOverrides.Length;

                FeatureCatalogue result = CatalogueLoader.Allocate(
                    definitions,
                    rules,
                    parameters,
                    anchors,
                    slots,
                    programs,
                    materials,
                    placements,
                    overrides,
                    allocator);

                int d = 0, r = 0, p = 0, a = 0, s = 0;
                int code = 0, m = 0, e = 0, o = 0;
                Append(in baseCatalogue, ref result,
                    ref d, ref r, ref p, ref a, ref s,
                    ref code, ref m, ref e, ref o);
                Append(in hiddenCatalogue, ref result,
                    ref d, ref r, ref p, ref a, ref s,
                    ref code, ref m, ref e, ref o);

                CatalogueLoadResult load = CatalogueLoader.Finalise(ref result);
                if (load != CatalogueLoadResult.Ok)
                {
                    result.Dispose();
                    throw new InvalidOperationException(
                        "Kentridge catalogue with hidden spaces failed validation: " + load);
                }
                return result;
            }
            finally
            {
                if (baseCatalogue.IsCreated) baseCatalogue.Dispose();
                if (hiddenCatalogue.IsCreated) hiddenCatalogue.Dispose();
            }
        }
    }
}
