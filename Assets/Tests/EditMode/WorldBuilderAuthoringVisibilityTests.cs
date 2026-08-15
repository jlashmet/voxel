using System;
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
        public void EditModeFriendAssemblyRetainsTestOnlyIdentityConstruction()
        {
            Assert.That(new RegionRef("test-region").Id, Is.EqualTo("test-region"));
            Assert.That(new SiteRef("test-site").Id, Is.EqualTo("test-site"));
            Assert.That(new NpcRef("test-npc").Id, Is.EqualTo("test-npc"));
        }
    }
}
