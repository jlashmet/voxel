using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeOrganicLayoutTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void PlannerInfersNonCardinalConnectedRoutesAndVoxelizesThem()
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
                if (plot.AccessDirection.X != 0 && plot.AccessDirection.Z != 0)
                    diagonalAccesses++;

                PlannedRoute route = FindRoute(plan, plot.Access.TargetId);
                Assert.AreEqual(plot.Access.NetworkPointDm.X, route.Points[0].X);
                Assert.AreEqual(plot.Access.NetworkPointDm.Y, route.Points[0].Y);
            }

            Assert.AreEqual(16, routeAccesses);
            Assert.GreaterOrEqual(diagonalAccesses, 4,
                "Organic public approaches should not collapse back to four cardinal vectors.");

            var connectedTerminals = new System.Collections.Generic.List<Int2>
            {
                plan.Plaza.CentreDm
            };
            for (int r = 0; r < plan.Routes.Count; r++)
            {
                PlannedRoute route = plan.Routes[r];
                Assert.AreEqual(3, route.Points.Count,
                    "Bounded route inference should emit one bend between each site and the connected network.");
                Assert.That(route.WidthDm, Is.InRange(18, 28));

                bool connected = Contains(connectedTerminals, route.Points[route.Points.Count - 1]);
                Assert.IsTrue(connected, route.Id + " does not join the already-connected public network.");
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
            AssertVoxelRealizationUsesOrganicRoutes();
        }

        private static PlannedRoute FindRoute(SettlementPlan plan, string id)
        {
            for (int i = 0; i < plan.Routes.Count; i++)
                if (plan.Routes[i].Id == id) return plan.Routes[i];
            Assert.Fail("Missing inferred route " + id);
            return null;
        }

        private static bool Contains(System.Collections.Generic.List<Int2> points, Int2 point)
        {
            for (int i = 0; i < points.Count; i++)
                if (points[i].X == point.X && points[i].Y == point.Y) return true;
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
                    Assert.IsFalse(intersects,
                        ((KentridgeRole)a.RoleId) + " overlaps " + ((KentridgeRole)b.RoleId));
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
                if (a.PositionDm.X != b.PositionDm.X || a.PositionDm.Y != b.PositionDm.Y)
                    changed++;
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
                KentridgeGameplaySiteAccess access;
                Assert.IsTrue(KentridgeGameplaySiteAccessResolver.TryResolve(
                    plan, plot.RoleId, 1, out access));
                Assert.AreEqual(plot.AccessDirection.X, access.PublicApproachInward.X);
                Assert.AreEqual(plot.AccessDirection.Z, access.PublicApproachInward.Y);
                Assert.IsTrue(access.Inward.X == 0 || access.Inward.Y == 0,
                    "The interior approach must remain normal to the current quarter-turn doorway.");
                return;
            }
            Assert.Fail("Expected at least one diagonal semantic public approach.");
        }

        private static void AssertVoxelRealizationUsesOrganicRoutes()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue routes = KentridgeDirectedTownSurfaceCatalogue.Build(
                Seed, settings, Allocator.Temp);
            FeatureCatalogue combined = KentridgeCombinedVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            try
            {
                Assert.That(routes.Definitions.Length, Is.InRange(3, 5));
                Assert.Greater(routes.ExplicitPlacements.Length, 64,
                    "Inferred polylines should rasterize into continuous terrain-following samples.");
                Assert.Less(routes.ExplicitPlacements.Length, 2048,
                    "Organic route rasterization must remain within a small bounded catalogue cost.");
                for (int i = 0; i < routes.Definitions.Length; i++)
                    StringAssert.StartsWith("kentridge-organic-route-", routes.Definitions[i].Name.ToString());

                int structures = 0;
                int organicDefinitions = 0;
                for (int i = 0; i < combined.Definitions.Length; i++)
                {
                    FeatureDefinition definition = combined.Definitions[i];
                    if (definition.Kind == FeatureKind.Structure) structures++;
                    if (definition.Name.ToString().StartsWith("kentridge-organic-route-", StringComparison.Ordinal))
                        organicDefinitions++;
                }
                Assert.AreEqual(17, structures,
                    "Organic circulation must preserve every stable Kentridge structure role.");
                Assert.Greater(organicDefinitions, 0,
                    "The combined production catalogue must consume inferred routes.");
            }
            finally
            {
                routes.Dispose();
                combined.Dispose();
            }
        }

        private static BuildingPlot FindPlot(SettlementPlan plan, int roleId)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == roleId) return plan.Plots[i];
            Assert.Fail("Missing role " + roleId);
            return default(BuildingPlot);
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
