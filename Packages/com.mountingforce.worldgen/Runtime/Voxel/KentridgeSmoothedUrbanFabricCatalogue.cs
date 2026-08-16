using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Transitional lowering adapter for Kentridge's anonymous frontage.
    ///
    /// UrbanFabricCompiler owns the deterministic architectural form and the selected architecture
    /// style owns its renderer-neutral geometry profile. The existing fabric catalogue still emits
    /// legacy box bytecode, so ArchitectureGeometryCatalogue lowers those profiles until the fabric
    /// program itself is migrated to ArchitectureShapeProgramBuilder. Keeping this adapter separate
    /// makes that remaining migration explicit instead of baking Kentridge smoothing into shared code.
    /// </summary>
    internal static class KentridgeSmoothedUrbanFabricCatalogue
    {
        private const int ModulePitchDm = 80;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            FeatureCatalogue source = KentridgeFrontageAlignedUrbanFabricCatalogue.Build(
                seed, settings, Allocator.Temp);
            try
            {
                List<StructureGeometryProfile> profiles = BuildProfiles(seed);
                if (profiles.Count != source.Definitions.Length)
                    throw new InvalidOperationException(
                        "Kentridge urban fabric geometry profile count does not match its definitions.");

                return ArchitectureGeometryCatalogue.Apply(
                    in source,
                    KentridgeDefinition.Theme,
                    settings,
                    profiles,
                    allocator);
            }
            finally
            {
                source.Dispose();
            }
        }

        private static List<StructureGeometryProfile> BuildProfiles(uint seed)
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(seed);
            var profiles = new List<StructureGeometryProfile>(48);

            for (int runIndex = 0; runIndex < plan.FrontageRuns.Count; runIndex++)
            {
                KentridgeFrontageRun run = plan.FrontageRuns[runIndex];
                int start = run.IsHorizontal
                    ? Math.Min(run.StartDm.X, run.EndDm.X)
                    : Math.Min(run.StartDm.Y, run.EndDm.Y);
                int end = run.IsHorizontal
                    ? Math.Max(run.StartDm.X, run.EndDm.X)
                    : Math.Max(run.StartDm.Y, run.EndDm.Y);
                int siteIndex = 0;

                if (!run.HasGap)
                {
                    AppendSegmentProfiles(
                        run, start, end, seed, runIndex, ref siteIndex, profiles);
                    continue;
                }

                int gapStart = Math.Max(
                    start,
                    run.GapCentreDm - run.GapWidthDm / 2);
                int gapEnd = Math.Min(end, gapStart + run.GapWidthDm);
                AppendSegmentProfiles(
                    run, start, gapStart, seed, runIndex, ref siteIndex, profiles);
                AppendSegmentProfiles(
                    run, gapEnd, end, seed, runIndex, ref siteIndex, profiles);
            }

            return profiles;
        }

        private static void AppendSegmentProfiles(
            KentridgeFrontageRun run,
            int startDm,
            int endDm,
            uint seed,
            int runIndex,
            ref int siteIndex,
            List<StructureGeometryProfile> profiles)
        {
            int lengthDm = endDm - startDm;
            if (lengthDm <= 0) return;

            // This is the same high-level site count contract used by KentridgeUrbanFabricCatalogue.
            // Geometry lowering intentionally does not need site position; it only needs to resolve the
            // same form sequence in the same deterministic order.
            int effectiveCoverage = Math.Min(94, run.CoveragePercent + 14);
            int targetOccupiedDm = lengthDm * effectiveCoverage / 100;
            int count = Math.Max(
                1,
                (targetOccupiedDm + ModulePitchDm - 1) / ModulePitchDm);
            UrbanFabricIntent intent = KentridgeDefinition.UrbanFabricIntent(run);

            for (int i = 0; i < count; i++)
            {
                UrbanFabricForm form = UrbanFabricCompiler.Resolve(
                    intent,
                    seed,
                    runIndex,
                    siteIndex,
                    BuiltInArchitectureStyles.Registry);
                profiles.Add(UrbanFabricGeometryProfiles.Resolve(
                    intent,
                    form,
                    BuiltInArchitectureStyles.Registry));
                siteIndex++;
            }
        }
    }
}
