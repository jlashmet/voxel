using System;
using System.IO;
using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    /// <summary>
    /// One-shot acceptance bridge for producing the exact current-source VoxelShowcase startup
    /// payload through the real editor baker. The shared CI runner remains scene-agnostic; this
    /// issue-owned test exports the bytes and provenance sidecar under its normal artifact root.
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
            ShowcaseWorldBaker.BakeShowcaseWorld();

            Assert.That(File.Exists(SourceBytes), Is.True,
                "The production Showcase baker produced no startup payload.");
            byte[] bytes = File.ReadAllBytes(SourceBytes);
            Assert.That(bytes.Length, Is.GreaterThan(1024 * 1024),
                "The startup payload is unexpectedly tiny.");
            Assert.That(bytes.Length, Is.LessThan(20 * 1024 * 1024),
                "The startup payload exceeded the established compact-bake envelope.");

            string manifest = ShowcaseStartupBakeContract.CreateManifest(bytes);
            File.WriteAllText(
                ShowcaseStartupBakeContract.ManifestAssetPath,
                manifest,
                new System.Text.UTF8Encoding(false));

            Directory.CreateDirectory(ArtifactDirectory);
            File.WriteAllBytes(Path.Combine(ArtifactDirectory, "ShowcaseWorld.bytes"), bytes);
            File.WriteAllText(
                Path.Combine(ArtifactDirectory, "ShowcaseWorld.manifest.txt"),
                manifest,
                new System.Text.UTF8Encoding(false));

            ShowcaseStartupBakeContract.Validate(bytes, manifest);
            TestContext.Progress.WriteLine(
                "SHOWCASE_ACCEPTED_BAKE bytes=" + bytes.Length
                + " sha256=" + ShowcaseStartupBakeContract.ComputePayloadSha256(bytes)
                + " signature=" + ShowcaseStartupBakeContract.RequiredContentSignature.ToString("X8"));
        }
    }
}
