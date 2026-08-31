using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Preserves the exact fresh startup bake used by the Mountain Dragon SceneIssue run inside
    /// the existing screenshot artifact directory. Normal launches are unaffected: this activates
    /// only for the issue-owned command line and never authors or mutates world content.
    /// </summary>
    internal static class MountainDragonBakeEvidenceCollector
    {
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";
        private const string AssignmentId = "20260828-180417-000-VoxelShowcaseMountainDragonCutscene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PreserveFreshBake()
        {
            string issuePath = Argument(SceneIssueArgument);
            string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
            if (string.IsNullOrWhiteSpace(issuePath) || string.IsNullOrWhiteSpace(screenshotDirectory))
                return;

            try
            {
                string fullIssuePath = Path.GetFullPath(issuePath);
                string issueDirectory = Path.GetDirectoryName(fullIssuePath) ?? string.Empty;
                if (!string.Equals(Path.GetFileName(issueDirectory), AssignmentId, StringComparison.Ordinal))
                    return;

                DirectoryInfo assignment = new DirectoryInfo(issueDirectory);
                DirectoryInfo open = assignment.Parent;
                DirectoryInfo sceneIssues = open?.Parent;
                DirectoryInfo projectRoot = sceneIssues?.Parent;
                if (projectRoot == null
                    || !string.Equals(open?.Name, "open", StringComparison.Ordinal)
                    || !string.Equals(sceneIssues?.Name, "SceneIssues", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Mountain Dragon evidence path is not under SceneIssues/open.");
                }

                string resourceDirectory = Path.Combine(
                    projectRoot.FullName, "Assets", "Resources", "VoxelShowcase");
                string bakePath = Path.Combine(resourceDirectory, "ShowcaseWorld.bytes");
                string manifestPath = Path.Combine(resourceDirectory, "ShowcaseWorld.manifest.txt");
                if (!File.Exists(bakePath) || !File.Exists(manifestPath))
                    throw new FileNotFoundException("Fresh Mountain Dragon bake evidence is missing.");

                string outputDirectory = Path.Combine(screenshotDirectory, "accepted-bake");
                Directory.CreateDirectory(outputDirectory);
                string outputBake = Path.Combine(outputDirectory, "ShowcaseWorld.bytes");
                string outputManifest = Path.Combine(outputDirectory, "ShowcaseWorld.manifest.txt");
                File.Copy(bakePath, outputBake, true);
                File.Copy(manifestPath, outputManifest, true);

                byte[] bytes = File.ReadAllBytes(bakePath);
                string sha256;
                using (SHA256 hash = SHA256.Create())
                    sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();

                string sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty;
                File.WriteAllText(
                    Path.Combine(outputDirectory, "ShowcaseWorld.evidence.txt"),
                    $"sourceSha={sourceSha}\nsizeBytes={bytes.LongLength}\nsha256={sha256}\n");
                Debug.Log($"MOUNTAIN_DRAGON_BAKE_EVIDENCE preserved size={bytes.LongLength} sha256={sha256}");
            }
            catch (Exception error)
            {
                Debug.LogError($"MOUNTAIN_DRAGON_BAKE_EVIDENCE failed: {error}");
                Application.Quit(25);
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }
    }
}
