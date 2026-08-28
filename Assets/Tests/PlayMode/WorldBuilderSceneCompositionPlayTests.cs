using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode.KentridgePlayableScenePlayTests
{
    /// <summary>
    /// Fast semantic half of the all-scenes acceptance. The single-test workflow recognizes this
    /// namespace as a Kentridge playable-scene profile, so after this assertion it also builds and
    /// launches the actual KentridgePlayableSlice player through showcase-player-capture.sh.
    /// </summary>
    public sealed class WorldBuilderCompositionTests
    {
        [Test]
        public void WorldBuilderRecipesResolveDistinctProductionPlans()
        {
            const uint seed = 0x4B454E54u;

            AuthoredTownPlan kentridge = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Kentridge,
                seed);
            AuthoredTownPlan hightown = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Hightown,
                seed);

            Assert.That(kentridge.BackendPlan, Is.TypeOf<SettlementPlan>());
            Assert.That(hightown.BackendPlan, Is.TypeOf<SettlementPlan>());
            var kentridgePlan = (SettlementPlan)kentridge.BackendPlan;
            var hightownPlan = (SettlementPlan)hightown.BackendPlan;
            Assert.That(kentridgePlan.CentreDm.Equals(hightownPlan.CentreDm), Is.False);
            Assert.That(kentridgePlan.Plots.Count, Is.GreaterThan(0));
            Assert.That(hightownPlan.Plots.Count, Is.GreaterThan(0));

            WorldEnvironmentSpec showcase = WorldBuilderEnvironmentComposition.SemanticSpec(
                seed,
                ShowcaseFeatureContent.Full);
            WorldBuilderEnvironmentComposition.Plan showcasePlan =
                WorldBuilderEnvironmentComposition.Resolve(in showcase);
            Assert.That(showcasePlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.Full));

            WorldEnvironmentSpec focused = WorldEnvironmentRecipes.DetailedStructure(seed + 1u);
            WorldBuilderEnvironmentComposition.Plan focusedPlan =
                WorldBuilderEnvironmentComposition.Resolve(in focused);
            Assert.That(focusedPlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.HouseOnly));
            Assert.That(focusedPlan.Seed, Is.EqualTo(seed + 1u));
        }
    }
}
