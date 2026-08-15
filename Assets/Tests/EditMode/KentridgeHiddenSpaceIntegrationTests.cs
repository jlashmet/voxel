using System.Linq;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeHiddenSpaceIntegrationTests
    {
        private const uint Seed = 0x51A7u;

        [Test]
        public void PubHiddenSpaceUsesOppositeSideFromGeneratedServiceWing()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            BuildingPlot pub = plan.Plots.Single(plot => plot.RoleId == (int)KentridgeRole.Pub);
            var request = new SiteHiddenSpaceRequest(
                "pub-hidden",
                (int)KentridgeRole.Pub,
                minimumCount: 1,
                targetCount: 2,
                HiddenSpaceEntranceKind.BreakableMatchingWall);

            var geometries = KentridgeHiddenSpacePlanner.Resolve(pub, Seed, request);

            Assert.That(geometries.Count, Is.EqualTo(1),
                "The generated pub has a side wing, so only the opposite side is a valid hidden cavity host.");
            KentridgeHiddenSpaceGeometry geometry = geometries[0];
            Assert.That(geometry.OnRightSide, Is.True,
                "Kentridge pub's generated side wing is on the left; hidden geometry must not overlap it.");
            Assert.That(geometry.Realization.HiddenFromNormalTraversal, Is.True);
            Assert.That(geometry.Realization.Entrance.SeparatesHiddenSpaceBeforeOpen, Is.True);
            Assert.That(geometry.Realization.Entrance.GrantsNormalTraversalAfterOpen, Is.True);
            Assert.That(geometry.Realization.Entrance.IsStructurallyCritical, Is.False);
            Assert.That(geometry.Realization.Entrance.SupportsRemoval, Is.True);
            Assert.That(geometry.Realization.Entrance.MatchesHostSurface, Is.True);

            HiddenSpaceBoundsDm room = geometry.Realization.LocalBoundsDm;
            Int3 envelope = KentridgeDefinition.FootprintDm(pub.Archetype);
            Assert.That(room.MinX, Is.GreaterThanOrEqualTo(KentridgeHiddenSpacePlanner.EnvelopeEdgeMarginDm));
            Assert.That(room.MinX + room.SizeX, Is.LessThanOrEqualTo(envelope.X - KentridgeHiddenSpacePlanner.EnvelopeEdgeMarginDm));
            Assert.That(room.MinZ, Is.GreaterThanOrEqualTo(0));
            Assert.That(room.MinZ + room.SizeZ, Is.LessThanOrEqualTo(envelope.Z));
        }

        [Test]
        public void RequiredFalseWallSecretResolvesOnlyFromPhysicalWorldGenFacts()
        {
            var game = Campaign.Create("physical-secret");
            SiteRef pub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub));
            LootTableRef loot = game.Loot.Table("secret-loot", table => table
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            SecretRef requiredRef = game.World.RequireSecret("pub-cache", secret => secret
                .Inside(pub)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .Container(ContainerArchetype.TreasureChest)
                .RewardWith(loot));

            CampaignBlueprint blueprint = game.Build();
            PlanningGraph graph = BlueprintCompiler.Compile(blueprint);
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var projections = new KentridgeArchitectureSiteProjectionProvider(plan);
            var traversal = new SettlementStreetTraversalFacts(plan, projections);
            var facts = new SettlementPlanWorldBuilderFacts(
                plan,
                new RegionRef("kentridge-region"),
                new SettlementRef("kentridge"),
                projections,
                traversal,
                projections);
            SiteResolutionResult siteResolution = SiteRoleResolver.Resolve(graph, facts);

            Assert.That(siteResolution.IsResolved, Is.True,
                siteResolution.Diagnostics.Count == 0
                    ? string.Empty
                    : string.Join("\n", siteResolution.Diagnostics.Select(value => value.ToString())));
            ResolvedSiteId resolvedPub = SettlementPlanSiteCandidateFacts.CandidateId(
                plan.Id,
                (int)KentridgeRole.Pub);
            Assert.That(
                siteResolution.Bindings.Single(value => value.Role.Equals(pub)).Site,
                Is.EqualTo(resolvedPub),
                "The pub's measured architecture must advertise its real hidden-space hosting capability.");

            var requests = KentridgeHiddenSpaceRequestComposer.Compose(graph, siteResolution, plan);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests[0].RoleId, Is.EqualTo((int)KentridgeRole.Pub));
            Assert.That(requests[0].MinimumCount, Is.EqualTo(1));
            Assert.That(requests[0].TargetCount, Is.EqualTo(1));

            var geometry = KentridgeHiddenSpaceRequestComposer.ResolveArchitecture(
                graph,
                siteResolution,
                plan);
            var provider = new KentridgeHiddenSpaceSecretCandidateProvider(
                plan,
                siteResolution,
                geometry);
            RequiredSecretSpec required = blueprint.RequiredSecrets.Single(value => value.Ref.Equals(requiredRef));

            ResolvedSecretPlan resolved = SecretPlanner.ResolveRequired(required, provider, Seed);

            Assert.That(resolved.SourceKind, Is.EqualTo(SecretResolutionSourceKind.RequiredSecret));
            Assert.That(resolved.RequiredSecret, Is.EqualTo(requiredRef));
            Assert.That(resolved.Site, Is.EqualTo(pub));
            Assert.That(resolved.EntranceId, Does.EndWith("/false-wall"));
            Assert.That(provider.GetCandidates(pub).Single().HiddenFromNormalTraversal, Is.True);

            var physicalFacts = new KentridgeHiddenSpaceVoxelRealizationFacts(
                plan,
                voxelsPerDecimetre: 1,
                geometry);
            ResolvedSecretWorldGeometry physical = SecretWorldGeometryResolver.Resolve(
                resolved,
                physicalFacts);

            Assert.That(physical.Secret, Is.SameAs(resolved));
            Assert.That(physical.HiddenSpaceBounds.UnitsPerDecimetre, Is.EqualTo(1));
            Assert.That(physical.EntranceBounds.UnitsPerDecimetre, Is.EqualTo(1));
            Assert.That(physical.ContainerFloorPoint.UnitsPerDecimetre, Is.EqualTo(1));
            Assert.That(physical.ContainerFloorPoint.Position.Y,
                Is.EqualTo(physical.HiddenSpaceBounds.MinInclusive.Y),
                "The container sits on the top of the room foundation, where the carved interior begins.");
            Assert.That(physical.ContainerFloorPoint.Position.X,
                Is.InRange(
                    physical.HiddenSpaceBounds.MinInclusive.X,
                    physical.HiddenSpaceBounds.MaxInclusive.X));
            Assert.That(physical.ContainerFloorPoint.Position.Z,
                Is.InRange(
                    physical.HiddenSpaceBounds.MinInclusive.Z,
                    physical.HiddenSpaceBounds.MaxInclusive.Z));
        }

        [Test]
        public void HiddenSpaceVoxelCatalogueEmitsSealedRoomAndFalseWallProgram()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var request = new SiteHiddenSpaceRequest(
                "pub-hidden",
                (int)KentridgeRole.Pub,
                minimumCount: 1,
                targetCount: 1,
                HiddenSpaceEntranceKind.BreakableMatchingWall);
            var geometry = KentridgeHiddenSpaceBatchPlanner.Resolve(plan, new[] { request });
            VoxelWorldGenSettings settings = Settings();

            var catalogue = KentridgeHiddenSpaceVoxelCatalogue.Build(
                plan,
                settings,
                geometry,
                Allocator.Temp);
            try
            {
                Assert.That(catalogue.Definitions.Length, Is.EqualTo(1));
                Assert.That(catalogue.Rules.Length, Is.EqualTo(1));
                Assert.That(catalogue.ExplicitPlacements.Length, Is.EqualTo(1));
                Assert.That(catalogue.Program.Length, Is.GreaterThan(0));
                Assert.That(catalogue.Definitions[0].Precedence,
                    Is.EqualTo(KentridgeHiddenSpaceVoxelCatalogue.HiddenSpacePrecedence));
                Assert.That(catalogue.Definitions[0].Name.ToString(), Does.StartWith("kentridge-hidden-"));
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings Settings() =>
            new VoxelWorldGenSettings(
                1,
                new VoxelMaterialMap(
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
                    roadSurface: 12));
    }
}
