using System.Collections.Generic;
using MountingForce.WorldGen.Voxel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Editor-only bridge between the isolated Kentridge diagnostic capture and the production
    /// procedural-tree geometry introduced by world vegetation.
    ///
    /// The runtime capture intentionally owns only temporary voxel storage, so it does not publish
    /// a global TreeWorldState snapshot. This hook recognizes that one diagnostic camera and builds
    /// LOD1 tree meshes from the same semantic TreeInstance identities immediately before the first
    /// camera render. Gameplay/runtime vegetation remains owned by TreeWorldState and
    /// ProceduralTreeRenderer; this class is visualization plumbing only.
    /// </summary>
    [InitializeOnLoad]
    internal static class KentridgeVegetationCaptureHook
    {
        private const uint Seed = 0x4B454E54u;
        private const string CameraObjectName = "CI Kentridge Runtime Camera";
        private const int DiagnosticLod = 1;

        private static readonly List<GameObject> s_Roots = new();
        private static readonly List<Mesh> s_Meshes = new();
        private static bool s_Built;

        static KentridgeVegetationCaptureHook()
        {
            Camera.onPreCull += BuildForCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera) =>
            BuildForCamera(camera);

        private static void BuildForCamera(Camera camera)
        {
            if (s_Built || camera == null || camera.gameObject.name != CameraObjectName) return;
            s_Built = true;

            if (!ProceduralTreeMaterials.Ensure())
            {
                Debug.LogWarning("Kentridge vegetation capture skipped: procedural tree shaders are unavailable.");
                return;
            }

            List<TreeInstance> instances = KentridgeVegetationPlanner.BuildAnalytic(Seed);
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, DiagnosticLod);
                mesh.name = $"CI Kentridge Tree {i:00} {instance.Species}";
                mesh.hideFlags = HideFlags.DontSave;
                s_Meshes.Add(mesh);

                var root = new GameObject(mesh.name)
                {
                    hideFlags = HideFlags.DontSave,
                };
                root.transform.position = (Vector3)instance.PositionMetres;
                MeshFilter filter = root.AddComponent<MeshFilter>();
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = ProceduralTreeMaterials.Shared;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                s_Roots.Add(root);
            }

            Debug.Log($"CI Kentridge vegetation: rendered {instances.Count} semantic procedural trees.");
        }

        private static void Cleanup()
        {
            for (int i = 0; i < s_Roots.Count; i++)
                if (s_Roots[i] != null) Object.DestroyImmediate(s_Roots[i]);
            for (int i = 0; i < s_Meshes.Count; i++)
                if (s_Meshes[i] != null) Object.DestroyImmediate(s_Meshes[i]);
            s_Roots.Clear();
            s_Meshes.Clear();
            s_Built = false;
        }
    }
}
