using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeaturePresentationSelectionTests
    {
        private static readonly FarFeatureSelectionPolicy.Thresholds Thresholds =
            new FarFeatureSelectionPolicy.Thresholds(100f, 80f, 40f, 30f, 10f, 5f);

        private static readonly FarFeatureSelectionPolicy.DistanceCaps DistanceCaps =
            new FarFeatureSelectionPolicy.DistanceCaps(500f, 2000f, 12000f);

        [Test]
        public void Adapter_CullsSmallOrdinaryAndKeepsLargeAndImportantUnrelatedBakes()
        {
            var source = new FeaturePresentationManifest(sectorSizeVoxels: 64);
            FeaturePresentationBake smallStructure = Bake(
                10UL, 100UL, FeatureKind.Structure,
                new int3(0, 0, 100), new int3(0, 0, 100), material: 1);
            FeaturePresentationBake largeLandform = Bake(
                20UL, 200UL, FeatureKind.Landform,
                new int3(20, 0, 95), new int3(29, 9, 104), material: 2);
            FeaturePresentationBake importantStructure = Bake(
                30UL, 300UL, FeatureKind.Structure,
                new int3(-20, 0, 100), new int3(-20, 0, 100), material: 3);
            source.Upsert(smallStructure);
            source.Upsert(largeLandform);
            source.Upsert(importantStructure);

            var policy = new FarFeatureSelectionPolicy(Thresholds, DistanceCaps, 90f, 1000);
            var adapter = new FarFeaturePresentationAdapter(
                source,
                policy,
                voxelSizeMetres: 1f,
                bake => bake.SourceId == importantStructure.SourceId
                    ? FarFeatureImportance.Important
                    : FarFeatureImportance.Default);

            var selected = adapter.Query(float3.zero, 500f);

            Assert.That(selected.Any(value => value.StableId == smallStructure.SourceId), Is.False,
                "ordinary sub-threshold bakes should leave the sparse render set");
            FarFeatureInstance large = selected.Single(value => value.StableId == largeLandform.SourceId);
            Assert.That(large.Tier, Is.EqualTo(FarFeatureTier.Far));
            Assert.That(large.GeometryKey, Does.StartWith("bake-"));
            FarFeatureInstance important = selected.Single(value => value.StableId == importantStructure.SourceId);
            Assert.That(important.Tier, Is.EqualTo(FarFeatureTier.Horizon));
            Assert.That((important.Flags & FarFeatureVisualFlags.Landmark) != 0, Is.True);
            Assert.That(selected.Select(value => value.StableId), Is.Ordered,
                "the generic adapter should preserve the sparse source's stable ordering");
        }

        [Test]
        public void Policy_BackAndForthInsideThresholdBandIsHysteretic()
        {
            var policy = new FarFeatureSelectionPolicy(Thresholds, DistanceCaps, 90f, 1000);
            var freshPolicy = new FarFeatureSelectionPolicy(Thresholds, DistanceCaps, 90f, 1000);
            var center = float3.zero;
            var extents = new float3(5f);

            Assert.That(policy.Select(77UL, center, extents, new float3(0f, 0f, 100f)),
                Is.EqualTo(FarFeatureTier.Far));
            Assert.That(policy.Select(77UL, center, extents, new float3(0f, 0f, 150f)),
                Is.EqualTo(FarFeatureTier.Far),
                "an already-visible feature should stay Far above the Far exit threshold");
            Assert.That(freshPolicy.Select(77UL, center, extents, new float3(0f, 0f, 150f)),
                Is.EqualTo(FarFeatureTier.Culled),
                "the same projected size should not enter until it crosses the farther Far enter threshold");
            Assert.That(policy.Select(77UL, center, extents, new float3(0f, 0f, 180f)),
                Is.EqualTo(FarFeatureTier.Culled));
            Assert.That(policy.Select(77UL, center, extents, new float3(0f, 0f, 150f)),
                Is.EqualTo(FarFeatureTier.Culled),
                "backtracking inside the hysteresis band must not immediately re-enter");
        }

        private static FeaturePresentationBake Bake(
            ulong sourceId,
            ulong revision,
            FeatureKind kind,
            int3 min,
            int3 max,
            byte material)
        {
            var primitive = new Primitive
            {
                Shape = PrimitiveShape.Box,
                Mode = PrimitiveMode.Fill,
                Material = material,
                SurfaceStyle = (ushort)(material * 10),
                Coating = (byte)(material + 1),
                A = min,
                B = max,
            };
            return new FeaturePresentationBake(
                sourceId,
                revision,
                kind,
                min,
                0,
                min,
                max,
                new[] { primitive });
        }
    }
}
