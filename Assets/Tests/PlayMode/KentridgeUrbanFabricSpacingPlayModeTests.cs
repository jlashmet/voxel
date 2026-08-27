using System.Collections.Generic;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeUrbanFabricSpacingPlayModeTests
    {
        private const uint Seed = 0x4B454E54u;
        private const int MinimumClearanceDm = 20;

        [Test]
        public void ProductionAnonymousFrontagesLeavePedestrianClearanceBetweenHouses()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue catalogue = KentridgeUrbanFabricCatalogue.Build(
                Seed, settings, Allocator.Temp);

            try
            {
                Assert.Greater(catalogue.Definitions.Length, 0,
                    "Production Kentridge anonymous fabric must remain populated.");

                var frontageLines = new Dictionary<long, FrontageLine>();
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    Assert.AreEqual(1, rule.ExplicitCount,
                        "Each anonymous fabric definition should keep one production explicit placement.");

                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset];
                    int orientation = placement.Orientation & 3;
                    bool horizontalFrontage = (orientation & 1) == 0;
                    bool quarterTurn = (orientation & 1) != 0;
                    int worldWidth = quarterTurn ? definition.Footprint.z : definition.Footprint.x;
                    int worldDepth = quarterTurn ? definition.Footprint.x : definition.Footprint.z;
                    int alongStart = horizontalFrontage ? placement.Position.x : placement.Position.z;
                    int alongSize = horizontalFrontage ? worldWidth : worldDepth;
                    int crossAxis = horizontalFrontage ? placement.Position.z : placement.Position.x;
                    long lineKey = ((long)orientation << 32) | (uint)crossAxis;

                    if (!frontageLines.TryGetValue(lineKey, out FrontageLine line))
                    {
                        line = new FrontageLine(orientation, crossAxis);
                        frontageLines.Add(lineKey, line);
                    }

                    line.Intervals.Add(new Interval(alongStart, alongStart + alongSize));
                }

                int minimumClearance = MinimumClearanceDm * settings.VoxelsPerDecimetre;
                int comparedFrontages = 0;
                int comparedNeighbours = 0;
                foreach (FrontageLine line in frontageLines.Values)
                {
                    if (line.Intervals.Count < 2)
                        continue;

                    comparedFrontages++;
                    line.Intervals.Sort((left, right) => left.Start.CompareTo(right.Start));
                    for (int index = 1; index < line.Intervals.Count; index++)
                    {
                        Interval previous = line.Intervals[index - 1];
                        Interval current = line.Intervals[index];
                        int clearance = current.Start - previous.End;
                        comparedNeighbours++;
                        Assert.GreaterOrEqual(clearance, minimumClearance,
                            $"Anonymous frontage orientation {line.Orientation} at cross-axis " +
                            $"{line.CrossAxis} leaves only {clearance} voxels between adjacent " +
                            $"production envelopes; expected at least {minimumClearance} voxels " +
                            $"({MinimumClearanceDm} dm at scale 1)." );
                    }
                }

                Assert.Greater(comparedFrontages, 0,
                    "Regression must exercise at least one production frontage with multiple houses.");
                Assert.Greater(comparedNeighbours, 0,
                    "Regression must exercise at least one pair of neighbouring production houses.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private sealed class FrontageLine
        {
            public readonly int Orientation;
            public readonly int CrossAxis;
            public readonly List<Interval> Intervals = new List<Interval>();

            public FrontageLine(int orientation, int crossAxis)
            {
                Orientation = orientation;
                CrossAxis = crossAxis;
            }
        }

        private readonly struct Interval
        {
            public readonly int Start;
            public readonly int End;

            public Interval(int start, int end)
            {
                Start = start;
                End = end;
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
