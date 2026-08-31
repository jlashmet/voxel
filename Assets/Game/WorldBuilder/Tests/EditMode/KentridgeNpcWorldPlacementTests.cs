using System.Collections.Generic;
using System.Linq;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeNpcWorldPlacementTests
    {
        private const uint Seed = 0x51A7u;

        [Test]
        public void ConversationNpcAssignmentsResolveDeterministicallyInsidePhysicalInterior()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            BuildingPlot pub = plan.Plots.Single(plot => plot.RoleId == (int)KentridgeRole.Pub);
            ResolvedSiteId site = SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, pub.RoleId);
            var siteRole = new SiteRef("starting-pub");
            var assignments = new[]
            {
                new NpcSiteAssignment(new NpcRef("steven"), siteRole, site, true),
                new NpcSiteAssignment(new NpcRef("logan"), siteRole, site, true),
                new NpcSiteAssignment(new NpcRef("background-patron"), siteRole, site, false),
            };
            var facts = new KentridgeVoxelSiteRealizationFacts(plan, 1);

            IReadOnlyList<ResolvedNpcWorldPlacement> forward =
                KentridgeNpcWorldPlacementResolver.Resolve(assignments, plan, facts);
            IReadOnlyList<ResolvedNpcWorldPlacement> reversed =
                KentridgeNpcWorldPlacementResolver.Resolve(assignments.Reverse().ToArray(), plan, facts);

            Assert.That(forward.Count, Is.EqualTo(3));
            Assert.That(reversed.Count, Is.EqualTo(3));

            var reversedByNpc = reversed.ToDictionary(value => value.Npc.Id);
            RealizedWorldPoint entrance;
            Assert.That(facts.TryGetPublicEntrance(pub.RoleId, out entrance), Is.True);

            StructureIntent intent = KentridgeDefinition.StructureIntent(pub);
            StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, plan.Seed);
            StructureSiteGeometry geometry;
            Assert.That(
                StructureSiteGeometryResolver.TryResolve(intent, plan.Theme, form, out geometry),
                Is.True);

            var occupied = new HashSet<string>();
            foreach (ResolvedNpcWorldPlacement placement in forward)
            {
                ResolvedNpcWorldPlacement sameNpc = reversedByNpc[placement.Npc.Id];
                Assert.That(sameNpc.Position.Position.X, Is.EqualTo(placement.Position.Position.X));
                Assert.That(sameNpc.Position.Position.Y, Is.EqualTo(placement.Position.Position.Y));
                Assert.That(sameNpc.Position.Position.Z, Is.EqualTo(placement.Position.Position.Z));
                Assert.That(placement.Position.UnitsPerDecimetre, Is.EqualTo(1));
                Assert.That(placement.Position.Position.Y, Is.EqualTo(entrance.Position.Y),
                    "Ground-floor NPC placement must use the backend's exact realized floor height.");
                Assert.That(placement.Position.Position.X,
                    Is.InRange(geometry.FootprintMinDm.X, geometry.FootprintMaxDm.X - 1));
                Assert.That(placement.Position.Position.Z,
                    Is.InRange(geometry.FootprintMinDm.Y, geometry.FootprintMaxDm.Y - 1));

                string key = placement.Position.Position.X + ":" + placement.Position.Position.Y + ":" +
                             placement.Position.Position.Z;
                Assert.That(occupied.Add(key), Is.True,
                    "Two NPCs must not resolve to the same deterministic interior slot.");
            }

            ResolvedNpcWorldPlacement first = forward[0];
            Assert.That(first.RequiresConversation, Is.True,
                "Conversation NPCs should receive the nearest deterministic interior slots first.");
            Assert.That(first.Npc.Id, Is.EqualTo("logan"),
                "Conversation slot assignment is stable by NPC id, independent of source ordering.");
        }
    }
}
