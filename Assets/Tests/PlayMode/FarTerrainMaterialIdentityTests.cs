using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FarTerrainMaterialIdentityTests
    {
        private const string ScenePath = "Assets/Scenes/VoxelShowcase.unity";

        private static IEnumerator LoadShowcase()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static List<Mesh> RingMeshes(VoxelFarTerrain far) =>
            (List<Mesh>)typeof(VoxelFarTerrain)
                .GetField("_ringMeshes", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(far);

        private static object MaterialRoles(VoxelFarTerrain far)
        {
            PropertyInfo property = typeof(VoxelFarTerrain).GetProperty(
                "MaterialRoles", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "VoxelFarTerrain.MaterialRoles must be available.");
            object roles = property.GetValue(far);
            Assert.That(roles, Is.Not.Null, "VoxelFarTerrain.MaterialRoles must be initialized.");
            return roles;
        }

        private static int ReadRole(object roles, string name)
        {
            Type type = roles.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return Convert.ToInt32(property.GetValue(roles));

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{name} must be available.");
            return Convert.ToInt32(field.GetValue(roles));
        }

        private static int SurfaceAt(object roles, int height)
        {
            MethodInfo method = roles.GetType().GetMethod(
                "SurfaceAt", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int) }, null);
            Assert.That(method, Is.Not.Null, "Material role set must expose SurfaceAt(int, int).");
            return Convert.ToInt32(method.Invoke(
                roles, new object[] { height, ShowcaseWorld.BaseHeightVoxels }));
        }

        [UnityTest]
        public IEnumerator FarTerrainCarriesAuthoritativeMaterialIds()
        {
            yield return LoadShowcase();

            var far = UnityEngine.Object.FindFirstObjectByType<VoxelFarTerrain>();
            Assert.That(far, Is.Not.Null, "VoxelShowcase did not create VoxelFarTerrain.");

            for (int frame = 0; frame < 600 && !far.HasSampledHeightsForEveryRing; frame++)
                yield return null;

            Assert.That(far.HasSampledHeightsForEveryRing, Is.True,
                "Far-terrain rings never finished publishing sampled heights.");

            List<Mesh> meshes = RingMeshes(far);
            Assert.That(meshes, Is.Not.Null.And.Not.Empty,
                "No generated far-terrain ring meshes were available to inspect.");

            object roles = MaterialRoles(far);
            int farStructure = ReadRole(roles, "FarStructure");
            int checkedTerrainVertices = 0;

            foreach (Mesh mesh in meshes)
            {
                if (mesh == null || mesh.vertexCount == 0) continue;

                Vector3[] vertices = mesh.vertices;
                Vector2[] materialIds = mesh.uv2;
                Assert.That(materialIds.Length, Is.EqualTo(vertices.Length),
                    $"{mesh.name} must carry one authoritative material id in UV2 per vertex.");

                for (int i = 0; i < vertices.Length; i++)
                {
                    int actual = Mathf.RoundToInt(materialIds[i].x);
                    if (actual == farStructure) continue;

                    int height = Mathf.RoundToInt(vertices[i].y / ShowcaseWorld.VoxelSize);
                    int expected = SurfaceAt(roles, height);

                    Assert.That(actual, Is.EqualTo(expected),
                        $"{mesh.name} vertex {i} at {vertices[i]} carries material id {actual}, "
                      + $"but the shared surface-role contract requires {expected}.");
                    checkedTerrainVertices++;
                }
            }

            Assert.That(checkedTerrainVertices, Is.GreaterThan(0),
                "No non-structure far-terrain vertices were checked.");
        }
    }
}
