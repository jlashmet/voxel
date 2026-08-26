using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    [NUnit.Framework.Explicit("SceneIssue 20260825-032832 exact saved-camera visual replay.")]
    public sealed class ShowcaseSceneIssue032832ReplayTests
    {
        private const string CaptureId = "20260825-032832-253-VoxelShowcase";
        private const string ExpectedScene = "Assets/Scenes/VoxelShowcase.unity";
        private const int SettleFrames = 300;

        [UnityTest]
        public IEnumerator SavedFixtureIsConfiguredForExactReplay()
        {
            string issuePath = ResolveIssuePath();
            Assert.That(File.Exists(issuePath), Is.True,
                $"Saved scene-issue fixture {CaptureId} must remain available for exact replay.");

            SceneIssueFixture issue = JsonUtility.FromJson<SceneIssueFixture>(File.ReadAllText(issuePath));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.scenePath, Is.EqualTo(ExpectedScene));
            Assert.That(issue.captures, Is.Not.Null.And.Length.EqualTo(1));

            SceneIssueFrame frame = issue.captures[0];
            Assert.That(frame.screenWidth, Is.EqualTo(1364));
            Assert.That(frame.screenHeight, Is.EqualTo(836));
            Assert.That(frame.camera, Is.Not.Null);
            Assert.That(frame.camera.hierarchyPath, Is.EqualTo("Showcase Camera"));
            Assert.That(frame.camera.fieldOfView, Is.EqualTo(70f).Within(0.001f));
            Assert.That(frame.circles, Is.Not.Null.And.Length.EqualTo(3));

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ExpectedScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            VoxelShowcase showcase = null;
            Camera camera = null;
            for (int i = 0; i < 120 && (showcase == null || camera == null); i++)
            {
                showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                camera = ResolveCamera(frame.camera.hierarchyPath) ?? Camera.main;
                if (showcase == null || camera == null)
                    yield return null;
            }

            Assert.That(showcase, Is.Not.Null, "VoxelShowcase did not initialize for saved-view replay.");
            Assert.That(camera, Is.Not.Null, "Saved replay camera did not initialize.");

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(showcase, false);

            for (int i = 0; i < SettleFrames; i++)
            {
                ApplySavedCamera(camera, frame.camera);
                yield return null;
            }

            ApplySavedCamera(camera, frame.camera);
            if (Application.isBatchMode)
                yield return null;
            else
                yield return new WaitForEndOfFrame();
            ApplySavedCamera(camera, frame.camera);

            string artifactRoot = Path.Combine(ProjectRoot(), "Artifacts", "SingleTest", "SceneIssue032832");
            Directory.CreateDirectory(artifactRoot);
            string screenshotPath = Path.Combine(artifactRoot, "verification-exact-pose.png");
            CaptureView(camera, frame.screenWidth, frame.screenHeight, screenshotPath);

            var evidence =
                $"capture={CaptureId}\n" +
                $"scene={issue.scenePath}\n" +
                $"camera={frame.camera.hierarchyPath}\n" +
                $"position={frame.camera.position.x:R},{frame.camera.position.y:R},{frame.camera.position.z:R}\n" +
                $"rotation={frame.camera.rotation.x:R},{frame.camera.rotation.y:R},{frame.camera.rotation.z:R},{frame.camera.rotation.w:R}\n" +
                $"fov={frame.camera.fieldOfView:R}\n" +
                $"resolution={frame.screenWidth}x{frame.screenHeight}\n" +
                $"marked_regions={frame.circles.Length}\n" +
                $"settle_frames={SettleFrames}\n";
            File.WriteAllText(Path.Combine(artifactRoot, "verification-exact-pose.txt"), evidence);

            Assert.That(new FileInfo(screenshotPath).Length, Is.GreaterThan(4096),
                "Exact saved-camera replay must produce a non-empty rendered PNG artifact.");
            Debug.Log($"SCENEISSUE032832 exact-pose artifact: {screenshotPath}");
        }

        private static Camera ResolveCamera(string hierarchyPath)
        {
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (!candidate.gameObject.scene.IsValid())
                    continue;
                if (HierarchyPath(candidate.transform) == hierarchyPath)
                    return candidate;
            }
            return null;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private static void ApplySavedCamera(Camera camera, SceneIssueCamera saved)
        {
            camera.transform.SetPositionAndRotation(saved.position, saved.rotation);
            camera.fieldOfView = saved.fieldOfView;
            camera.orthographic = saved.orthographic;
            camera.orthographicSize = saved.orthographicSize;
            camera.nearClipPlane = saved.nearClipPlane;
            camera.farClipPlane = saved.farClipPlane;
        }

        private static void CaptureView(Camera camera, int width, int height, string outputPath)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ResolveIssuePath()
        {
            string openPath = Path.Combine(ProjectRoot(), "SceneIssues", "open", CaptureId, "issue.json");
            if (File.Exists(openPath))
                return openPath;
            return Path.Combine(ProjectRoot(), "SceneIssues", "closed", CaptureId, "issue.json");
        }

        private static string ProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve project root from Application.dataPath.");

        [Serializable]
        private sealed class SceneIssueFixture
        {
            public string scenePath;
            public SceneIssueFrame[] captures;
        }

        [Serializable]
        private sealed class SceneIssueFrame
        {
            public int screenWidth;
            public int screenHeight;
            public SceneIssueCamera camera;
            public SceneIssueCircle[] circles;
        }

        [Serializable]
        private sealed class SceneIssueCamera
        {
            public string hierarchyPath;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView;
            public bool orthographic;
            public float orthographicSize;
            public float nearClipPlane;
            public float farClipPlane;
        }

        [Serializable]
        private sealed class SceneIssueCircle
        {
            public float centerX;
            public float centerY;
            public float radius;
        }
    }
}
