using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Temporary bare-bones reproduction for the ArchLookdev semantic-growth issue. Remove once
    /// the shared vegetation draw path has been diagnosed and the arch regression is pinned.
    /// </summary>
    [NUnit.Framework.Explicit("Temporary vegetation framebuffer reproduction; run by name.")]
    public sealed class VegetationRenderingReproTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator EnablingBatchRendererChangesFramebuffer()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VegetationRenderingShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("VegetationRenderingShowcase", LoadSceneMode.Single);
#endif
            yield return null;
            yield return null;

            VegetationRenderingShowcase showcase =
                Object.FindAnyObjectByType<VegetationRenderingShowcase>();
            Assert.NotNull(showcase);
            Assert.NotNull(showcase.Renderer);
            Assert.Greater(showcase.Renderer.InstanceCount, 0);

            Camera camera = Camera.main;
            Assert.NotNull(camera);
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            var baseline = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var withVegetation = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                target.Create();
                camera.targetTexture = target;

                showcase.Renderer.enabled = false;
                yield return null;
                yield return new WaitForEndOfFrame();
                Read(target, baseline);

                showcase.Renderer.enabled = true;
                yield return null;
                yield return new WaitForEndOfFrame();
                Read(target, withVegetation);

                Color32[] before = baseline.GetPixels32();
                Color32[] after = withVegetation.GetPixels32();
                int changed = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    int difference = Mathf.Abs(before[i].r - after[i].r)
                                   + Mathf.Abs(before[i].g - after[i].g)
                                   + Mathf.Abs(before[i].b - after[i].b);
                    if (difference >= 18) changed++;
                }

                Assert.Greater(changed, 250,
                    $"Vegetation renderer had {showcase.Renderer.InstanceCount} semantic instances "
                  + $"but enabling it changed only {changed} framebuffer pixels.");
            }
            finally
            {
                showcase.Renderer.enabled = true;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(baseline);
                Object.Destroy(withVegetation);
            }
        }

        private static void Read(RenderTexture target, Texture2D destination)
        {
            RenderTexture.active = target;
            destination.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            destination.Apply(false, false);
        }
    }
}
