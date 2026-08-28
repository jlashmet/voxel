using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Isolated grass lookdev harness. It intentionally bypasses terrain sampling and ecology by
    /// feeding known semantic Grass instances into the same production renderer used by showcases.
    /// </summary>
    public sealed class GrassLookdevTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InternalStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test, Timeout(30000)]
        public void KnownSemanticGrassInstancesReachProductionRendererEvenInFormerCoverageHoles()
        {
            Scene lookdev = SceneManager.CreateScene("GrassLookdev");
            var host = new GameObject("GrassLookdev Production Renderer");
            SceneManager.MoveGameObjectToScene(host, lookdev);
            ProceduralVegetationBatchRenderer renderer = host.AddComponent<ProceduralVegetationBatchRenderer>();

            try
            {
                object grassBatch = GetGrassBatch(renderer);
                MethodInfo legacyCoverage = grassBatch.GetType().GetMethod("CoverageField", InternalStatic);
                Assert.That(legacyCoverage, Is.Not.Null,
                    "The former macro coverage field remains only as a regression oracle for this test.");

                List<VegetationInstance> semantic = FindFormerCoverageHoles(legacyCoverage, 12);
                Assert.That(semantic.Count, Is.EqualTo(12),
                    "The lookdev grid must contain enough locations that the old renderer would have rejected.");

                renderer.SetInstances(semantic);
                grassBatch = GetGrassBatch(renderer);
                int bladeCount = GetIntProperty(grassBatch, "BladeCount");
                IList meshes = GetGrassMeshes(grassBatch);

                Assert.That(renderer.InstanceCount, Is.EqualTo(semantic.Count),
                    "All known semantic Grass instances must remain authoritative at the renderer boundary.");
                Assert.That(bladeCount, Is.InRange(semantic.Count * 5, semantic.Count * 15),
                    "Presentation may vary local blade density by seed, but it must not discard a semantic Grass placement.");
                Assert.That(meshes.Count, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
                if (lookdev.IsValid() && lookdev.isLoaded)
                    SceneManager.UnloadSceneAsync(lookdev);
            }
        }

        [Test]
        public void SeedControlsOnlyDeterministicLocalBladeDensity()
        {
            var host = new GameObject("GrassLookdev Seed Density");
            ProceduralVegetationBatchRenderer renderer = host.AddComponent<ProceduralVegetationBatchRenderer>();

            try
            {
                object grassBatch = GetGrassBatch(renderer);
                MethodInfo bladeCountForSeed = grassBatch.GetType().GetMethod("BladeCountForSeed", InternalStatic);
                Assert.That(bladeCountForSeed, Is.Not.Null);

                var counts = new HashSet<int>();
                for (uint seed = 1; seed <= 64; seed++)
                {
                    int first = (int)bladeCountForSeed.Invoke(null, new object[] { seed });
                    int second = (int)bladeCountForSeed.Invoke(null, new object[] { seed });
                    Assert.That(second, Is.EqualTo(first), $"seed {seed}");
                    Assert.That(first, Is.InRange(5, 15), $"seed {seed}");
                    counts.Add(first);
                }

                Assert.That(counts.Count, Is.GreaterThan(3),
                    "Seeded presentation should retain useful local density variation without becoming an ecology decision.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static List<VegetationInstance> FindFormerCoverageHoles(MethodInfo legacyCoverage, int count)
        {
            var instances = new List<VegetationInstance>(count);
            uint seed = 7001;
            for (int z = -96; z <= 96 && instances.Count < count; z += 3)
            for (int x = -96; x <= 96 && instances.Count < count; x += 3)
            {
                float coverage = (float)legacyCoverage.Invoke(null, new object[] { (float)x, (float)z });
                if (coverage >= 0.20f) continue;

                instances.Add(new VegetationInstance
                {
                    PositionMetres = new float3(x, 0f, z),
                    SurfaceNormal = new float3(0f, 1f, 0f),
                    Kind = VegetationKind.Grass,
                    Seed = seed++,
                    Scale = 1f,
                });
            }
            return instances;
        }

        private static object GetGrassBatch(ProceduralVegetationBatchRenderer renderer)
        {
            FieldInfo field = typeof(ProceduralVegetationBatchRenderer).GetField("_grass", PrivateInstance);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(renderer);
        }

        private static IList GetGrassMeshes(object grassBatch)
        {
            FieldInfo field = grassBatch.GetType().GetField("_meshes", PrivateInstance);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(grassBatch);
        }

        private static int GetIntProperty(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, PrivateInstance);
            Assert.That(property, Is.Not.Null, name);
            return (int)property.GetValue(instance);
        }
    }
}
