using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldbuildingGallerySecretClueCompositionTests
    {
        [Test]
        public void GalleryClueStagesEscalateFromGroundTraceToReadableMasonryWithoutMarkerComponents()
        {
            Type composition = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("VoxelEngine.Showcase.WorldbuildingGallerySecretClueComposition", false))
                .FirstOrDefault(type => type != null);
            Assert.That(composition, Is.Not.Null, "Gallery clue composition must be available to validation.");
            MethodInfo buildCue = composition.GetMethod("BuildCue", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(buildCue, Is.Not.Null);

            GameObject root = new GameObject("gallery-clue-regression-root");
            try
            {
                StageEvidence trace = BuildStage(buildCue, root.transform, 0);
                StageEvidence weathered = BuildStage(buildCue, root.transform, 1);
                StageEvidence seam = BuildStage(buildCue, root.transform, 2);

                Assert.That(trace.RendererCount, Is.GreaterThanOrEqualTo(5),
                    "The approach clue must read as a repeated environmental trail rather than one tiny prop.");
                Assert.That(trace.Bounds.size.x, Is.GreaterThanOrEqualTo(2.5f),
                    "The approach trace needs a gameplay-scale footprint at the gallery tour target.");

                Assert.That(weathered.RendererCount, Is.GreaterThanOrEqualTo(3),
                    "The middle clue must combine a slab and physical weathering notches.");
                Assert.That(weathered.Bounds.size.x, Is.GreaterThanOrEqualTo(1.8f));

                Assert.That(seam.RendererCount, Is.GreaterThanOrEqualTo(4),
                    "The final clue must present a composed masonry seam rather than a single generic cube.");
                Assert.That(seam.Bounds.size.y, Is.GreaterThanOrEqualTo(1.3f),
                    "The threshold clue must be readable at human gameplay scale.");

                Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty,
                    "Clue readability must not rely on glowing/light marker language.");
                Assert.That(root.GetComponentsInChildren<TextMesh>(true), Is.Empty,
                    "Clues must remain environmental geometry rather than placeholder signs.");
                Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty,
                    "Clues must not introduce UI marker authority into the gallery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static StageEvidence BuildStage(MethodInfo buildCue, Transform root, int stage)
        {
            GameObject stageRoot = new GameObject("stage-" + stage);
            stageRoot.transform.SetParent(root, false);
            buildCue.Invoke(null, new object[] { stageRoot.transform, stage, Vector3.zero });
            Renderer[] renderers = stageRoot.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return new StageEvidence(renderers.Length, bounds);
        }

        private readonly struct StageEvidence
        {
            public StageEvidence(int rendererCount, Bounds bounds)
            {
                RendererCount = rendererCount;
                Bounds = bounds;
            }

            public int RendererCount { get; }
            public Bounds Bounds { get; }
        }
    }
}
