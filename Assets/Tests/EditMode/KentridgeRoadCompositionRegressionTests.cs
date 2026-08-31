using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeRoadCompositionRegressionTests
    {
        private const uint KentridgePlayableSliceSeed = 1262833236u;

        [Test]
        public void OverlappingPlotLandformsMustRunBeforeRoadGrading()
        {
            SettlementPlan plan = KentridgeDefinition.Build(KentridgePlayableSliceSeed);
            VoxelWorldGenSettings settings = Settings().For(plan);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(
                plan,
                KentridgePlayableSliceSeed,
                settings);

            using FeatureCatalogue roads = WorldRoadNetworkVoxelCatalogue.Build(
                network,
                settings,
                Allocator.Temp);
            using FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                KentridgePlayableSliceSeed,
                settings,
                Allocator.Temp);

            List<Primitive> roadPrimitives = EvaluateAll(roads, KentridgePlayableSliceSeed);
            List<Primitive> plotPrimitives = EvaluateAll(plots, KentridgePlayableSliceSeed);

            Assert.IsTrue(
                TryFindGradeOnlyPlotOverlap(roadPrimitives, plotPrimitives, out int3 overlap),
                "The production Kentridge fixture must contain a real plot landform overlapping the road grading-only envelope; otherwise this regression would not reproduce the built-player trench interaction.");

            using FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                KentridgePlayableSliceSeed,
                settings,
                Allocator.Temp);
            int firstRoad = FirstDefinitionWithPrefix(combined, "world-road-");
            int lastPlot = LastDefinitionWithPrefix(combined, "kentridge-plot-");

            Assert.That(firstRoad, Is.GreaterThanOrEqualTo(0), "Combined Kentridge catalogue must contain road landforms.");
            Assert.That(lastPlot, Is.GreaterThanOrEqualTo(0), "Combined Kentridge catalogue must contain plot-surface landforms.");
            Assert.That(
                firstRoad,
                Is.GreaterThan(lastPlot),
                "A later plot-surface carve/fill can overwrite a road grading-only point at " + overlap
                + ". When these landforms overlap, Kentridge composition must finish plot pads before the authoritative road grading pass.");
        }

        private static List<Primitive> EvaluateAll(in FeatureCatalogue catalogue, uint seed)
        {
            var result = new List<Primitive>();
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);
            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    for (int i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset + i];
                        primitives.Clear();
                        anchors.Clear();
                        ParameterSet parameters = default;
                        EvaluationResult evaluation = ShapeProgram.Evaluate(
                            in catalogue,
                            rule.DefinitionId,
                            in parameters,
                            placement.Position,
                            placement.Orientation,
                            seed,
                            (ulong)(ruleIndex + 1) * 65537ul + (uint)i,
                            primitives,
                            anchors);
                        Assert.AreEqual(EvaluationResult.Ok, evaluation);
                        for (int p = 0; p < primitives.Length; p++) result.Add(primitives[p]);
                    }
                }
            }
            finally
            {
                primitives.Dispose();
                anchors.Dispose();
            }
            return result;
        }

        private static bool TryFindGradeOnlyPlotOverlap(
            IReadOnlyList<Primitive> roads,
            IReadOnlyList<Primitive> plots,
            out int3 overlap)
        {
            for (int r = 0; r < roads.Count; r++)
            {
                Primitive road = roads[r];
                if (road.Shape != PrimitiveShape.TerrainCorridor) continue;
                road.Bounds(out int3 roadMin, out int3 roadMax);

                for (int p = 0; p < plots.Count; p++)
                {
                    Primitive plot = plots[p];
                    if (plot.Shape != PrimitiveShape.Box
                        || (plot.Mode != PrimitiveMode.Fill && plot.Mode != PrimitiveMode.Carve))
                        continue;
                    plot.Bounds(out int3 plotMin, out int3 plotMax);

                    int x0 = math.max(roadMin.x, plotMin.x);
                    int x1 = math.min(roadMax.x, plotMax.x);
                    int z0 = math.max(roadMin.z, plotMin.z);
                    int z1 = math.min(roadMax.z, plotMax.z);
                    if (x0 > x1 || z0 > z1) continue;

                    for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!TerrainCorridorRasteriser.TrySample(
                                in road, x, z, out TerrainCorridorSample sample)
                            || sample.Coverage31 == 0
                            || sample.SurfaceCoverage31 != 0)
                            continue;
                        if (sample.TargetHeightVoxels < plotMin.y - 1
                            || sample.TargetHeightVoxels > plotMax.y + 1)
                            continue;

                        overlap = new int3(x, sample.TargetHeightVoxels, z);
                        return true;
                    }
                }
            }

            overlap = default;
            return false;
        }

        private static int FirstDefinitionWithPrefix(in FeatureCatalogue catalogue, string prefix)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix)) return i;
            return -1;
        }

        private static int LastDefinitionWithPrefix(in FeatureCatalogue catalogue, string prefix)
        {
            for (int i = catalogue.Definitions.Length - 1; i >= 0; i--)
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix)) return i;
            return -1;
        }

        private static VoxelWorldGenSettings Settings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 2,
                darkMasonry: 3,
                timber: 4,
                glass: 5,
                warmWindow: 6,
                roofTile: 7,
                slate: 8,
                cloth: 9,
                moss: 10,
                water: 11,
                roadSurface: 12);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}
