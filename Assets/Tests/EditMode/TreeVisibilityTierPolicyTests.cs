using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TreeVisibilityTierPolicyTests
    {
        private static TreeVisibilityTierPolicy Policy() =>
            new(120f, 480f, 1800f, 12000f, 20f);

        private static TreePresentationInput Tree(
            float x,
            float scale = 1f,
            bool severed = false,
            bool landmark = false,
            float health01 = 1f) =>
            new(17UL, new float3(x, 0f, 0f), scale, health01, severed, landmark);

        [Test]
        public void Select_UsesFullThenSimplifiedThenCanopyTiers()
        {
            var policy = Policy();
            float3 camera = float3.zero;

            Assert.That(policy.Select(Tree(80f), camera), Is.EqualTo(TreePresentationTier.Full));
            Assert.That(policy.Select(Tree(300f), camera), Is.EqualTo(TreePresentationTier.Simplified));
            Assert.That(policy.Select(Tree(900f), camera), Is.EqualTo(TreePresentationTier.CanopyMember));
            Assert.That(policy.Select(Tree(2500f), camera), Is.EqualTo(TreePresentationTier.Culled));
        }

        [Test]
        public void Select_ScalePreservesLargeTreeSignificance()
        {
            var policy = Policy();
            float3 camera = float3.zero;

            Assert.That(policy.Select(Tree(700f, scale: 2f), camera),
                Is.EqualTo(TreePresentationTier.Simplified));
            Assert.That(policy.Select(Tree(700f, scale: 0.5f), camera),
                Is.EqualTo(TreePresentationTier.CanopyMember));
        }

        [Test]
        public void Select_LandmarkTreeRemainsIndependentOfCanopyRange()
        {
            var policy = Policy();
            float3 camera = float3.zero;

            Assert.That(policy.Select(Tree(10000f, landmark: true), camera),
                Is.EqualTo(TreePresentationTier.Landmark));
            Assert.That(policy.Select(Tree(12500f, landmark: true), camera),
                Is.EqualTo(TreePresentationTier.Culled));
        }

        [Test]
        public void Select_SeveredOrDestroyedTreeIsAlwaysCulled()
        {
            var policy = Policy();
            float3 camera = float3.zero;

            Assert.That(policy.Select(Tree(10f, severed: true), camera),
                Is.EqualTo(TreePresentationTier.Culled));
            Assert.That(policy.Select(Tree(10f, health01: 0f), camera),
                Is.EqualTo(TreePresentationTier.Culled));
        }

        [Test]
        public void Select_HoldsPreviousTierAcrossExitBoundary()
        {
            var policy = Policy();
            float3 camera = float3.zero;

            Assert.That(policy.Select(Tree(130f), camera, TreePresentationTier.Full),
                Is.EqualTo(TreePresentationTier.Full));
            Assert.That(policy.Select(Tree(145f), camera, TreePresentationTier.Full),
                Is.EqualTo(TreePresentationTier.Simplified));

            Assert.That(policy.Select(Tree(490f), camera, TreePresentationTier.Simplified),
                Is.EqualTo(TreePresentationTier.Simplified));
            Assert.That(policy.Select(Tree(510f), camera, TreePresentationTier.Simplified),
                Is.EqualTo(TreePresentationTier.CanopyMember));
        }
    }
}
