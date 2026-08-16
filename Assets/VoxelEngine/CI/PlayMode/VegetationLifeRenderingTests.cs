using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Core.AmbientLife;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.AmbientLife;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    /// <summary>
    /// PlayMode contract for lightweight vegetation rendering. The standalone showcase must cover
    /// every semantic kind, use the production instanced renderer, and remain data-oriented rather
    /// than materializing a GameObject for every plant.
    /// </summary>
    public sealed class VegetationRenderingTests
    {
        [UnityTest]
        public IEnumerator StandaloneShowcase_CoversCatalogue_AndDrawsThroughInstancedRenderer()
        {
            GameObject root = new GameObject("Vegetation Rendering Test");
            try
            {
                VegetationRenderingShowcase showcase =
                    root.AddComponent<VegetationRenderingShowcase>();
                yield return null;

                Assert.That(showcase.Renderer, Is.Not.Null);
                Assert.That(showcase.InstanceCount,
                            Is.EqualTo(VegetationCatalogue.Count
                                       * VegetationRenderingShowcase.InstancesPerKind));
                Assert.That(showcase.Renderer.InstanceCount, Is.EqualTo(showcase.InstanceCount));

                var represented = new HashSet<VegetationKind>();
                for (int i = 0; i < showcase.Instances.Count; i++)
                    represented.Add(showcase.Instances[i].Kind);
                Assert.That(represented.Count, Is.EqualTo(VegetationCatalogue.Count),
                            "The showcase must visibly exercise every vegetation catalogue kind.");

                Assert.That(ProceduralVegetationMaterials.Ensure(), Is.True,
                            "Vegetation shaders must resolve in PlayMode/player-compatible imports.");
                for (int i = 0; i < VegetationCatalogue.Count; i++)
                {
                    VegetationKind kind = VegetationCatalogue.KindAt(i);
                    Assert.That(ProceduralVegetationMaterials.MaterialFor(kind), Is.Not.Null,
                                $"No runtime material resolved for {kind}.");
                }

                // DrawNow exercises the exact production submission path. One additional frame also
                // exercises LateUpdate, which is how the renderer is used in a real scene.
                showcase.Renderer.DrawNow();
                yield return null;

                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.LessThan(10),
                            "Vegetation showcase materialized per-instance GameObjects instead of batching.");

                showcase.Renderer.Clear();
                Assert.That(showcase.Renderer.InstanceCount, Is.Zero);
                showcase.Rebuild();
                Assert.That(showcase.Renderer.InstanceCount, Is.EqualTo(showcase.InstanceCount),
                            "Renderer failed to repopulate after a clear/rebuild cycle.");
            }
            finally
            {
                Object.Destroy(root);
            }

            yield return null;
        }
    }

    /// <summary>
    /// PlayMode contract for ambient-life rendering. Clusters remain the semantic/authoritative
    /// representation while the renderer reconstructs the expected number of local visual agents.
    /// </summary>
    public sealed class AmbientLifeRenderingTests
    {
        [UnityTest]
        public IEnumerator StandaloneShowcase_CoversCatalogue_AndReconstructsAllAgents()
        {
            GameObject root = new GameObject("Ambient Life Rendering Test");
            try
            {
                AmbientLifeRenderingShowcase showcase =
                    root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;

                Assert.That(showcase.Renderer, Is.Not.Null);
                Assert.That(showcase.ClusterCount, Is.EqualTo(AmbientLifeCatalogue.Count));

                var represented = new HashSet<AmbientLifeKind>();
                int expectedAgents = 0;
                for (int i = 0; i < showcase.Clusters.Count; i++)
                {
                    represented.Add(showcase.Clusters[i].Kind);
                    expectedAgents += showcase.Clusters[i].Count;
                }

                Assert.That(represented.Count, Is.EqualTo(AmbientLifeCatalogue.Count),
                            "The showcase must visibly exercise every ambient-life catalogue kind.");
                Assert.That(showcase.AgentCount, Is.EqualTo(expectedAgents),
                            "Renderer did not reconstruct the expected agents from semantic clusters.");

                Assert.That(ProceduralAmbientLifeMaterials.Ensure(), Is.True,
                            "Ambient-life shader must resolve in PlayMode/player-compatible imports.");
                Assert.That(ProceduralAmbientLifeMaterials.Shared, Is.Not.Null);

                showcase.Renderer.DrawNow();
                yield return null;

                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.LessThan(10),
                            "Ambient-life showcase materialized per-agent GameObjects instead of batching.");

                showcase.Renderer.Clear();
                Assert.That(showcase.Renderer.AgentCount, Is.Zero);
                showcase.Rebuild();
                Assert.That(showcase.AgentCount, Is.EqualTo(expectedAgents),
                            "Ambient-life renderer failed to repopulate after clear/rebuild.");
            }
            finally
            {
                Object.Destroy(root);
            }

            yield return null;
        }
    }
}
