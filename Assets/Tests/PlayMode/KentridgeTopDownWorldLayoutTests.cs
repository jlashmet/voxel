using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeTopDownWorldLayoutTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void SourceBackedWorldLayoutIsDeterministicConnectedAndRejectsDisconnectedTopology()
        {
            TopDownWorldLayoutSpec spec = KentridgeTopDownWorldLayout.BuildSpec();

            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(spec, Seed, out TopDownWorldLayout first, out string firstError),
                Is.True,
                firstError);
            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(spec, Seed, out TopDownWorldLayout second, out string secondError),
                Is.True,
                secondError);

            Assert.That(first.Nodes.Count, Is.EqualTo(18));
            Assert.That(first.Routes.Count, Is.EqualTo(17));
            Assert.That(first.RootId, Is.EqualTo(KentridgeTopDownWorldLayout.Kentridge));

            var occupied = new HashSet<TopDownWorldGridPoint>();
            for (var i = 0; i < first.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement node = first.Nodes[i];
                Assert.That(second.TryGetPosition(node.Node.Id, out TopDownWorldGridPoint replay), Is.True);
                Assert.That(replay, Is.EqualTo(node.Position),
                    "Identical source graph + seed must replay to identical top-down positions.");
                Assert.That(occupied.Add(node.Position), Is.True,
                    $"Macro destinations must not overlap: {node.Node.Id} at {node.Position}.");
            }

            AssertRoute(spec, KentridgeTopDownWorldLayout.Kentridge, KentridgeTopDownWorldLayout.Overworld);
            AssertRoute(spec, KentridgeTopDownWorldLayout.Kentridge, KentridgeTopDownWorldLayout.Mountains);
            AssertRoute(spec, KentridgeTopDownWorldLayout.Overworld, KentridgeTopDownWorldLayout.Forest);
            AssertRoute(spec, KentridgeTopDownWorldLayout.Overworld, KentridgeTopDownWorldLayout.Graveyard);
            AssertRoute(spec, KentridgeTopDownWorldLayout.FightingArea2, KentridgeTopDownWorldLayout.Hightown);
            AssertRoute(spec, KentridgeTopDownWorldLayout.MoordellCorridor, KentridgeTopDownWorldLayout.Moordell);
            AssertRoute(spec, KentridgeTopDownWorldLayout.RossdamRegion, KentridgeTopDownWorldLayout.Rossdam);
            AssertRoute(spec, KentridgeTopDownWorldLayout.SouthFightingArea, KentridgeTopDownWorldLayout.FairyVillage);
            AssertRoute(spec, KentridgeTopDownWorldLayout.SouthFightingArea, KentridgeTopDownWorldLayout.OrcVillage);
            AssertRoute(spec, KentridgeTopDownWorldLayout.LoganApproach, KentridgeTopDownWorldLayout.LoganCastle);

            var reachable = new HashSet<string>(StringComparer.Ordinal)
            {
                KentridgeTopDownWorldLayout.Kentridge
            };
            bool changed;
            do
            {
                changed = false;
                for (var i = 0; i < spec.Routes.Count; i++)
                {
                    TopDownWorldRouteSpec route = spec.Routes[i];
                    if (reachable.Contains(route.FromId) && reachable.Add(route.ToId))
                        changed = true;
                }
            } while (changed);

            Assert.That(reachable.Count, Is.EqualTo(spec.Nodes.Count),
                "Every macro destination must remain reachable from Kentridge through source-backed traversal.");

            var brokenNodes = new List<TopDownWorldNodeSpec>(spec.Nodes)
            {
                new TopDownWorldNodeSpec("orphaned-destination", "Orphaned Destination", TopDownWorldNodeKind.Settlement)
            };
            var broken = new TopDownWorldLayoutSpec(spec.RootId, brokenNodes, spec.Routes);
            Assert.That(
                TopDownWorldLayoutPlanner.TryPlan(broken, Seed, out _, out string brokenError),
                Is.False,
                "A disconnected world must be rejected rather than rendered as a plausible-looking map.");
            StringAssert.Contains("unreachable", brokenError.ToLowerInvariant());
        }

        private static void AssertRoute(TopDownWorldLayoutSpec spec, string from, string to)
        {
            for (var i = 0; i < spec.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = spec.Routes[i];
                if (!string.Equals(route.FromId, from, StringComparison.Ordinal)
                    || !string.Equals(route.ToId, to, StringComparison.Ordinal))
                    continue;

                StringAssert.Contains("world-procgen-clusters.yaml", route.Evidence);
                return;
            }

            Assert.Fail($"Missing verified legacy traversal relationship {from}->{to}.");
        }
    }
}
