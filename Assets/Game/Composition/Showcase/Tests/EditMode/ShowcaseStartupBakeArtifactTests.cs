using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    /// <summary>
    /// Exports the exact current-source VoxelShowcase startup payload through the real editor
    /// baker. The test consumes the baker's provenance sidecar instead of manufacturing one;
    /// shared CI remains scene-agnostic. Exported candidates still require visual acceptance.
    /// </summary>
    public sealed class ShowcaseStartupBakeArtifactTests
    {
        private const string SourceBytes =
            "Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes";
        private const string ArtifactDirectory =
            "Artifacts/SingleTest/AcceptedShowcaseBake";

        [Test]
        public void CurrentSourceBakeExportsPayloadAndMatchingManifest()
        {
            // A sidecar left by an earlier test/build must not conceal a broken normal baker.
            File.Delete(ShowcaseStartupBakeContract.ManifestAssetPath);
            ShowcaseWorldBaker.BakeShowcaseWorld();

            Assert.That(File.Exists(SourceBytes), Is.True,
                "The production Showcase baker produced no startup payload.");
            byte[] bytes = File.ReadAllBytes(SourceBytes);
            Assert.That(bytes.Length, Is.GreaterThan(1024 * 1024),
                "The startup payload is unexpectedly tiny.");
            Assert.That(bytes.Length, Is.LessThan(20 * 1024 * 1024),
                "The startup payload exceeded the established compact-bake envelope.");

            Assert.That(File.Exists(ShowcaseStartupBakeContract.ManifestAssetPath), Is.True,
                "The normal Showcase baker must write its matching startup manifest without test repair.");
            string manifest = File.ReadAllText(ShowcaseStartupBakeContract.ManifestAssetPath);
            ShowcaseStartupBakeContract.Validate(bytes, manifest);
            TextAsset importedManifest = Resources.Load<TextAsset>(
                ShowcaseStartupBakeContract.ManifestResourcePath);
            Assert.That(importedManifest, Is.Not.Null,
                "The normal baker must import the manifest as a runtime Resources asset.");
            Assert.That(importedManifest.text, Is.EqualTo(manifest),
                "The imported manifest must be the sidecar emitted for this exact payload.");

            Directory.CreateDirectory(ArtifactDirectory);
            File.WriteAllBytes(Path.Combine(ArtifactDirectory, "ShowcaseWorld.bytes"), bytes);
            File.WriteAllText(
                Path.Combine(ArtifactDirectory, "ShowcaseWorld.manifest.txt"),
                manifest,
                new System.Text.UTF8Encoding(false));

            TestContext.Progress.WriteLine(
                "SHOWCASE_ACCEPTED_BAKE bytes=" + bytes.Length
                + " sha256=" + ShowcaseStartupBakeContract.ComputePayloadSha256(bytes)
                + " signature=" + ShowcaseStartupBakeContract.RequiredContentSignature.ToString("X8"));
        }
    }
}
