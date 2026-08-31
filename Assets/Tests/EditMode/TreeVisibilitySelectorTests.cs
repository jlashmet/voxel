using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TreeVisibilitySelectorTests
    {
        [Test]
        public void Select_SplitsIndividualsFromCanopyWithoutDuplicatingTreeTruth()
        {
            var trees = new List<TreeVisibilityEntry>
            {
                Entry(1UL, 0, 60f),
                Entry(2UL, 1, 300f),
                Entry(3UL, 2, 900f),
            };
            var individuals = new List<SelectedTreePresentation>();
            var canopy = new List<TreeVisibilityEntry>();
            var selector = new TreeVisibilitySelector();
            var policy = new TreeVisibilityTierPolicy(120f, 480f, 1800f, 12000f, 20f);

            selector.Select(trees, float3.zero, in policy, null, individuals, canopy);

            Assert.That(individuals.Count, Is.EqualTo(2));
            Assert.That(individuals[0].StableId, Is.EqualTo(1UL));
            Assert.That(individuals[0].Tier, Is.EqualTo(TreePresentationTier.Full));
            Assert.That(individuals[1].StableId, Is.EqualTo(2UL));
            Assert.That(individuals[1].Tier, Is.EqualTo(TreePresentationTier.Simplified));
            Assert.That(canopy.Count, Is.EqualTo(1));
            Assert.That(canopy[0].StableId, Is.EqualTo(3UL));
        }

        [Test]
        public void Select_LandmarkRemainsIndependentAndSeveredTreeDisappears()
        {
            var trees = new List<TreeVisibilityEntry>
            {
                Entry(10UL, 0, 10000f),
                Entry(11UL, 1, 40f, severed: true),
            };
            var individuals = new List<SelectedTreePresentation>();
            var canopy = new List<TreeVisibilityEntry>();
            var selector = new TreeVisibilitySelector();
            var policy = new TreeVisibilityTierPolicy(120f, 480f, 1800f, 12000f, 20f);

            selector.Select(
                trees, float3.zero, in policy,
                tree => tree.StableId == 10UL,
                individuals, canopy);

            Assert.That(individuals.Count, Is.EqualTo(1));
            Assert.That(individuals[0].StableId, Is.EqualTo(10UL));
            Assert.That(individuals[0].Tier, Is.EqualTo(TreePresentationTier.Landmark));
            Assert.That(canopy, Is.Empty);
        }

        [Test]
        public void Select_PreservesHysteresisAcrossConsecutiveQueries()
        {
            var individuals = new List<SelectedTreePresentation>();
            var canopy = new List<TreeVisibilityEntry>();
            var selector = new TreeVisibilitySelector();
            var policy = new TreeVisibilityTierPolicy(120f, 480f, 1800f, 12000f, 20f);

            selector.Select(new[] { Entry(21UL, 0, 110f) }, float3.zero, in policy, null, individuals, canopy);
            Assert.That(individuals[0].Tier, Is.EqualTo(TreePresentationTier.Full));

            selector.Select(new[] { Entry(21UL, 0, 132f) }, float3.zero, in policy, null, individuals, canopy);
            Assert.That(individuals[0].Tier, Is.EqualTo(TreePresentationTier.Full));

            selector.Select(new[] { Entry(21UL, 0, 145f) }, float3.zero, in policy, null, individuals, canopy);
            Assert.That(individuals[0].Tier, Is.EqualTo(TreePresentationTier.Simplified));
        }

        private static TreeVisibilityEntry Entry(
            ulong stableId, int sourceIndex, float x, bool severed = false)
        {
            var instance = new TreeInstance
            {
                PositionMetres = new float3(x, 0f, 0f),
                Species = TreeSpecies.Oak,
                Seed = (uint)(sourceIndex + 1),
                Scale = 1f,
            };
            return new TreeVisibilityEntry(
                stableId,
                sourceIndex,
                (int)math.floor(x / 64f),
                0,
                instance,
                new TreeDamageState(1f, severed));
        }
    }
}
