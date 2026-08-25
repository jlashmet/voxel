using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeScene220516SpatialDiagnosticsTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PrintCombinedPlacementsIntersectingCapturedLowerTownCorridor()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildSettings(), Allocator.Temp);
            try
            {
                const int queryMinX = 560;
                const int queryMaxX = 1540;
                const int queryMinZ = 820;
                const int queryMaxZ = 1020;
                int hits = 0;

                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[
                            rule.ExplicitOffset + i];
                        bool quarterTurn = (placement.Orientation & 1) != 0;
                        int width = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
                        int depth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
                        int minX = placement.Position.x;
                        int minZ = placement.Position.z;
                        int maxX = minX + width;
                        int maxZ = minZ + depth;
                        bool intersects = maxX > queryMinX && minX < queryMaxX
                                       && maxZ > queryMinZ && minZ < queryMaxZ;
                        if (!intersects) continue;

                        hits++;
                        Debug.Log(
                            "SCENE220516_PLACEMENT "
                            + definition.Name + " kind=" + definition.Kind
                            + " precedence=" + definition.Precedence
                            + " orientation=" + placement.Orientation
                            + " xz=" + minX + "," + minZ + ".." + maxX + "," + maxZ
                            + " y=" + placement.Position.y
                            + " footprint=" + definition.Footprint);
                    }
                }

                Debug.Log("SCENE220516_PLACEMENT_COUNT " + hits);
                Assert.Greater(hits, 0, "Captured lower-town corridor unexpectedly has no explicit placements.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
