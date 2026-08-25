using System;
using System.IO;
using System.Linq;
using Game.Composition.Kentridge.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderAuthoringVisibilityTests
    {
        private static string RepoRoot
        {
            get
            {
                var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages")))
                    directory = directory.Parent;

                Assert.NotNull(directory, "Could not locate project root containing Packages/.");
                return directory.FullName;
            }
        }

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
        public void KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary()
        {
            Type authoringType = typeof(BlueprintCompiler).Assembly.GetType(
                "Game.WorldBuilder.Runtime.WorldBuilderTownAuthoring");
            string showcaseAsmdef = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "Game",
                "Composition",
                "Showcase",
                "Game.Composition.Showcase.asmdef"));
            Type[] publicPlanParameterTypes = typeof(KentridgeCampaignSessionBootstrap)
                .GetMethods()
                .Where(method => method.Name == "Plan")
                .SelectMany(method => method.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(
                    authoringType,
                    Is.Not.Null,
                    "Town authoring must enter through Game.WorldBuilder.Runtime rather than a content-specific generator.");
                StringAssert.DoesNotContain(
                    "MountingForce.WorldGen",
                    showcaseAsmdef,
                    "VoxelShowcase must depend on WorldBuilder's public/runtime boundary, not the legacy backend assemblies.");
                Assert.That(
                    publicPlanParameterTypes.Any(IsLegacyWorldGenType),
                    Is.False,
                    "Kentridge's public campaign bootstrap must not expose a MountingForce.WorldGen planning type.");
            });
        }

        [Test]
        public void EditModeFriendAssemblyRetainsTestOnlyIdentityConstruction()
        {
            Assert.That(new RegionRef("test-region").Id, Is.EqualTo("test-region"));
            Assert.That(new SiteRef("test-site").Id, Is.EqualTo("test-site"));
            Assert.That(new NpcRef("test-npc").Id, Is.EqualTo("test-npc"));
        }

        private static bool IsLegacyWorldGenType(Type type)
        {
            string ns = type.Namespace ?? string.Empty;
            return ns.StartsWith("MountingForce.WorldGen", StringComparison.Ordinal);
        }
    }
}
