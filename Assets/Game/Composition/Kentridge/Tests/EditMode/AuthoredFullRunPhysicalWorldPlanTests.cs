using System;
using Game.Composition.Campaign.Content;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using MountingForce.WorldGen;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Tests
{
    public sealed class AuthoredFullRunPhysicalWorldPlanTests
    {
        [Test]
        public void FullRunPlanConsumesCompiledHierarchyAndRecoveredPhysicalMacroWorld()
        {
            AuthoredFullRunCampaignContent content = AuthoredFullRunCampaignContent.Build(
                new CutsceneDefinition(
                    "system26-full-run-destination",
                    CutsceneStageSetupDefinition.Empty,
                    Array.Empty<CutsceneStep>()));

            AuthoredFullRunPhysicalWorldPlan plan = AuthoredFullRunPhysicalWorldPlanner.Plan(
                content.Blueprint,
                0x4B454E54u,
                new Int2(0, 0),
                voxelsPerDecimetre: 1);

            Assert.That(plan.Graph.HierarchyPlan.Settlements.Count, Is.GreaterThan(1),
                "The full-run adapter must consume the compiled multi-settlement hierarchy, not the opening-only graph.");
            for (var i = 0; i < plan.Graph.HierarchyPlan.Settlements.Count; i++)
            {
                Assert.That(
                    plan.TryGetPhysicalSettlement(
                        plan.Graph.HierarchyPlan.Settlements[i].Settlement,
                        out _),
                    Is.True,
                    "Every authored settlement must have a source-backed physical settlement; fake or dropped regions are forbidden.");
            }

            Assert.That(plan.Physical.Routes.Count, Is.GreaterThan(0));
            Assert.That(plan.Physical.RouteTileCount, Is.GreaterThan(0));
            Assert.That(plan.Physical.TryGetSettlement("moordell", out _), Is.True);
            Assert.That(plan.Physical.TryGetSettlement("rossdam", out _), Is.True);
            Assert.That(plan.Physical.TryGetRoute("south-fighting-area-1", "overworld-logan-castle", out _), Is.True,
                "The production physical plan must retain the real source-backed Logan approach route.");
        }
    }
}
