using System;
using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SceneIssueCapturePathTests
    {
        [Test]
        public void OpenCaptureRootIsOpenChildOfSceneIssuesRoot()
        {
            Type captureType = Type.GetType(
                "MountingForce.DeveloperTools.SceneIssueCapture, Assembly-CSharp",
                throwOnError: true);
            string captureRoot = (string)captureType.GetMethod("GetCaptureRootPath").Invoke(null, null);
            string openRoot = (string)captureType.GetMethod("GetOpenCaptureRootPath").Invoke(null, null);

            Assert.That(
                openRoot,
                Is.EqualTo(Path.Combine(captureRoot, "open")));
        }
    }
}
