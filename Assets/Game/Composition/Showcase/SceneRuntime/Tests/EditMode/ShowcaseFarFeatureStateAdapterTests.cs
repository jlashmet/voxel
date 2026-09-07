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
        public void RuinedStateUsesSameStableIdAndPreservesResolvedPresentation()
        {
            const ulong structureId = 0xB017DUL;
            var states = new StructureVisualStateStore();
            var adapter = CreateAdapter(states);
            states.Set(structureId, StructureVisualState.Ruined);

            IReadOnlyList<FarFeatureInstance> result = adapter.Apply(new[] { Instance(structureId) });

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].StableId, Is.EqualTo(structureId));
            Assert.That((result[0].Flags & FarFeatureVisualFlags.Ruined) != 0, Is.True);
            Assert.That(result[0].Presentation.Albedo.x, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(result[0].Presentation.Albedo.y, Is.EqualTo(0.41f).Within(0.0001f));
            Assert.That(result[0].Presentation.Albedo.z, Is.EqualTo(0.63f).Within(0.0001f));
            Assert.That(result[0].Presentation.Roughness, Is.EqualTo(0.79f).Within(0.0001f));
        }

        [Test]
        public void ResidentCandidatesObserveRemovalRuinAndRestorationWithoutRequeryingGeometry()
        {
            var states=new StructureVisualStateStore();
            var source=new ResidentSource();
            var selection=new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24,18,4,3,1.5f,1),
                new FarFeatureSelectionPolicy.DistanceCaps(1000,1000,1000),60,1080);
            var adapter=new ShowcaseFarFeatureStateAdapter(new FarFeaturePresentationAdapter(source,selection,1),states);
            var first=adapter.QueryCandidates(float3.zero,1000)[0];
            ulong version=adapter.CandidateVersion;
            adapter.QueryCandidates(new float3(1,0,0),1000);
            Assert.AreEqual(version,adapter.CandidateVersion);
            states.Set(7,StructureVisualState.Removed);
            Assert.AreEqual(0,adapter.QueryCandidates(float3.zero,1000).Count);
            Assert.Greater(adapter.CandidateVersion,version);
            states.Set(7,StructureVisualState.Ruined);
            var ruined=adapter.QueryCandidates(float3.zero,1000)[0];
            Assert.AreEqual(first.Geometry,ruined.Geometry);
            Assert.AreNotEqual(0,ruined.Flags & FarFeatureVisualFlags.Ruined);
            states.Remove(7);
            Assert.AreEqual(FarFeatureVisualFlags.None,adapter.QueryCandidates(float3.zero,1000)[0].Flags);
            states.Set(7,StructureVisualState.Removed);states.Clear();
            Assert.AreEqual(1,adapter.QueryCandidates(float3.zero,1000).Count);
            Assert.AreEqual(1,source.Queries);
        }

        private sealed class ResidentSource : IVersionedFeaturePresentationSource
        {
            public ulong Revision=>1;
            public int Queries;
            private readonly FeaturePresentationBake[] _bakes={new FeaturePresentationBake(7,1,default,int3.zero,0,
                int3.zero,new int3(10),new[]{new Primitive {Shape=PrimitiveShape.Box,Mode=PrimitiveMode.Fill,
                    A=int3.zero,B=new int3(10),Material=1}})};
            public bool TryGet(ulong id,out FeaturePresentationBake bake){bake=_bakes[0];return id==7;}
            public IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds){Queries++;return _bakes;}
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
                FarFeatureVisualFlags.Landmark,
                null,
                new FarFeaturePresentation(new float4(0.22f, 0.41f, 0.63f, 1f), 0.79f));
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
