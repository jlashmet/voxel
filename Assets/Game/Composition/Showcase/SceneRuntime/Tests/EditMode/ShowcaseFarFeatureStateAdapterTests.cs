using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseFarFeatureStateAdapterTests
    {
        [Test]
        public void RemovedLandmarkDoesNotReappearAfterDetailedInstanceIsDiscarded()
        {
            const ulong structureId = 0xC4571EUL;
            var states = new StructureVisualStateStore();
            var adapter = CreateAdapter(states);
            FarFeatureInstance original = Instance(structureId);

            states.Set(structureId, StructureVisualState.Removed);
            Assert.That(adapter.Apply(new[] { original }), Is.Empty);

            FarFeatureInstance afterUnload = Instance(structureId);
            Assert.That(adapter.Apply(new[] { afterUnload }), Is.Empty);
            Assert.That(states.Get(structureId), Is.EqualTo(StructureVisualState.Removed));
        }

        [Test]
        public void RuinedStateUsesSameStableIdAndFlowsAsRenderFlag()
        {
            const ulong structureId = 0xB017DUL;
            var states = new StructureVisualStateStore();
            var adapter = CreateAdapter(states);
            states.Set(structureId, StructureVisualState.Ruined);

            IReadOnlyList<FarFeatureInstance> result = adapter.Apply(new[] { Instance(structureId) });

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].StableId, Is.EqualTo(structureId));
            Assert.That((result[0].Flags & FarFeatureVisualFlags.Ruined) != 0, Is.True);
        }

        private static ShowcaseFarFeatureStateAdapter CreateAdapter(StructureVisualStateStore states)
        {
            var selection = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(100f, 80f, 60f, 40f, 20f, 10f),
                new FarFeatureSelectionPolicy.DistanceCaps(1000f, 2000f, 3000f),
                60f,
                1080);
            var presentation = new FarFeaturePresentationAdapter(new EmptyPresentationSource(), selection, 1f);
            return new ShowcaseFarFeatureStateAdapter(presentation, states);
        }

        private static FarFeatureInstance Instance(ulong stableId)
        {
            return new FarFeatureInstance(
                stableId,
                float3.zero,
                quaternion.identity,
                new float3(10f),
                new float3(0f, 5f, 0f),
                new float3(5f),
                "landmark-geometry",
                "stone",
                FarFeatureTier.Far,
                FarFeatureVisualFlags.Landmark);
        }

        private sealed class EmptyPresentationSource : IFeaturePresentationSource
        {
            private static readonly FeaturePresentationBake[] Empty = new FeaturePresentationBake[0];

            public bool TryGet(ulong sourceId, out FeaturePresentationBake bake)
            {
                bake = null;
                return false;
            }

            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds) => Empty;
        }
    }
}
