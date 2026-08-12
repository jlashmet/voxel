using System;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Minimal deterministic visual smoke test for procedural vegetation.
    ///
    /// This deliberately does not load Showcase, the voxel world, Surface Nets, Transvoxel,
    /// migration components, or the runtime ProceduralTreeRenderer. It generates exactly one
    /// semantic tree, renders its LOD0 mesh with the production tree shaders, and writes a PNG.
    /// That makes a bad orientation/mesh/shader result attributable to the tree generator itself.
    /// </summary>
    public static class SingleTreeCapture
    {
        private const int Width = 1024;
        private const int Height = 1024;

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "SingleTree");
            Directory.CreateDirectory(outputDirectory);

            GameObject treeObject = null;
            GameObject cameraObject = null;
            GameObject groundObject = null;
            Mesh mesh = null;
            Material barkMaterial = null;
            Material leafMaterial = null;
            Material groundMaterial = null;
            RenderTexture target = null;
            Texture2D capture = null;

            try
            {
                var instance = new TreeInstance
                {
                    PositionMetres = float3.zero,
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };

                ProceduralTreeSkeleton skeleton =
                    ProceduralTreeSkeletonBuilder.Generate(in instance);
                mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
                if (mesh == null || mesh.vertexCount == 0)
                    throw new InvalidOperationException("Procedural tree LOD0 produced no geometry.");
                if (mesh.subMeshCount != 2)
                    throw new InvalidOperationException($"Expected 2 tree submeshes, got {mesh.subMeshCount}.");

                Shader barkShader = Shader.Find("VoxelEngine/ProceduralTreeBark");
                Shader leafShader = Shader.Find("VoxelEngine/ProceduralTreeLeaves");
                if (barkShader == null)
                    throw new InvalidOperationException("ProceduralTreeBark shader was not found.");
                if (leafShader == null)
                    throw new InvalidOperationException("ProceduralTreeLeaves shader was not found.");

                barkMaterial = new Material(barkShader) { name = "CI Tree Bark" };
                leafMaterial = new Material(leafShader) { name = "CI Tree Leaves" };
                leafMaterial.SetFloat("_WindStrength", 0f);
                leafMaterial.SetFloat("_Damage", 0f);

                Vector4 sun = new(-0.48f, 0.76f, -0.44f, 0f);
                Color horizon = new(0.66f, 0.75f, 0.85f, 1f);
                Color zenith = new(0.24f, 0.45f, 0.76f, 1f);
                barkMaterial.SetVector("_SunDirection", sun);
                barkMaterial.SetColor("_SkyHorizon", horizon);
                barkMaterial.SetColor("_SkyZenith", zenith);
                leafMaterial.SetVector("_SunDirection", sun);
                leafMaterial.SetColor("_SkyHorizon", horizon);
                leafMaterial.SetColor("_SkyZenith", zenith);

                treeObject = new GameObject("CI Single Procedural Tree");
                treeObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var filter = treeObject.AddComponent<MeshFilter>();
                var renderer = treeObject.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { barkMaterial, leafMaterial };

                // A flat reference plane makes a 90-degree tree orientation immediately obvious.
                groundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                groundObject.name = "CI Ground Reference";
                groundObject.transform.position = new Vector3(0f, -0.025f, 0f);
                groundObject.transform.localScale = Vector3.one * 4f;
                Shader groundShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (groundShader != null)
                {
                    groundMaterial = new Material(groundShader) { name = "CI Ground" };
                    groundMaterial.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.20f, 1f));
                    groundObject.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                }

                Bounds bounds = mesh.bounds;
                Vector3 focus = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, skeleton.Height * 0.55f, 2f);

                cameraObject = new GameObject("CI Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                Vector3 viewDirection = new Vector3(0.78f, 0.20f, -1f).normalized;
                cameraObject.transform.position = focus + viewDirection * (radius * 3.05f);
                cameraObject.transform.LookAt(focus + Vector3.up * (bounds.extents.y * 0.06f));

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Single Tree Capture",
                    antiAliasing = 4,
                };
                target.Create();
                camera.targetTexture = target;

                RenderTexture previous = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = target;
                    capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    capture.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outputDirectory, "single-tree.png"),
                                       capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }

                int barkIndices = (int)mesh.GetIndexCount(0);
                int leafIndices = (int)mesh.GetIndexCount(1);
                string metadata =
                    $"species={instance.Species}\n" +
                    $"seed={instance.Seed}\n" +
                    $"scale={instance.Scale:F3}\n" +
                    $"height={skeleton.Height:F3}\n" +
                    $"branches={skeleton.Branches.Count}\n" +
                    $"leaves={skeleton.Leaves.Count}\n" +
                    $"vertices={mesh.vertexCount}\n" +
                    $"barkTriangles={barkIndices / 3}\n" +
                    $"leafTriangles={leafIndices / 3}\n" +
                    $"boundsCenter={bounds.center:F3}\n" +
                    $"boundsSize={bounds.size:F3}\n" +
                    $"rootRotation={treeObject.transform.eulerAngles:F3}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "single-tree.txt"), metadata);

                Debug.Log($"CI single-tree capture written to {outputDirectory}\n{metadata}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (treeObject != null) UnityEngine.Object.DestroyImmediate(treeObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (groundObject != null) UnityEngine.Object.DestroyImmediate(groundObject);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (barkMaterial != null) UnityEngine.Object.DestroyImmediate(barkMaterial);
                if (leafMaterial != null) UnityEngine.Object.DestroyImmediate(leafMaterial);
                if (groundMaterial != null) UnityEngine.Object.DestroyImmediate(groundMaterial);
            }
        }
    }
}
