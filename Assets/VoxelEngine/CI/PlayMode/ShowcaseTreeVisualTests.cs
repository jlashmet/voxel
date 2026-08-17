using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Runtime;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Showcase;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Loads the real Showcase and proves semantic vegetation is upright, healthy, data-first and
    /// visibly rendered. Uprightness is checked from the generated trunk geometry rather than from
    /// GameObject rotation, because healthy batched trees intentionally have no per-tree GameObject.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter ShowcaseTreeVisualTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class ShowcaseTreeVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float StartupTimeoutSeconds = 30f;

        [UnityTest]
        public IEnumerator ShowcaseStartup_HasOneUprightUndamagedSemanticTreePresentation()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "ShowcaseTree");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D capture = null;
            Texture2D noVegetationCapture = null;

            // Load by path, not by name: VoxelShowcase is deliberately not in the build profile
            // (KentridgePlayableSlice is the launch scene), and LoadSceneAsync by name resolves
            // only against that list. Every other showcase test loads this scene the same way.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
#else
            AsyncOperation load = SceneManager.LoadSceneAsync("VoxelShowcase", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "VoxelShowcase must be available to the CI PlayMode run.");
            while (!load.isDone) yield return null;
#endif

            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while ((!ShowcaseTreePopulation.Completed || TreeWorldRuntime.Instances.Count == 0)
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(ShowcaseTreePopulation.Completed, Is.True,
                        "Semantic Showcase tree population did not complete.");
            Assert.That(TreeWorldRuntime.Instances.Count, Is.GreaterThan(0),
                        "Showcase never published semantic tree instances.");

            List<ProceduralTreeRenderer> renderers = FindRuntimeRenderers();
            while ((renderers.Count != 1
                    || renderers[0].PresentationCount < TreeWorldRuntime.Instances.Count)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                renderers = FindRuntimeRenderers();
            }

            Assert.That(renderers.Count, Is.EqualTo(1),
                        "Showcase must have exactly one ProceduralTreeRenderer singleton.");
            ProceduralTreeRenderer treeRenderer = renderers[0];
            for (int i = 0; i < 30; i++) yield return null;

            int instanceCount = TreeWorldRuntime.Instances.Count;
            int damageCount = TreeWorldRuntime.Damage.Count;
            int severedCount = 0;
            int foliageDamagedCount = 0;
            int sidewaysSkeletonCount = 0;
            int rotatedDynamicRootCount = 0;
            int selectedTreeIndex = instanceCount > 0 ? 0 : -1;

            for (int i = 0; i < instanceCount; i++)
            {
                if (i < damageCount)
                {
                    TreeDamageState damage = TreeWorldRuntime.Damage[i];
                    if (damage.Severed) severedCount++;
                    if (damage.FoliageHealth < 0.999f) foliageDamagedCount++;
                }

                TreeInstance instance = TreeWorldRuntime.Instances[i];
                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                if (!HasUprightTrunk(skeleton)) sidewaysSkeletonCount++;

                if (treeRenderer.TryGetDynamicPresentationRoot(i, out Transform root)
                    && Quaternion.Angle(root.localRotation, Quaternion.identity) > 1f)
                    rotatedDynamicRootCount++;
            }

            Assert.That(treeRenderer.TryGetTreeBounds(selectedTreeIndex, out Bounds selectedBounds), Is.True,
                        "Selected semantic Showcase tree had no computable bounds.");
            Assert.That(selectedBounds.size.sqrMagnitude, Is.GreaterThan(0.01f));

            cameraObject = new GameObject("CI Showcase Tree Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            Vector3 focus = selectedBounds.center;
            float radius = Mathf.Max(selectedBounds.extents.magnitude, 2f);
            Vector3 viewDirection = new Vector3(0.85f, 0.18f, -1f).normalized;
            cameraObject.transform.position = focus + viewDirection * (radius * 2.65f);
            cameraObject.transform.LookAt(focus + Vector3.up * (selectedBounds.extents.y * 0.04f));
            yield return null;
            yield return null;

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "CI Showcase Tree Capture",
                antiAliasing = 4,
            };
            target.Create();
            camera.targetTexture = target;

            MeshRenderer[] vegetationRenderers =
                treeRenderer.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(vegetationRenderers.Length, Is.GreaterThan(0));
            bool[] enabled = new bool[vegetationRenderers.Length];
            int renderChangedPixels;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                capture.Apply(false, false);
                File.WriteAllBytes(Path.Combine(outputDirectory, "showcase-tree.png"), capture.EncodeToPNG());

                for (int i = 0; i < vegetationRenderers.Length; i++)
                {
                    enabled[i] = vegetationRenderers[i].enabled;
                    vegetationRenderers[i].enabled = false;
                }

                camera.Render();
                RenderTexture.active = target;
                noVegetationCapture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                noVegetationCapture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                noVegetationCapture.Apply(false, false);
                renderChangedPixels = CountChangedPixels(capture, noVegetationCapture, 8);
            }
            finally
            {
                for (int i = 0; i < vegetationRenderers.Length; i++)
                    if (vegetationRenderers[i] != null)
                        vegetationRenderers[i].enabled = enabled[i];
                RenderTexture.active = previous;
                camera.targetTexture = null;
            }

            TreeInstance selected = TreeWorldRuntime.Instances[selectedTreeIndex];
            int expectedDynamic = instanceCount - treeRenderer.BatchedTreeCount;
            string metadata =
                $"populationComplete={ShowcaseTreePopulation.Completed}\n" +
                $"registryInstances={instanceCount}\n" +
                $"damageStates={damageCount}\n" +
                $"rendererInstances={renderers.Count}\n" +
                $"semanticPresentations={treeRenderer.PresentationCount}\n" +
                $"rendererChildren={treeRenderer.transform.childCount}\n" +
                $"treeBatches={treeRenderer.BatchCount}\n" +
                $"batchedTrees={treeRenderer.BatchedTreeCount}\n" +
                $"dynamicPresentations={treeRenderer.DynamicPresentationCount}\n" +
                $"dynamicMeshes={treeRenderer.DynamicMeshCount}\n" +
                $"residentRenderObjects={treeRenderer.ResidentRenderObjectCount}\n" +
                $"estimatedVisibleTreeDraws={treeRenderer.EstimatedVisibleDrawCount}\n" +
                $"severedAtStartup={severedCount}\n" +
                $"foliageDamagedAtStartup={foliageDamagedCount}\n" +
                $"sidewaysSkeletonsAtStartup={sidewaysSkeletonCount}\n" +
                $"rotatedDynamicRootsAtStartup={rotatedDynamicRootCount}\n" +
                $"selectedTreeIndex={selectedTreeIndex}\n" +
                $"selectedSpecies={selected.Species}\n" +
                $"selectedPosition={(Vector3)selected.PositionMetres:F3}\n" +
                $"selectedBoundsCenter={selectedBounds.center:F3}\n" +
                $"selectedBoundsSize={selectedBounds.size:F3}\n" +
                $"renderChangedPixels={renderChangedPixels}\n";
            File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree.txt"), metadata);
            Debug.Log($"CI showcase-tree capture written to {outputDirectory}\n{metadata}");

            Assert.That(damageCount, Is.EqualTo(instanceCount));
            Assert.That(treeRenderer.PresentationCount, Is.EqualTo(instanceCount));
            Assert.That(treeRenderer.DynamicPresentationCount, Is.EqualTo(expectedDynamic),
                        "Only trees that cannot join a healthy spatial batch may own dynamic GameObjects.");
            Assert.That(treeRenderer.GeneratedMeshCount,
                        Is.EqualTo(treeRenderer.BatchMeshCount + treeRenderer.DynamicMeshCount));
            Assert.That(severedCount, Is.EqualTo(0));
            Assert.That(foliageDamagedCount, Is.EqualTo(0));
            Assert.That(sidewaysSkeletonCount, Is.EqualTo(0),
                        "At least one generated Showcase trunk is not actually Y-up; root-rotation checks alone cannot catch this.");
            Assert.That(rotatedDynamicRootCount, Is.EqualTo(0));
            Assert.That(renderChangedPixels, Is.GreaterThan(512),
                        "Disabling production vegetation renderers did not materially change the Showcase frame.");
            Assert.That(File.Exists(Path.Combine(outputDirectory, "showcase-tree.png")), Is.True);

            if (capture != null) Object.Destroy(capture);
            if (noVegetationCapture != null) Object.Destroy(noVegetationCapture);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
            if (cameraObject != null) Object.Destroy(cameraObject);
        }

        private static bool HasUprightTrunk(TreeSkeletonSnapshot skeleton)
        {
            float highestY = 0f;
            float maxHorizontal = 0f;
            int trunkSegments = 0;
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                if (branch.Level != 0) continue;
                trunkSegments++;
                float3 delta = branch.End - branch.Start;
                float3 direction = math.normalizesafe(delta, new float3(0f, 1f, 0f));
                if (direction.y < 0.72f) return false;
                highestY = math.max(highestY, math.max(branch.Start.y, branch.End.y));
                maxHorizontal = math.max(maxHorizontal,
                    math.length(new float2(branch.End.x, branch.End.z)));
            }

            if (trunkSegments == 0) return false;
            if (highestY < skeleton.Height * 0.80f) return false;
            return maxHorizontal < math.max(0.75f, highestY * 0.22f);
        }

        private static int CountChangedPixels(Texture2D withVegetation,
                                              Texture2D withoutVegetation,
                                              int channelThreshold)
        {
            Color32[] withPixels = withVegetation.GetPixels32();
            Color32[] withoutPixels = withoutVegetation.GetPixels32();
            Assert.That(withPixels.Length, Is.EqualTo(withoutPixels.Length));

            int changed = 0;
            for (int i = 0; i < withPixels.Length; i++)
            {
                Color32 a = withPixels[i];
                Color32 b = withoutPixels[i];
                int maxDelta = Mathf.Max(
                    Mathf.Abs(a.r - b.r),
                    Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
                if (maxDelta >= channelThreshold) changed++;
            }
            return changed;
        }

        private static List<ProceduralTreeRenderer> FindRuntimeRenderers()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            var result = new List<ProceduralTreeRenderer>(all.Length);
            foreach (ProceduralTreeRenderer renderer in all)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (!renderer.gameObject.scene.IsValid()) continue;
                result.Add(renderer);
            }
            return result;
        }
    }
}
