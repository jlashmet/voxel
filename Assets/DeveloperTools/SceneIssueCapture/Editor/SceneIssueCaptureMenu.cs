#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MountingForce.DeveloperTools;

namespace MountingForce.DeveloperTools.Editor
{
    public static class SceneIssueCaptureMenu
    {
        private const string MenuRoot = "Tools/Scene Issue Capture/";

        [MenuItem(MenuRoot + "Replay Latest Capture", priority = 100)]
        private static void ReplayLatestCapture()
        {
            string root = SceneIssueCapture.GetCaptureRootPath();
            if (!Directory.Exists(root))
            {
                EditorUtility.DisplayDialog("Scene Issue Capture", "No captures have been saved yet.", "OK");
                return;
            }

            string latest = Directory.EnumerateFiles(root, "issue.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(latest))
            {
                EditorUtility.DisplayDialog("Scene Issue Capture", "No issue.json capture files were found.", "OK");
                return;
            }

            Replay(latest);
        }

        [MenuItem(MenuRoot + "Replay Capture...", priority = 101)]
        private static void ReplayCapture()
        {
            string root = SceneIssueCapture.GetCaptureRootPath();
            Directory.CreateDirectory(root);
            string path = EditorUtility.OpenFilePanel("Replay scene issue capture", root, "json");
            if (!string.IsNullOrEmpty(path))
                Replay(path);
        }

        [MenuItem(MenuRoot + "Open Captures Folder", priority = 120)]
        private static void OpenCapturesFolder()
        {
            string root = SceneIssueCapture.GetCaptureRootPath();
            Directory.CreateDirectory(root);
            EditorUtility.RevealInFinder(root);
        }

        private static void Replay(string path)
        {
            SceneIssueCaptureRecord record;
            try
            {
                record = JsonUtility.FromJson<SceneIssueCaptureRecord>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Scene Issue Capture", "Could not read this capture file.", "OK");
                return;
            }

            if (record == null || string.IsNullOrEmpty(record.scenePath))
            {
                EditorUtility.DisplayDialog(
                    "Scene Issue Capture",
                    "This capture does not contain a saved Unity scene path, so it cannot be replayed automatically.",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(record.scenePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Scene Issue Capture",
                    $"The captured scene no longer exists at:\n{record.scenePath}",
                    "OK");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Scene Issue Capture",
                    "Stop Play Mode before starting a replay from the Tools menu.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorPrefs.SetString(SceneIssueCapture.ReplayRequestEditorPrefsKey, path);
            EditorSceneManager.OpenScene(record.scenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
