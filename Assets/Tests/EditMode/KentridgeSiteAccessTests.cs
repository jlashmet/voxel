using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeSiteAccessTests
    {
        [Test]
        public void EveryStableSiteDeclaresAnExplicitExistingAccessTarget()
        {
            SettlementPlan plan = KentridgeDefinition.Build(123u);
            var streetIds = new HashSet<string>();
            for (var i = 0; i < plan.Streets.Count; i++)
                streetIds.Add(plan.Streets[i].Id);

            Assert.That(plan.Sites.Count, Is.EqualTo(17));
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                Assert.That(site.Access.IsSpecified, Is.True,
                    "Stable site role " + site.RoleId + " must expose its movement-network access.");

                if (site.Access.Kind == SiteAccessKind.Street)
                {
                    Assert.That(streetIds.Contains(site.Access.TargetId), Is.True,
                        "Site role " + site.RoleId + " references missing street '" + site.Access.TargetId + "'.");
                }
                else
                {
                    Assert.That(site.Access.Kind, Is.EqualTo(SiteAccessKind.Plaza));
                    Assert.That(site.Access.TargetId, Is.EqualTo(plan.Plaza.Id));
                }
            }
        }

        [Test]
        public void PubAndWellKeepAuthoredNetworkConnections()
        {
            SettlementPlan plan = KentridgeDefinition.Build(456u);
            PlannedSite pub = FindSite(plan, KentridgeRole.Pub);
            PlannedSite well = FindSite(plan, KentridgeRole.Well);

            Assert.That(pub.Access.Kind, Is.EqualTo(SiteAccessKind.Street));
            Assert.That(pub.Access.TargetId, Is.EqualTo(KentridgeTownPlanner.MainSpineId));
            Assert.That(pub.Access.NetworkPointDm.X, Is.EqualTo(KentridgeTownPlanner.MainSpineXDm));

            Assert.That(well.Access.Kind, Is.EqualTo(SiteAccessKind.Plaza));
            Assert.That(well.Access.TargetId, Is.EqualTo(KentridgeTownPlanner.MarketSquareId));
            Assert.That(well.Access.NetworkPointDm.X, Is.EqualTo(KentridgeDefinition.TownCentreDm.X));
            Assert.That(well.Access.NetworkPointDm.Y, Is.EqualTo(KentridgeDefinition.TownCentreDm.Y));
        }

        private static PlannedSite FindSite(SettlementPlan plan, KentridgeRole role)
        {
            int roleId = (int)role;
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                if (plan.Sites[i].RoleId == roleId)
                    return plan.Sites[i];
            }

            Assert.Fail("Missing stable Kentridge site role " + role + ".");
            return default(PlannedSite);
        }
    }
}
