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

        [Test]
        public void LandmarkProxyRetiresWhenItsBoundsOverlapPublishedNearSurface()
        {
            var adapter = CreateAdapter(new StructureVisualStateStore());
            FarFeatureInstance landmark = Instance(
                0xA11CEUL,
                boundsCenter: new float3(120f, 20f, 0f),
                boundsExtents: new float3(80f, 20f, 80f));

            IReadOnlyList<FarFeatureInstance> result = adapter.Apply(
                new[] { landmark },
                nearSurfaceCentre: float3.zero,
                nearSurfaceRadiusMetres: 64f);

            Assert.That(result, Is.Empty,
                "A whole-feature semantic proxy cannot remain drawn once any of its horizontal bounds overlap the published detailed surface.");
        }

        [Test]
        public void LandmarkProxyRemainsWhenEntirelyOutsidePublishedNearSurface()
        {
            var adapter = CreateAdapter(new StructureVisualStateStore());
            FarFeatureInstance landmark = Instance(
                0xFA12UL,
                boundsCenter: new float3(160f, 20f, 0f),
                boundsExtents: new float3(40f, 20f, 40f));

            IReadOnlyList<FarFeatureInstance> result = adapter.Apply(
                new[] { landmark },
                nearSurfaceCentre: float3.zero,
                nearSurfaceRadiusMetres: 64f);

            Assert.That(result, Has.Count.EqualTo(1),
                "Semantic far representation must remain available while the feature is wholly beyond published near coverage.");
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

        private static FarFeatureInstance Instance(
            ulong stableId,
            float3? boundsCenter = null,
            float3? boundsExtents = null)
        {
            float3 center = boundsCenter ?? new float3(0f, 5f, 0f);
            float3 extents = boundsExtents ?? new float3(5f);
            return new FarFeatureInstance(
                stableId,
                new float3(center.x, center.y - extents.y, center.z),
                quaternion.identity,
                extents * 2f,
                center,
                extents,
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
