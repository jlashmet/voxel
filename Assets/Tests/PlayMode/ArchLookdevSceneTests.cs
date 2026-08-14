using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter ArchLookdevSceneTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class ArchLookdevSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SceneBuildsHeroThroughProductionSurfacePath()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/ArchLookdev.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("ArchLookdev", LoadSceneMode.Single);
#endif
            yield return null;
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            Assert.NotNull(lookdev);
            Assert.True(VoxelRenderBridge.TryGetWorld(out var world));
            Assert.NotNull(world.ProfileBlocks);
            Assert.Greater(world.ProfileBlocks.Count, 0);

            Camera camera = lookdev.GetComponent<Camera>();
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            try
            {
                int stableFrames = 0;
                for (int frame = 0; frame < 240; frame++)
                {
                    VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                    bool converged = metrics.SolidKnownChunks > 0
                        && metrics.SolidDirtyChunks == 0
                        && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                    stableFrames = converged ? stableFrames + 1 : 0;
                    if (stableFrames >= 3)
                    {
                        camera.Render();
                        RenderTexture previousActive = RenderTexture.active;
                        RenderTexture.active = target;
                        var capture = new Texture2D(target.width, target.height,
                                                    TextureFormat.RGB24, false);
                        capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        capture.Apply(false, false);
                        RenderTexture.active = previousActive;
                        string path = Path.GetFullPath("Artifacts/ArchStudy/arch-runtime-test.png");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllBytes(path, capture.EncodeToPNG());
                        Object.Destroy(capture);
                        yield break;
                    }
                    yield return null;
                }
                VoxelSurfaceMetrics finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                Assert.Fail($"Arch lookdev production surface did not converge: "
                          + $"known={finalMetrics.SolidKnownChunks}, "
                          + $"resident={finalMetrics.SolidResidentChunks}, "
                          + $"dirty={finalMetrics.SolidDirtyChunks}.");
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
