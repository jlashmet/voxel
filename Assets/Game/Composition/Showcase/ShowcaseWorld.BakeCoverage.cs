using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Returns the region layers required by explicit fixed-altitude structures inside the
        /// startup disc. Terrain surface residency cannot discover structure-owned vertical layers:
        /// a tall structure may begin on the surface layer and extend into otherwise empty sky.
        ///
        /// Restrict this to fixed-altitude Structure definitions. Landforms deliberately stay on
        /// terrain-surface residency so mountain/headroom bounds do not make unrelated sky resident,
        /// while non-fixed structures need terrain adaptation before their final Y is known.
        /// </summary>
        public static List<int3> PlanExplicitFixedStructureBakeRegions(
            in FeatureCatalogue catalogue,
            int3 startupCentre,
            int startupRadiusRegions)
        {
            var required = new HashSet<int3>();
            if (!catalogue.IsCreated) return new List<int3>();

            int radius = math.max(0, startupRadiusRegions);
            int radiusSquared = radius * radius;

            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.DefinitionCount)
                    continue;

                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                if (definition.Kind != FeatureKind.Structure
                    || definition.BasePlane != BasePlaneRule.FixedAltitude)
                    continue;

                for (int explicitIndex = 0; explicitIndex < rule.ExplicitCount; explicitIndex++)
                {
                    int placementIndex = rule.ExplicitOffset + explicitIndex;
                    if ((uint)placementIndex >= (uint)catalogue.ExplicitPlacements.Length)
                        continue;

                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];

                    // Match FeatureRegionBuild's declared-footprint overlap contract exactly. The
                    // footprint is half-open [Position, Position + Footprint), so subtract one when
                    // converting the inclusive maximum voxel to a region coordinate. Keep the math
                    // component-wise here so this helper does not depend on vector operator support.
                    int3 minVoxel = placement.Position;
                    int3 maxVoxel = new int3(
                        placement.Position.x + definition.Footprint.x - 1,
                        placement.Position.y + definition.Footprint.y - 1,
                        placement.Position.z + definition.Footprint.z - 1);
                    int3 minRegion = new int3(
                        FloorDivRegion(minVoxel.x),
                        FloorDivRegion(minVoxel.y),
                        FloorDivRegion(minVoxel.z));
                    int3 maxRegion = new int3(
                        FloorDivRegion(maxVoxel.x),
                        FloorDivRegion(maxVoxel.y),
                        FloorDivRegion(maxVoxel.z));

                    for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
                    for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
                    {
                        int dx = rx - startupCentre.x;
                        int dz = rz - startupCentre.z;
                        if (dx * dx + dz * dz > radiusSquared) continue;

                        for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
                            required.Add(new int3(rx, ry, rz));
                    }
                }
            }

            var regions = new List<int3>(required);
            regions.Sort(CompareRegionCoords);
            return regions;
        }

        private void MaterialiseExplicitFixedStructureBakeRegions(int3 centre, int radius)
        {
            List<int3> required = PlanExplicitFixedStructureBakeRegions(
                in _catalogue, centre, radius);
            for (int i = 0; i < required.Count; i++)
                GenerateRegionBlocking(required[i]);
        }

        private static int FloorDivRegion(int voxel)
        {
            int quotient = voxel / RegionVoxelEdge;
            int remainder = voxel % RegionVoxelEdge;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
