using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthTests
    {
        [UnityTest]
        public IEnumerator AuthoredGrowthCoversLeftPierCrownAndRightCounterweight()
        {
            var root = new GameObject("Arch reference growth contract");
            try
            {
                ArchReferenceGrowth growth = root.AddComponent<ArchReferenceGrowth>();
                yield return null;

                Assert.That(growth.Instances, Has.Count.EqualTo(60));
                Assert.That(growth.InstanceCount, Is.EqualTo(growth.Instances.Count),
                    "Every authored plant must be submitted through the production batch renderer.");

                int leftPierIvy = growth.Instances.Count(instance =>
                    instance.Kind == VegetationKind.Ivy
                    && instance.PositionMetres.x < -1.1f
                    && instance.PositionMetres.y < 6.1f);
                int leftPierFlowers = growth.Instances.Count(instance =>
                    instance.Kind == VegetationKind.Flower
                    && instance.PositionMetres.x < -1.1f
                    && instance.PositionMetres.y > 2f
                    && instance.PositionMetres.y < 6.1f);
                int crownIvy = growth.Instances.Count(instance =>
                    instance.Kind == VegetationKind.Ivy
                    && instance.PositionMetres.x < 0.6f
                    && instance.PositionMetres.y >= 6.1f);
                int crownFlowers = growth.Instances.Count(instance =>
                    instance.Kind == VegetationKind.Flower
                    && instance.PositionMetres.x < 0.6f
                    && instance.PositionMetres.y >= 6.1f);
                int rightIvy = growth.Instances.Count(instance =>
                    instance.Kind == VegetationKind.Ivy
                    && instance.PositionMetres.x > 1f
                    && instance.PositionMetres.y > 1f);

                Assert.That(leftPierIvy, Is.EqualTo(21));
                Assert.That(leftPierFlowers, Is.EqualTo(5));
                Assert.That(crownIvy, Is.EqualTo(15));
                Assert.That(crownFlowers, Is.EqualTo(4));
                Assert.That(rightIvy, Is.EqualTo(8));

                Assert.That(growth.Instances.Where(instance =>
                        instance.Kind == VegetationKind.Flower
                        && instance.SurfaceNormal.z < -0.9f)
                    .All(instance => instance.Scale >= 0.48f), Is.True,
                    "Wall flower heads must remain large enough to survive the saved hero-camera distance.");

                growth.enabled = false;
                Assert.That(growth.InstanceCount, Is.Zero,
                    "Disabling the authoring component must release its renderer submission.");
                growth.enabled = true;
                Assert.That(growth.InstanceCount, Is.EqualTo(60),
                    "Re-enabling after a scene lifecycle transition must restore all authored growth.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
