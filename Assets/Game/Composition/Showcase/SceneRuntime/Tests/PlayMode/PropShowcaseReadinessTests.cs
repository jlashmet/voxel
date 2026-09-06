using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class PropShowcaseReadinessTests
    {
        [UnityTest]
        public IEnumerator VoxelSelection_IsNotReportedReadyBeforeSurfacePublication()
        {
            var cameraObject = new GameObject("PropShowcase readiness regression");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            PropShowcase browser = cameraObject.AddComponent<PropShowcase>();

            try
            {
                cameraObject.SetActive(true);
                Assert.That(browser.EntryCount, Is.EqualTo(PropShowcase.ExpectedCatalogueCount));
                Assert.That(browser.SelectedStableId, Is.Not.Empty);
                Assert.That(browser.IsPresentationReady, Is.False,
                    "A voxel-backed selection must remain loading until production surface publication completes.");
            }
            finally
            {
                cameraObject.SetActive(false);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }
    }
}
