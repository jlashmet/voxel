using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Showcase;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Loads the real Showcase and captures one semantic tree with all production voxel render
    /// features active. This is intentionally the first test that includes the legacy migration,
    /// proxy cleanup, Surface Nets/Transvoxel ownership, and ProceduralTreeRenderer together.
    /// </summary>
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

            AsyncOperation load = SceneManager.LoadSceneAsync("VoxelShowcase", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "VoxelShowcase must be available to the CI PlayMode run.");
            while (!load.isDone) yield return null;

            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (ProceduralTreeRegistry.Instances.Count == 0
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(ProceduralTreeRegistry.Instances.Count, Is.GreaterThan(0),
                        "Showcase never published semantic tree instances.");

            // Wait for the cleanup's explicit completion flag rather than trying to rediscover
            // hidden DontSave migration components from the Test Runner. Production code owns the
            // lifecycle invariant; the test only observes that the rewrite completed.
            while (!ShowcaseTreePopulation.Completed
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool cleanupComplete = ShowcaseTreePopulation.Completed;

            List<ProceduralTreeRenderer> renderers = FindRuntimeRenderers();
            while (renderers.Count == 1
                   && renderers[0].transform.childCount < ProceduralTreeRegistry.Instances.Count
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                renderers = FindRuntimeRenderers();
            }

            Assert.That(renderers.Count, Is.EqualTo(1),
                        "Showcase must have exactly one ProceduralTreeRenderer singleton.");
            ProceduralTreeRenderer treeRenderer = renderers[0];

            // Give fallback ownership and LOD selection a short time to settle. We still record
            // startup damage/rotation below, so an accidentally severed tree cannot hide from this.
            for (int i = 0; i < 60; i++) yield return null;

            int instanceCount = ProceduralTreeRegistry.Instances.Count;
            int damageCount = ProceduralTreeRegistry.Damage.Count;
            int presentationRoots = treeRenderer.transform.childCount;
            int severedCount = 0;
            int foliageDamagedCount = 0;
            int rotatedRootCount = 0;
            int activeRootCount = 0;
            int selectedTreeIndex = -1;

            int inspectCount = Mathf.Min(instanceCount, Mathf.Min(damageCount, presentationRoots));
            for (int i = 0; i < inspectCount; i++)
            {
                ProceduralTreeRegistry.TreeDamageState damage = ProceduralTreeRegistry.Damage[i];
                if (damage.Severed) severedCount++;
                if (damage.FoliageHealth < 0.999f) foliageDamagedCount++;

                Transform root = treeRenderer.transform.GetChild(i);
                if (Quaternion.Angle(root.localRotation, Quaternion.identity) > 1f)
                    rotatedRootCount++;
                if (root.gameObject.activeSelf)
                {
                    activeRootCount++;
                    if (selectedTreeIndex < 0) selectedTreeIndex = i;
                }
            }

            // Prefer a tree currently owned by the semantic renderer. If fallback ownership keeps
            // every semantic root hidden, still capture tree zero so the artifact documents it.
            if (selectedTreeIndex < 0 && presentationRoots > 0) selectedTreeIndex = 0;

            Transform selectedRoot = selectedTreeIndex >= 0
                ? treeRenderer.transform.GetChild(selectedTreeIndex)
                : null;
            Bounds selectedBounds = default;
            bool hasBounds = false;
            if (selectedRoot != null)
            {
                MeshRenderer[] meshRenderers = selectedRoot.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    if (!hasBounds)
                    {
                        selectedBounds = meshRenderers[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        selectedBounds.Encapsulate(meshRenderers[i].bounds);
                    }
                }
            }

            if (selectedRoot != null && hasBounds)
            {
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

                // Let the render feature observe this camera and update chunk ownership before the
                // explicit capture. This is important for the Surface Nets -> Transvoxel handoff.
                yield return null;
                yield return null;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Showcase Tree Capture",
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
                    File.WriteAllBytes(Path.Combine(outputDirectory, "showcase-tree.png"),
                                       capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                }
            }

            string selectedSpecies = "none";
            Vector3 selectedPosition = Vector3.zero;
            Vector3 selectedRotation = Vector3.zero;
            bool selectedActive = false;
            bool selectedSevered = false;
            float selectedFoliageHealth = 1f;
            if (selectedTreeIndex >= 0 && selectedTreeIndex < instanceCount)
            {
                TreeInstance instance = ProceduralTreeRegistry.Instances[selectedTreeIndex];
                selectedSpecies = instance.Species.ToString();
                selectedPosition = (Vector3)instance.PositionMetres;
                if (selectedRoot != null)
                {
                    selectedRotation = selectedRoot.localEulerAngles;
                    selectedActive = selectedRoot.gameObject.activeSelf;
                }
                if (selectedTreeIndex < damageCount)
                {
                    selectedSevered = ProceduralTreeRegistry.Damage[selectedTreeIndex].Severed;
                    selectedFoliageHealth = ProceduralTreeRegistry.Damage[selectedTreeIndex].FoliageHealth;
                }
            }

            string metadata =
                $"cleanupComplete={cleanupComplete}\n" +
                $"registryInstances={instanceCount}\n" +
                $"damageStates={damageCount}\n" +
                $"rendererInstances={renderers.Count}\n" +
                $"presentationRoots={presentationRoots}\n" +
                $"activeRoots={activeRootCount}\n" +
                $"severedAtStartup={severedCount}\n" +
                $"foliageDamagedAtStartup={foliageDamagedCount}\n" +
                $"rotatedRootsAtStartup={rotatedRootCount}\n" +
                $"selectedTreeIndex={selectedTreeIndex}\n" +
                $"selectedSpecies={selectedSpecies}\n" +
                $"selectedPosition={selectedPosition:F3}\n" +
                $"selectedRotation={selectedRotation:F3}\n" +
                $"selectedActive={selectedActive}\n" +
                $"selectedSevered={selectedSevered}\n" +
                $"selectedFoliageHealth={selectedFoliageHealth:F3}\n" +
                $"selectedBoundsCenter={(hasBounds ? selectedBounds.center : Vector3.zero):F3}\n" +
                $"selectedBoundsSize={(hasBounds ? selectedBounds.size : Vector3.zero):F3}\n";
            File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree.txt"), metadata);
            Debug.Log($"CI showcase-tree capture written to {outputDirectory}\n{metadata}");

            Assert.That(cleanupComplete, Is.True,
                        "Semantic Showcase tree population did not complete before the visual checkpoint.");
            Assert.That(damageCount, Is.EqualTo(instanceCount),
                        "Every semantic tree must have exactly one damage state.");
            Assert.That(presentationRoots, Is.EqualTo(instanceCount),
                        "Every semantic tree must have exactly one presentation root.");
            Assert.That(severedCount, Is.EqualTo(0),
                        "No Showcase tree may begin play already severed/falling.");
            Assert.That(foliageDamagedCount, Is.EqualTo(0),
                        "No Showcase tree may begin play with foliage damage.");
            Assert.That(rotatedRootCount, Is.EqualTo(0),
                        "No Showcase tree presentation may begin rotated/fallen.");
            Assert.That(selectedRoot, Is.Not.Null, "No semantic Showcase tree was available to capture.");
            Assert.That(hasBounds, Is.True, "Selected semantic Showcase tree had no render bounds.");
            Assert.That(File.Exists(Path.Combine(outputDirectory, "showcase-tree.png")), Is.True,
                        "Showcase tree PNG was not produced.");

            if (capture != null) Object.Destroy(capture);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
            if (cameraObject != null) Object.Destroy(cameraObject);
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
