using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.PlayMode.CastleScreenshotTests
{
    /// <summary>
    /// Routing anchors for exact SceneIssue visual verification in the standalone-player capture
    /// path. The namespace intentionally sits under CastleScreenshotTests because that established
    /// visual profile already builds VoxelShowcase.unity and publishes real presented frames.
    /// The issue-specific camera itself comes from the temporary SceneIssueCameraPose resource on
    /// the CI request branch, not from hard-coded scene/test behavior.
    /// </summary>
    [Explicit("Visual acceptance for human review; run by exact test name.")]
    public sealed class SceneIssueVisualTests
    {
        [Test]
        public void SceneIssue20260823013924433UsesCapturedVoxelShowcaseView()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fixturePath = Path.Combine(projectRoot,
                "SceneIssues/20260823-013924-433-VoxelShowcase/issue.json");

            Assert.That(File.Exists(fixturePath), Is.True,
                $"SceneIssue fixture is missing: {fixturePath}");

            string json = File.ReadAllText(fixturePath);
            Assert.That(json, Does.Contain("\"sceneName\": \"VoxelShowcase\""));
            Assert.That(json, Does.Contain("\"hierarchyPath\": \"Showcase Camera\""));
            Assert.That(json, Does.Contain("75.63558959960938"));
            Assert.That(json, Does.Contain("-7.454492568969727"));
        }
    }
}
