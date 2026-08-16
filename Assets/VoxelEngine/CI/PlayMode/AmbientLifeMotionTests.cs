using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Showcase;

namespace VoxelEngine.CI
{
    public sealed class AmbientLifeMotionTests
    {
        [Test]
        public void AllMovementForms_AreDeterministicBoundedAndActuallyMove()
        {
            Vector3 centre = new Vector3(2f, 1f, -3f);
            Vector3 basePosition = centre + new Vector3(2.1f, 0.8f, 0.7f);
            const float radius = 4f;
            const uint agentSeed = 0x1234ABCDu;
            const uint clusterSeed = 0xBEEF7711u;

            foreach (AmbientMovementForm form in System.Enum.GetValues(typeof(AmbientMovementForm)))
            {
                Vector3 first = AmbientLifeMotion.EvaluatePosition(
                    form, basePosition, centre, radius, agentSeed, clusterSeed, 2, 0.37f);
                Vector3 repeated = AmbientLifeMotion.EvaluatePosition(
                    form, basePosition, centre, radius, agentSeed, clusterSeed, 2, 0.37f);
                Assert.That(Vector3.Distance(first, repeated), Is.LessThan(0.000001f),
                    $"{form} must be deterministic for the same seed and time.");

                float maxMove = 0f;
                float maxY = basePosition.y;
                float minY = basePosition.y;
                Vector3 previous = AmbientLifeMotion.EvaluatePosition(
                    form, basePosition, centre, radius, agentSeed, clusterSeed, 2, 0f);

                for (int sample = 1; sample <= 20; sample++)
                {
                    float time = sample * 0.23f;
                    Vector3 current = AmbientLifeMotion.EvaluatePosition(
                        form, basePosition, centre, radius, agentSeed, clusterSeed, 2, time);
                    maxMove = Mathf.Max(maxMove, Vector3.Distance(previous, current));
                    maxY = Mathf.Max(maxY, current.y);
                    minY = Mathf.Min(minY, current.y);
                    float horizontal = Vector2.Distance(
                        new Vector2(current.x, current.z), new Vector2(centre.x, centre.z));
                    Assert.That(horizontal, Is.LessThanOrEqualTo(radius * 1.041f),
                        $"{form} escaped its cluster radius at t={time:0.00}.");
                    previous = current;
                }

                Assert.That(maxMove, Is.GreaterThan(0.015f),
                    $"{form} did not produce meaningful locomotion over time.");

                if (form == AmbientMovementForm.GroundScuttle)
                {
                    Assert.That(maxY, Is.EqualTo(basePosition.y).Within(0.0001f));
                    Assert.That(minY, Is.EqualTo(basePosition.y).Within(0.0001f));
                }
                else if (form == AmbientMovementForm.Hop)
                {
                    Assert.That(maxY - basePosition.y, Is.GreaterThan(0.12f),
                        "Hop must visibly leave the ground.");
                    Assert.That(Mathf.Abs(minY - basePosition.y), Is.LessThan(0.001f),
                        "Hop must return to the ground between jumps.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Showcase_ReconstructedAgentsMoveOverTime_WithoutChangingAuthority()
        {
            GameObject root = new GameObject("Ambient Life Motion Contract");
            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;

                ProceduralAmbientLifeBatchRenderer renderer =
                    root.GetComponent<ProceduralAmbientLifeBatchRenderer>();
                Assert.That(renderer, Is.Not.Null);
                renderer.enabled = false;

                var atZero = new List<Vector3>();
                var atLater = new List<Vector3>();
                int zeroCount = renderer.CopyAgentPositionsAtTime(0f, atZero);
                int laterCount = renderer.CopyAgentPositionsAtTime(1.75f, atLater);

                Assert.That(zeroCount, Is.EqualTo(showcase.AgentCount));
                Assert.That(laterCount, Is.EqualTo(zeroCount));
                Assert.That(showcase.ClusterCount, Is.EqualTo(AmbientLifeCatalogue.Count),
                    "Animating presentation must not mutate authoritative cluster count.");

                int visiblyMoved = 0;
                float totalMovement = 0f;
                for (int i = 0; i < atZero.Count; i++)
                {
                    float moved = Vector3.Distance(atZero[i], atLater[i]);
                    totalMovement += moved;
                    if (moved > 0.025f) visiblyMoved++;
                }

                float movedRatio = visiblyMoved / (float)Mathf.Max(1, atZero.Count);
                float averageMovement = totalMovement / Mathf.Max(1, atZero.Count);
                Assert.That(movedRatio, Is.GreaterThan(0.85f),
                    "Too many reconstructed agents remain effectively stationary.");
                Assert.That(averageMovement, Is.GreaterThan(0.08f),
                    "Ambient-life locomotion is too subtle to read in the world.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        [NUnit.Framework.Explicit("Two-frame visual motion capture; run by rendering CI.")]
        public IEnumerator Showcase_RenderedFrameChangesSubstantiallyAcrossTime()
        {
            GameObject cameraObject = VegetationLifeRenderingVisualTests.CreateCamera(
                "CI Ambient Life Motion Camera",
                new Vector3(0f, 5.4f, -13.2f),
                new Vector3(0f, 1.55f, 8.2f),
                52f,
                out Camera camera,
                out RenderTexture target);
            GameObject root = new GameObject("CI Ambient Life Motion Showcase");
            Texture2D first = null;
            Texture2D second = null;

            try
            {
                AmbientLifeRenderingShowcase showcase = root.AddComponent<AmbientLifeRenderingShowcase>();
                yield return null;
                VegetationLifeRenderingVisualTests.RemovePresentationGeometry(root.transform);

                ProceduralAmbientLifeBatchRenderer renderer =
                    root.GetComponent<ProceduralAmbientLifeBatchRenderer>();
                Assert.That(renderer, Is.Not.Null);
                renderer.enabled = false;

                renderer.DrawAtTime(0f);
                camera.Render();
                first = VegetationLifeRenderingVisualTests.ReadTarget(target);
                File.WriteAllBytes(
                    VegetationLifeRenderingVisualTests.ArtifactPath("ambient_life_motion_t0.png"),
                    first.EncodeToPNG());

                renderer.DrawAtTime(1.75f);
                camera.Render();
                second = VegetationLifeRenderingVisualTests.ReadTarget(target);
                File.WriteAllBytes(
                    VegetationLifeRenderingVisualTests.ArtifactPath("ambient_life_motion_t175.png"),
                    second.EncodeToPNG());

                Color32[] a = first.GetPixels32();
                Color32[] b = second.GetPixels32();
                int changed = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    float dr = (a[i].r - b[i].r) / 255f;
                    float dg = (a[i].g - b[i].g) / 255f;
                    float db = (a[i].b - b[i].b) / 255f;
                    if (dr * dr + dg * dg + db * db > 0.010f)
                        changed++;
                }

                File.WriteAllText(
                    VegetationLifeRenderingVisualTests.ArtifactPath("ambient_life_motion_quality.txt"),
                    $"changed_pixels={changed}\nchanged_ratio={changed / (float)a.Length:0.0000}\n");
                Assert.That(changed, Is.GreaterThan(1800),
                    "Rendered ambient life did not move enough between deterministic timestamps.");
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
                VegetationLifeRenderingVisualTests.ReleaseTarget(camera, target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }
    }
}
