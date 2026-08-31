using System.Collections.Generic;
using Game.WorldBuilder.Api;
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
            int lastPlotSurface = LastDefinitionFromStage(combined, plots);

            Assert.That(firstRoad, Is.GreaterThanOrEqualTo(0), "Combined Kentridge catalogue must contain road landforms.");
            Assert.That(lastPlotSurface, Is.GreaterThanOrEqualTo(0), "Combined Kentridge catalogue must contain plot-surface landforms.");
            Assert.That(
                firstRoad,
                Is.GreaterThan(lastPlotSurface),
                "A later plot-surface carve/fill can overwrite a road grading-only point at " + overlap
                + ". When these landforms overlap, Kentridge composition must finish plot pads before the authoritative road grading pass.");
        }

        [Test]
        public void OrganicPublicApproachMustNotCrossRaisedPlotLip()
        {
            SettlementPlan plan = KentridgeDefinition.Build(KentridgePlayableSliceSeed);
            VoxelWorldGenSettings settings = Settings().For(plan);
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(
                plan,
                KentridgePlayableSliceSeed,
                settings);
            using FeatureCatalogue plots = KentridgePlotSurfaceCatalogue.Build(
                KentridgePlayableSliceSeed,
                settings,
                Allocator.Temp);

            int worstRiseDm = int.MinValue;
            int worstRole = -1;
            int3 worstPoint = default;
            bool sampled = false;

            for (int plotIndex = 0; plotIndex < plan.Plots.Count; plotIndex++)
            {
                BuildingPlot plot = plan.Plots[plotIndex];
                if (plot.Archetype == StructureArchetype.Well) continue;
                Assert.IsTrue(
                    KentridgeGameplaySiteAccessResolver.TryResolve(plan, plot.RoleId, 1, out KentridgeGameplaySiteAccess access),
                    "Every routed Kentridge building must expose its realized public entrance.");
                Assert.IsTrue(
                    network.TryGetRoute(plot.Access.TargetId, out WorldRoadNetworkRoute route),
                    "Every routed Kentridge plot must resolve to the same semantic road used by its gameplay access.");

                List<Primitive> pad = EvaluatePlotPlacement(plots, plot, KentridgePlayableSliceSeed);
                var influence = new WorldRoadInfluence(route.Road, network.Junctions, route.ShoulderWidthDm);
                int3 entrance = new int3(
                    access.Entrance.Position.X,
                    access.Entrance.Position.Y,
                    access.Entrance.Position.Z);
                int3 exterior = new int3(
                    access.ExteriorApproach.Position.X,
                    access.ExteriorApproach.Position.Y,
                    access.ExteriorApproach.Position.Z);

                for (int step = 0; step <= KentridgeGameplaySiteAccessResolver.ApproachDistanceDecimetres; step++)
                {
                    int denominator = KentridgeGameplaySiteAccessResolver.ApproachDistanceDecimetres;
                    int x = entrance.x + (exterior.x - entrance.x) * step / denominator;
                    int z = entrance.z + (exterior.z - entrance.z) * step / denominator;
                    if (!influence.TrySample(x, z, out WorldRoadInfluenceSample road)) continue;
                    if (!TryLastFillTop(pad, x, z, out int plotTop)) continue;

                    sampled = true;
                    int riseDm = plotTop - road.TargetHeightDm;
                    if (riseDm <= worstRiseDm) continue;
                    worstRiseDm = riseDm;
                    worstRole = plot.RoleId;
                    worstPoint = new int3(x, plotTop, z);
                }
            }

            Assert.IsTrue(sampled, "The fixed production fixture must exercise a road approach while it still crosses its authored plot surface.");
            Assert.That(
                worstRiseDm,
                Is.LessThanOrEqualTo(3),
                "A public road approach crosses a plot feather " + worstRiseDm + " dm above its road target for role " + worstRole
                + " at " + worstPoint + ". A frontage pad must meet the authoritative road within the shared bounded cross-section instead of forcing the later road pass to carve a visible trench through a raised parcel lip.");
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

        private static List<Primitive> EvaluatePlotPlacement(
            in FeatureCatalogue catalogue,
            BuildingPlot plot,
            uint seed)
        {
            int definitionId = (int)plot.Archetype;
            PlacementRule rule = catalogue.Rules[definitionId];
            ExplicitPlacement selected = default;
            bool found = false;
            for (int i = 0; i < rule.ExplicitCount; i++)
            {
                ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset + i];
                if (placement.Position.x != plot.PositionDm.X
                    || placement.Position.z != plot.PositionDm.Y
                    || placement.Orientation != (byte)plot.Frontage)
                    continue;
                selected = placement;
                found = true;
                break;
            }
            Assert.IsTrue(found, "The plot-surface stage must contain every settlement plot placement.");

            var result = new List<Primitive>();
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);
            try
            {
                FeatureDefinition definition = catalogue.Definitions[definitionId];
                ParameterSet parameters = FeatureGeneration.ResolveParameters(
                    in catalogue,
                    in definition,
                    in selected,
                    definitionId,
                    selected.Position,
                    seed);
                EvaluationResult evaluation = ShapeProgram.Evaluate(
                    in catalogue,
                    definitionId,
                    in parameters,
                    selected.Position,
                    selected.Orientation,
                    seed,
                    FeatureGeneration.InstanceSeed(seed, definitionId, selected.Position),
                    primitives,
                    anchors);
                Assert.AreEqual(EvaluationResult.Ok, evaluation);
                for (int p = 0; p < primitives.Length; p++) result.Add(primitives[p]);
            }
            finally
            {
                primitives.Dispose();
                anchors.Dispose();
            }
            return result;
        }

        private static bool TryLastFillTop(
            IReadOnlyList<Primitive> primitives,
            int x,
            int z,
            out int top)
        {
            bool found = false;
            top = 0;
            for (int i = 0; i < primitives.Count; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Shape != PrimitiveShape.Box || primitive.Mode != PrimitiveMode.Fill) continue;
                primitive.Bounds(out int3 min, out int3 max);
                if (x < min.x || x > max.x || z < min.z || z > max.z) continue;
                top = max.y;
                found = true;
            }
            return found;
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

        private static int LastDefinitionFromStage(
            in FeatureCatalogue catalogue,
            in FeatureCatalogue stage)
        {
            for (int i = catalogue.Definitions.Length - 1; i >= 0; i--)
            {
                string combinedName = catalogue.Definitions[i].Name.ToString();
                for (int stageIndex = 0; stageIndex < stage.Definitions.Length; stageIndex++)
                    if (combinedName == stage.Definitions[stageIndex].Name.ToString()) return i;
            }
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
