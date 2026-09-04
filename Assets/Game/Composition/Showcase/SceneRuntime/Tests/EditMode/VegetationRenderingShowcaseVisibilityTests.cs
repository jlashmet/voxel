using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VegetationRenderingShowcaseVisibilityTests
    {
        [Test]
        public void SelectionUsesSectorQueryAndProjectedSignificanceWithoutMutatingWorldTruth()
        {
            var source = new List<VegetationInstance>
            {
                Instance(new float3(2f, 0f, 0f), 11u, 1f),
                Instance(new float3(80f, 0f, 0f), 22u, 1f),
                Instance(new float3(400f, 0f, 0f), 33u, 1f),
            };
            ulong nearId = VegetationVisibility.StableVegetationId(source[0]);
            ulong midId = VegetationVisibility.StableVegetationId(source[1]);
            ulong farId = VegetationVisibility.StableVegetationId(source[2]);
            var scratch = new List<VegetationVisibilityEntry>();
            var visible = new List<VegetationInstance>();
            var policy = VegetationRenderingShowcase.CreateVisibilityPolicy(null);

            VegetationRenderingShowcase.SelectVisibleInstances(
                source,
                float3.zero,
                policy,
                scratch,
                visible);

            Assert.That(source.Count, Is.EqualTo(3), "visibility must not mutate deterministic placement truth");
            Assert.That(Contains(visible, nearId), Is.True);
            Assert.That(Contains(visible, farId), Is.False, "out-of-sector/radius scatter must not be submitted");

            VegetationRenderingShowcase.SelectVisibleInstances(
                source,
                new float3(80f, 0f, 0f),
                policy,
                scratch,
                visible);

            Assert.That(source.Count, Is.EqualTo(3));
            Assert.That(Contains(visible, midId), Is.True, "moving the camera should query/select the new local sectors");
        }

        [Test]
        public void RepeatedSelectionProducesStableMembershipOrder()
        {
            var source = new List<VegetationInstance>
            {
                Instance(new float3(4f, 0f, 3f), 91u, 0.9f),
                Instance(new float3(-5f, 0f, 2f), 17u, 1.2f),
                Instance(new float3(8f, 0f, -6f), 53u, 1f),
            };
            var firstScratch = new List<VegetationVisibilityEntry>();
            var first = new List<VegetationInstance>();
            var secondScratch = new List<VegetationVisibilityEntry>();
            var second = new List<VegetationInstance>();

            VegetationRenderingShowcase.SelectVisibleInstances(
                source,
                float3.zero,
                VegetationRenderingShowcase.CreateVisibilityPolicy(null),
                firstScratch,
                first);
            VegetationRenderingShowcase.SelectVisibleInstances(
                source,
                float3.zero,
                VegetationRenderingShowcase.CreateVisibilityPolicy(null),
                secondScratch,
                second);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
                Assert.That(
                    VegetationVisibility.StableVegetationId(second[i]),
                    Is.EqualTo(VegetationVisibility.StableVegetationId(first[i])));
        }

        private static bool Contains(IReadOnlyList<VegetationInstance> instances, ulong stableId)
        {
            for (int i = 0; i < instances.Count; i++)
                if (VegetationVisibility.StableVegetationId(instances[i]) == stableId) return true;
            return false;
        }

        private static VegetationInstance Instance(float3 position, uint seed, float scale) =>
            new VegetationInstance
            {
                PositionMetres = position,
                SurfaceNormal = new float3(0f, 1f, 0f),
                Kind = VegetationKind.Fern,
                Seed = seed,
                Scale = scale,
            };
    }
}
