using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SiteRolePlanningTests
    {
        [Test]
        public void CompilerCarriesExactlyOneArchetypeAndConstraintMatchedRoles()
        {
            var game = Campaign.Create("site-role-plan");
            RegionRef region = game.World.RequireRegion("region", value => value
                .Biome(BiomeFamily.TemperateForest));

            SiteRef pub = game.World.RequireSite("pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior));
            game.World.Place(pub).In(region);

            SiteRef destination = game.World.RequireSite("destination", site => site
                .DifferentSiteFrom(pub)
                .ReachableFrom(pub, TraversalProfile.NormalParty));
            game.World.Place(destination).In(region);
            game.World.RequireNpc("guide", npc => npc
                .PlaceAt(destination)
                .RequireConversation());

            CampaignBlueprint blueprint = game.Build();
            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);

            SiteRolePlan pubRole = graph.SiteRoles.Single(role => role.Role.Equals(pub));
            Assert.That(pubRole.RequiredCardinality, Is.EqualTo(1));
            Assert.That(pubRole.ResolutionMode, Is.EqualTo(SiteResolutionMode.RequiredArchetype));
            Assert.That(pubRole.Archetype, Is.EqualTo(SiteArchetype.Pub));

            SiteRolePlan destinationRole = graph.SiteRoles.Single(role => role.Role.Equals(destination));
            Assert.That(destinationRole.RequiredCardinality, Is.EqualTo(1));
            Assert.That(destinationRole.ResolutionMode, Is.EqualTo(SiteResolutionMode.ConstraintMatch));
            Assert.That(destinationRole.Archetype, Is.EqualTo(SiteArchetype.Unspecified));
            Assert.That(destinationRole.Capabilities.Any(capability =>
                capability.Kind == SiteCapabilityKind.ConversationSpace
                && capability.Source == SiteCapabilitySource.Derived), Is.True);

            Assert.That(graph.Hierarchy.Regions.Single().Ref, Is.EqualTo(region));
            Assert.That(graph.SpatialConstraints.Any(constraint =>
                constraint.Kind == SpatialConstraintKind.DifferentSite
                && constraint.Subject.Equals(destination)
                && constraint.Target.Equals(pub)), Is.True);
            Assert.That(graph.SpatialConstraints.Any(constraint =>
                constraint.Kind == SpatialConstraintKind.ReachableFrom
                && constraint.Subject.Equals(destination)
                && constraint.Target.Equals(pub)), Is.True);
        }
    }
}
