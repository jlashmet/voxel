using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeOrganicLayoutTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PlannerInfersNonCardinalConnectedRoutesAndVoxelizesThemThroughSharedNetwork()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);

            Assert.AreEqual(17, plan.Plots.Count, "Stable Kentridge gameplay roles must be preserved.");
            Assert.AreEqual(0, plan.Streets.Count,
                "Kentridge must not expose authored cross-streets as semantic layout truth.");
            Assert.AreEqual(16, plan.Routes.Count,
                "Every named building should contribute one inferred route; the market well stays in the plaza.");

            int routeAccesses = 0;
            int diagonalAccesses = 0;
            int diagonalSegments = 0;
            bool[] roles = new bool[17];

            for (int p = 0; p < plan.Plots.Count; p++)
            {
                BuildingPlot plot = plan.Plots[p];
                Assert.That(plot.RoleId, Is.InRange(0, roles.Length - 1));
                Assert.IsFalse(roles[plot.RoleId], "Stable role appeared twice: " + plot.RoleId);
                roles[plot.RoleId] = true;

                if (plot.RoleId == (int)KentridgeRole.Well)
                {
                    Assert.AreEqual(SiteAccessKind.Plaza, plot.Access.Kind);
                    continue;
                }

                Assert.AreEqual(SiteAccessKind.Route, plot.Access.Kind,
                    "Named structures should bind to inferred public circulation.");
                Assert.IsTrue(plot.Access.IsSpecified);
                routeAccesses++;
                if (plot.AccessDirection.X != 0 && plot.AccessDirection.Z != 0) diagonalAccesses++;

                PlannedRoute route = FindRoute(plan, plot.Access.TargetId);
                Assert.AreEqual(plot.Access.NetworkPointDm.X, route.Points[0].X);
                Assert.AreEqual(plot.Access.NetworkPointDm.Y, route.Points[0].Y);
            }

            Assert.AreEqual(16, routeAccesses);
            Assert.GreaterOrEqual(diagonalAccesses, 4,
                "Organic public approaches should not collapse back to four cardinal vectors.");

            var connectedTerminals = new System.Collections.Generic.List<Int2> { plan.Plaza.CentreDm };
            for (int r = 0; r < plan.Routes.Count; r++)
            {
                PlannedRoute route = plan.Routes[r];
                Assert.AreEqual(3, route.Points.Count,
                    "Bounded route inference should emit one bend between each site and the connected network.");
                Assert.That(route.WidthDm, Is.InRange(18, 28));
                Assert.IsTrue(Contains(connectedTerminals, route.Points[route.Points.Count - 1]),
                    route.Id + " does not join the already-connected public network.");
                connectedTerminals.Add(route.Points[0]);

                for (int i = 0; i + 1 < route.Points.Count; i++)
                {
                    int dx = route.Points[i + 1].X - route.Points[i].X;
                    int dz = route.Points[i + 1].Y - route.Points[i].Y;
                    Assert.IsFalse(dx == 0 && dz == 0, route.Id + " contains a zero-length segment.");
                    if (dx != 0 && dz != 0) diagonalSegments++;
                }
            }
            Assert.GreaterOrEqual(diagonalSegments, 12,
                "Most inferred circulation should use non-axis-aligned segments.");

            AssertNoPlotOverlap(plan);
            AssertSeedChangesLayout(plan);
            AssertGameplayUsesSemanticPublicApproach(plan);
            AssertVoxelRealizationUsesSharedRoutes(plan);
        }

        private static void AssertVoxelRealizationUsesSharedRoutes(SettlementPlan plan)
        {
            VoxelWorldGenSettings settings = BuildSettings();
            WorldRoadNetwork network = KentridgeWorldRoadNetwork.Build(plan, Seed, settings);
            FeatureCatalogue routes = KentridgeDirectedTownSurfaceCatalogue.Build(Seed, settings, Allocator.Temp);
            FeatureCatalogue piazza = KentridgeMarketPiazzaCatalogue.Build(Seed, settings, Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(Seed, settings, Allocator.Temp);
            try
            {
                Assert.AreEqual(plan.Routes.Count, network.Routes.Count,
                    "Every semantic Kentridge route must remain queryable after physical resolution.");
                Assert.AreEqual(plan.Routes.Count, routes.Definitions.Length,
                    "Shared lowering should retain one traceable definition per semantic route.");
                Assert.Greater(routes.ExplicitPlacements.Length, 64,
                    "Resolved polylines should rasterize into continuous terrain-following samples.");
                Assert.Less(routes.ExplicitPlacements.Length, 4096,
                    "Shared route rasterization must remain within a bounded catalogue cost.");

                for (int i = 0; i < routes.Definitions.Length; i++)
                    StringAssert.StartsWith("world-road-", routes.Definitions[i].Name.ToString());

                for (int p = 0; p < plan.Plots.Count; p++)
                {
                    BuildingPlot plot = plan.Plots[p];
                    if (plot.Archetype == StructureArchetype.Well) continue;

                    Int2 frontage = KentridgeVerticalProfile.FrontagePointDm(plan, plot);
                    Assert.AreEqual(
                        TerrainQuery.HeightAt(frontage.X, frontage.Y, Seed),
                        KentridgeVerticalProfile.PlotSurfaceY(plan, plot, Seed, 1),
                        "Organic plots must sit on local terrain after fixed district shelves are removed.");

                    KentridgeGameplaySiteAccess access;
                    Assert.IsTrue(KentridgeGameplaySiteAccessResolver.TryResolve(plan, plot.RoleId, 1, out access));
                    Assert.IsTrue(network.TryGetRoute(plot.Access.TargetId, out WorldRoadNetworkRoute route),
                        "Gameplay route id must resolve to a shared road object for " + (KentridgeRole)plot.RoleId + ".");
                    Assert.IsTrue(network.TrySampleClearance(
                        access.Entrance.Position.X, access.Entrance.Position.Z, out WorldRoadNetworkSample sample),
                        "Shared road clearance does not reach realized entrance for " + (KentridgeRole)plot.RoleId + ".");
                    Assert.AreEqual(route.Id, sample.Route.Id,
                        "The realized entrance must remain traceable to its semantic access route.");
                    Assert.IsTrue(RouteCatalogueCovers(routes, access.Entrance.Position),
                        "Shared road voxelization does not reach realized entrance for " + (KentridgeRole)plot.RoleId + ".");
                }

                int plazaSurface = piazza.ExplicitPlacements[0].Position.y
                                 + KentridgeMarketPiazzaCatalogue.SurfaceThicknessDm;
                Assert.AreEqual(TerrainQuery.HeightAt(plan.Plaza.CentreDm.X, plan.Plaza.CentreDm.Y, Seed), plazaSurface);
                BuildingPlot well = FindPlot(plan, (int)KentridgeRole.Well);
                Assert.AreEqual(plazaSurface, KentridgeVerticalProfile.PlotSurfaceY(plan, well, Seed, 1));

                int structures = 0;
                int roadDefinitions = 0;
                for (int i = 0; i < combined.Definitions.Length; i++)
                {
                    FeatureDefinition definition = combined.Definitions[i];
                    if (definition.Kind == FeatureKind.Structure) structures++;
                    if (definition.Name.ToString().StartsWith("world-road-", StringComparison.Ordinal)) roadDefinitions++;
                }
                Assert.AreEqual(17, structures,
                    "Shared circulation must preserve every stable Kentridge structure role.");
                Assert.AreEqual(plan.Routes.Count, roadDefinitions,
                    "The combined production catalogue must consume the shared semantic road network.");
            }
            finally
            {
                routes.Dispose();
                piazza.Dispose();
                combined.Dispose();
            }
        }

        private static PlannedRoute FindRoute(SettlementPlan plan, string id)
        {
            for (int i = 0; i < plan.Routes.Count; i++) if (plan.Routes[i].Id == id) return plan.Routes[i];
            Assert.Fail("Missing inferred route " + id);
            return null;
        }

        private static bool Contains(System.Collections.Generic.List<Int2> points, Int2 point)
        {
            for (int i = 0; i < points.Count; i++) if (points[i].X == point.X && points[i].Y == point.Y) return true;
            return false;
        }

        private static void AssertNoPlotOverlap(SettlementPlan plan)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot a = plan.Plots[i];
                Int3 fa = KentridgeDefinition.FootprintDm(a.Archetype);
                for (int j = i + 1; j < plan.Plots.Count; j++)
                {
                    BuildingPlot b = plan.Plots[j];
                    Int3 fb = KentridgeDefinition.FootprintDm(b.Archetype);
                    bool intersects = a.PositionDm.X + fa.X > b.PositionDm.X
                                   && a.PositionDm.X < b.PositionDm.X + fb.X
                                   && a.PositionDm.Y + fa.Z > b.PositionDm.Y
                                   && a.PositionDm.Y < b.PositionDm.Y + fb.Z;
                    Assert.IsFalse(intersects, ((KentridgeRole)a.RoleId) + " overlaps " + ((KentridgeRole)b.RoleId));
                }
            }
        }

        private static void AssertSeedChangesLayout(SettlementPlan baseline)
        {
            SettlementPlan alternate = KentridgeDefinition.Build(Seed ^ 0x5A17u);
            int changed = 0;
            for (int role = 0; role < 16; role++)
            {
                BuildingPlot a = FindPlot(baseline, role);
                BuildingPlot b = FindPlot(alternate, role);
                if (a.PositionDm.X != b.PositionDm.X || a.PositionDm.Y != b.PositionDm.Y) changed++;
            }
            Assert.GreaterOrEqual(changed, 12,
                "Seed should materially vary organic site placement without changing role identity.");
        }

        private static void AssertGameplayUsesSemanticPublicApproach(SettlementPlan plan)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.AccessDirection.X == 0 || plot.AccessDirection.Z == 0) continue;
                Assert.IsTrue(KentridgeGameplaySiteAccessResolver.TryResolve(plan, plot.RoleId, 1, out KentridgeGameplaySiteAccess access));
                Assert.AreEqual(plot.AccessDirection.X, access.PublicApproachInward.X);
                Assert.AreEqual(plot.AccessDirection.Z, access.PublicApproachInward.Y);
                Assert.IsTrue(access.Inward.X == 0 || access.Inward.Y == 0,
                    "The interior approach must remain normal to the current quarter-turn doorway.");
                return;
            }
            Assert.Fail("Expected at least one diagonal semantic public approach.");
        }

        private static bool RouteCatalogueCovers(FeatureCatalogue routes, Int3 point)
        {
            for (int ruleIndex = 0; ruleIndex < routes.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = routes.Rules[ruleIndex];
                FeatureDefinition definition = routes.Definitions[rule.DefinitionId];
                for (int i = 0; i < rule.ExplicitCount; i++)
                {
                    ExplicitPlacement placement = routes.ExplicitPlacements[rule.ExplicitOffset + i];
                    if (point.X >= placement.Position.x
                        && point.X < placement.Position.x + definition.Footprint.x
                        && point.Z >= placement.Position.z
                        && point.Z < placement.Position.z + definition.Footprint.z)
                        return true;
                }
            }
            return false;
        }

        private static BuildingPlot FindPlot(SettlementPlan plan, int roleId)
        {
            for (int i = 0; i < plan.Plots.Count; i++) if (plan.Plots[i].RoleId == roleId) return plan.Plots[i];
            Assert.Fail("Missing role " + roleId);
            return default;
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
