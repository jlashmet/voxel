using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VisibleVegetationBatchAdapterTests
    {
        [Test]
        public void Apply_SubmitsOnlyQueriedVisibleSectorMembership()
        {
            VegetationInstance near = Vegetation(1u, new float3(5f, 0f, 5f));
            VegetationInstance far = Vegetation(2u, new float3(105f, 0f, 5f));
            var visible = new List<VegetationVisibilityEntry>();
            VegetationVisibility.QueryVegetation(
                new[] { near, far },
                10f,
                new VisibilitySectorBounds(0, 0, 0, 0),
                visible);

            var go = new GameObject("visible-vegetation-batch-test");
            try
            {
                var renderer = go.AddComponent<ProceduralVegetationBatchRenderer>();
                var adapter = new VisibleVegetationBatchAdapter();

                adapter.Apply(renderer, visible);

                Assert.That(adapter.VisibleCount, Is.EqualTo(1));
                Assert.That(renderer.InstanceCount, Is.EqualTo(1),
                    "instances outside the queried sectors must never enter renderer draw batches");
                Assert.That(adapter.VisibleInstances[0].Seed, Is.EqualTo(near.Seed));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ReusesPresentationScratchAndBatchKeyAsVisibleWindowMoves()
        {
            VegetationInstance west = Vegetation(11u, new float3(5f, 0f, 5f));
            VegetationInstance east = Vegetation(22u, new float3(105f, 0f, 5f));
            var all = new[] { west, east };
            var visible = new List<VegetationVisibilityEntry>();
            var go = new GameObject("visible-vegetation-reuse-test");
            try
            {
                var renderer = go.AddComponent<ProceduralVegetationBatchRenderer>();
                var adapter = new VisibleVegetationBatchAdapter();
                IReadOnlyList<VegetationInstance> scratch = adapter.VisibleInstances;

                VegetationVisibility.QueryVegetation(
                    all, 10f, new VisibilitySectorBounds(0, 0, 0, 0), visible);
                adapter.Apply(renderer, visible);
                Assert.That(adapter.VisibleInstances, Is.SameAs(scratch));
                Assert.That(adapter.VisibleInstances[0].Seed, Is.EqualTo(west.Seed));
                Assert.That(renderer.BatchKindCount, Is.EqualTo(1));

                VegetationVisibility.QueryVegetation(
                    all, 10f, new VisibilitySectorBounds(10, 0, 10, 0), visible);
                adapter.Apply(renderer, visible);

                Assert.That(adapter.VisibleInstances, Is.SameAs(scratch),
                    "camera-sector churn should reuse the adapter's presentation scratch list");
                Assert.That(renderer.InstanceCount, Is.EqualTo(1));
                Assert.That(renderer.BatchKindCount, Is.EqualTo(1),
                    "the existing kind batch must be cleared and reused rather than recreated");
                Assert.That(adapter.VisibleInstances[0].Seed, Is.EqualTo(east.Seed));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static VegetationInstance Vegetation(uint seed, float3 position) =>
            new VegetationInstance
            {
                Seed = seed,
                PositionMetres = position,
                SurfaceNormal = new float3(0f, 1f, 0f),
                Kind = VegetationKind.Bush,
                Scale = 1f,
            };
    }
}
