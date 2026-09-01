using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureRenderingTests
    {
        [Test]
        public void GenericContract_RepresentsStructureAndNaturalBakeThroughSameRenderer()
        {
            var structure = new FarFeatureInstance(
                0xCA57EUL,
                new float3(1200f, 80f, -400f),
                quaternion.identity,
                new float3(48f, 72f, 52f),
                new float3(1200f, 116f, -400f),
                new float3(24f, 36f, 26f),
                "baked-structure-massing",
                "stone",
                FarFeatureTier.Horizon,
                FarFeatureVisualFlags.Landmark);
            var natural = new FarFeatureInstance(
                0xB01DUL,
                new float3(-900f, 34f, 650f),
                quaternion.RotateY(math.radians(18f)),
                new float3(28f, 68f, 24f),
                new float3(-900f, 68f, 650f),
                new float3(14f, 34f, 12f),
                "baked-natural-massing",
                "granite",
                FarFeatureTier.Far);

            Assert.That(structure.GeometryKey, Is.EqualTo("baked-structure-massing"));
            Assert.That(natural.GeometryKey, Is.EqualTo("baked-natural-massing"));

            var go = new GameObject("generic-far-feature-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                renderer.SetInstances(new[] { structure, natural });

                Assert.That(renderer.InstanceCount, Is.EqualTo(2));
                Assert.That(renderer.PersistentInstanceObjectCount, Is.Zero);
                Assert.That(renderer.BatchKeyFor(structure), Is.Not.EqualTo(renderer.BatchKeyFor(natural)));
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AutomaticallyBakedUnrelatedShapes_UseDistinctCachedMassingThroughSameRenderer()
        {
            var source = new FeaturePresentationManifest(sectorSizeVoxels: 64);
            FeaturePresentationBake structureBake = Bake(
                0x100UL,
                0xA001UL,
                FeatureKind.Structure,
                PrimitiveShape.Box,
                new int3(-4, 0, 96),
                new int3(4, 15, 104));
            FeaturePresentationBake naturalBake = Bake(
                0x200UL,
                0xB002UL,
                FeatureKind.Landform,
                PrimitiveShape.Cylinder,
                new int3(20, 0, 92),
                new int3(30, 18, 102));
            source.Upsert(structureBake);
            source.Upsert(naturalBake);

            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(100f, 80f, 40f, 30f, 10f, 5f),
                new FarFeatureSelectionPolicy.DistanceCaps(500f, 2000f, 12000f),
                90f,
                1000);
            var adapter = new FarFeaturePresentationAdapter(
                source,
                policy,
                voxelSizeMetres: 1f,
                _ => FarFeatureImportance.Important);
            var selected = adapter.Query(float3.zero, 500f);
            FarFeatureInstance structure = selected.Single(value => value.StableId == structureBake.SourceId);
            FarFeatureInstance natural = selected.Single(value => value.StableId == naturalBake.SourceId);

            Assert.That(structure.Geometry, Is.Not.Null);
            Assert.That(natural.Geometry, Is.Not.Null);
            Assert.That(structure.Geometry.GetPrimitive(0).Shape, Is.EqualTo(FarFeatureGeometryShape.Box));
            Assert.That(natural.Geometry.GetPrimitive(0).Shape, Is.EqualTo(FarFeatureGeometryShape.Cylinder));

            var go = new GameObject("generic-baked-massing-renderer-test");
            try
            {
                var renderer = go.AddComponent<ProceduralFarFeatureRenderer>();
                renderer.SetInstances(selected);

                Mesh structureMesh = renderer.ResolveMesh(structure);
                Mesh naturalMesh = renderer.ResolveMesh(natural);
                Assert.That(structureMesh.vertexCount, Is.EqualTo(8));
                Assert.That(naturalMesh.vertexCount, Is.GreaterThan(structureMesh.vertexCount),
                    "unrelated baked shapes must not collapse to the same fallback cube silhouette");
                Assert.That(renderer.ResolveMesh(structure), Is.SameAs(structureMesh),
                    "immutable baked geometry should reuse its cached mesh");
                Assert.That(renderer.InstanceCount, Is.EqualTo(2));
                Assert.That(renderer.PersistentInstanceObjectCount, Is.Zero);
                Assert.That(go.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static FeaturePresentationBake Bake(
            ulong sourceId,
            ulong revision,
            FeatureKind kind,
            PrimitiveShape shape,
            int3 min,
            int3 max)
        {
            var primitive = new Primitive
            {
                Shape = shape,
                Mode = PrimitiveMode.Fill,
                Material = 1,
                SurfaceStyle = 10,
                Axis = 1,
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
