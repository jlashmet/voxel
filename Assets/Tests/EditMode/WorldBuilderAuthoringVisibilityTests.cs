using System;
using System.Linq;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderAuthoringVisibilityTests
    {
        [Test]
        public void StableSemanticRefsHaveNoPublicStringConstructors()
        {
            Type[] referenceTypes =
            {
                typeof(RegionRef),
                typeof(RouteRef),
                typeof(SettlementRef),
                typeof(SiteRef),
                typeof(NpcRef),
                typeof(CutsceneRef),
                typeof(StoryRuleRef),
                typeof(ObjectiveRef),
                typeof(LootTableRef),
                typeof(SecretPolicyRef),
                typeof(SecretRef)
            };

            for (var i = 0; i < referenceTypes.Length; i++)
            {
                Assert.That(
                    referenceTypes[i].GetConstructors(),
                    Is.Empty,
                    referenceTypes[i].Name + " must remain a public value type with non-public identity construction.");
            }
        }

        [Test]
        public void LegacyOwnershipAndPlacementEntryPointsAreNotPublic()
        {
            string[] worldMethods =
            {
                "RequireRegion",
                "RequireRoute",
                "RequireSettlement",
                "RequireSite",
                "RequireNpc"
            };

            for (var i = 0; i < worldMethods.Length; i++)
            {
                string methodName = worldMethods[i];
                Assert.That(
                    typeof(WorldBlueprintBuilder).GetMethods().Any(method => method.Name == methodName),
                    Is.False,
                    methodName + " must remain an internal compatibility entry point; use typed handles in production authoring.");
            }

            Assert.That(
                typeof(StoryBlueprintBuilder).GetMethods().Any(method => method.Name == "Objective"),
                Is.False);
            Assert.That(
                typeof(StoryBlueprintBuilder).GetMethods().Any(method => method.Name == "Cutscene"),
                Is.False);
            Assert.That(
                typeof(StoryBlueprintBuilder).GetMethods().Any(method => method.Name == "Rule"),
                Is.True,
                "Story rules remain a public authoring entry point until their typed facade replaces them.");
        }

        [Test]
        public void EditModeFriendAssemblyRetainsTestOnlyIdentityConstruction()
        {
            Assert.That(new RegionRef("test-region").Id, Is.EqualTo("test-region"));
            Assert.That(new SiteRef("test-site").Id, Is.EqualTo("test-site"));
            Assert.That(new NpcRef("test-npc").Id, Is.EqualTo("test-npc"));
        }
    }
}
