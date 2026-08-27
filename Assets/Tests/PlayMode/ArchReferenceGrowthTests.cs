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
            var host = new GameObject("Arch reference growth regression");
            try
            {
                // Reproduce the real ownership condition from the captured Hero Arch pose: the
                // lookdev/growth host is the movable camera, while hero foliage coordinates are
                // authored in the arch's world-space metre frame. Install the production lifecycle
                // anchor before growth, then prove no test-only manual repair is needed.
                host.transform.SetPositionAndRotation(
                    new Vector3(-0.85728186f, 8.398123f, -9.309617f),
                    new Quaternion(0.09724782f, -0.01389580f, 0.00135791f, 0.9951624f));
                Camera camera = host.AddComponent<Camera>();
                ArchReferenceGrowthWorldSpace.EnsureInstalled(camera);
                ArchReferenceGrowth growth = host.AddComponent<ArchReferenceGrowth>();
                yield return null;

                Transform heroRoot = FindHeroRoot();
                Assert.That(heroRoot, Is.Not.Null,
                    "The authored hero root must exist after ArchReferenceGrowth enables.");
                Assert.That(host.transform.Find("Arch Reference Hero Growth"), Is.Null,
                    "Production lifecycle anchoring must detach the hero root without a manual test repair.");
                Assert.That(heroRoot.parent, Is.Null,
                    "Hero foliage must be detached from the movable Hero Arch Camera before rendering.");
                Assert.That(heroRoot.position.sqrMagnitude, Is.LessThan(0.000001f),
                    "Authored ivy/flower coordinates are world-space arch coordinates and require a world-identity root.");
                Assert.That(Quaternion.Angle(heroRoot.rotation, Quaternion.identity), Is.LessThan(0.01f));
                Assert.That((heroRoot.localScale - Vector3.one).sqrMagnitude, Is.LessThan(0.000001f));

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

                MeshRenderer[] renderers = heroRoot.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Has.Length.EqualTo(3),
                    "The hero mesh must not introduce per-leaf or per-flower GameObjects/draws.");

                growth.enabled = false;
                Assert.That(growth.InstanceCount, Is.Zero,
                    "Disabling the authoring component must release semantic and hero growth.");
                yield return null;

                growth.enabled = true;
                yield return null;
                Transform restoredHeroRoot = FindHeroRoot();
                Assert.That(restoredHeroRoot, Is.Not.Null);
                Assert.That(host.transform.Find("Arch Reference Hero Growth"), Is.Null,
                    "Re-enabled growth must be re-anchored automatically by the lifecycle listener.");
                Assert.That(restoredHeroRoot.parent, Is.Null);
                Assert.That(restoredHeroRoot.position.sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(growth.HeroLeafCount, Is.EqualTo(128));
                Assert.That(growth.HeroFlowerHeadCount, Is.EqualTo(30));
                Assert.That(growth.SemanticInstanceCount, Is.EqualTo(2),
                    "Re-enabling after a scene lifecycle transition must restore the bounded presentation.");

                growth.enabled = false;
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Transform FindHeroRoot()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Transform candidate in transforms)
                if (candidate != null && candidate.name == "Arch Reference Hero Growth")
                    return candidate;
            return null;
        }
    }
}
