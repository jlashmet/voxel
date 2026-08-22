using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOpeningSurfaceCoverageDiagnosticTests
    {
        [UnityTest]
        public IEnumerator OpeningCamera_ReportsStuckSurfaceCoverage()
        {
            Scene previous = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync("KentridgePlayableSlice", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            Scene scene = SceneManager.GetSceneByName("KentridgePlayableSlice");
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);

            for (var frame = 0; frame < 1200; frame++)
            {
                if (RenderingComposition.HasCompletePublishedNearSurfaceCoverage()) break;
                yield return null;
            }

            RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missing);
            RenderingComposition.TryGetSurfaceBuildStatus(
                out int known, out int dirty, out int resident, out long residentBytes);
            string rings = RenderingComposition.DescribeVoxelRings() ?? "<no ring diagnostics>";
            bool complete = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();

            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null)
                while (!unload.isDone) yield return null;

            Assert.That(complete, Is.True,
                $"Opening coverage remained incomplete: visible={visible} missing={missing} "
              + $"known={known} resident={resident} dirty={dirty} bytes={residentBytes}; {rings}");
        }
    }
}
