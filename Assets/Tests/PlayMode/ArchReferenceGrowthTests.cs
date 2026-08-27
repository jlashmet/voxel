using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ArchReferenceGrowthTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator HeroGrowthUsesAuthoredLeavesAndFlowerClustersWithinBudget()
        {
            var root = new GameObject("Arch reference growth regression");
            try
            {
                ArchReferenceGrowth growth = root.AddComponent<ArchReferenceGrowth>();
                yield return null;

                Assert.That(growth.HeroLeafCount, Is.EqualTo(128),
                    "The reference hero should be built from individual lobed ivy leaves, not generic vine stamps.");
                Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30),
                    "Reference flowers should remain clustered multi-head blossoms rather than isolated semantic cards.");
                Assert.That(growth.SemanticInstanceCount, Is.EqualTo(2),
                    "Only the two small ground ferns should remain on the shared semantic vegetation renderer.");
                Assert.That(growth.Instances, Has.Count.EqualTo(2));
                Assert.That(growth.Instances[0].Kind, Is.EqualTo(VegetationKind.Fern));
                Assert.That(growth.Instances[1].Kind, Is.EqualTo(VegetationKind.Fern));

                Assert.That(growth.HeroDrawCallCount, Is.EqualTo(3),
                    "Hero presentation is budgeted as ivy, petals, and flower centres only.");
                Assert.That(growth.HeroVertexCount, Is.GreaterThan(1500));
                Assert.That(growth.HeroVertexCount, Is.LessThanOrEqualTo(4096),
                    "The close-up art-directed mesh must stay inside its one-time 4k-vertex budget.");

                Mesh ivy = growth.HeroIvyMesh;
                Mesh petals = growth.HeroFlowerPetalMesh;
                Assert.That(ivy, Is.Not.Null);
                Assert.That(petals, Is.Not.Null);
                Assert.That(ivy.bounds.size.x, Is.GreaterThan(3.5f),
                    "Ivy must keep the sparse right-hand counterweight as well as the dense left mass.");
                Assert.That(ivy.bounds.size.y, Is.GreaterThan(7.0f),
                    "Ivy must climb from the lower pier across the crown.");
                Assert.That(petals.bounds.max.y, Is.GreaterThan(7.8f),
                    "Flowers must reach the upper crown where the reference is most lush.");

                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Has.Length.EqualTo(3),
                    "The hero mesh must not introduce per-leaf or per-flower GameObjects/draws.");

                growth.enabled = false;
                Assert.That(growth.InstanceCount, Is.Zero,
                    "Disabling the authoring component must release semantic and hero growth.");
                growth.enabled = true;
                yield return null;
                Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
                Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
                Assert.That(growth.SemanticInstanceCount, Is.EqualTo(2),
                    "Re-enabling after a scene lifecycle transition must restore the bounded presentation.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
