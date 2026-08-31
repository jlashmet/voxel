using System.IO;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseStartupBakeContractTests
    {
        [Test]
        public void ManifestBindsCurrentLandmarkContractToExactPayload()
        {
            byte[] payload = { 0x10, 0x20, 0x30, 0x40, 0x50 };
            string manifest = ShowcaseStartupBakeContract.CreateManifest(payload);

            Assert.That(ShowcaseStartupBakeContract.RequiredContentSignature, Is.Not.EqualTo(0u));
            Assert.DoesNotThrow(() => ShowcaseStartupBakeContract.Validate(payload, manifest));

            byte[] changedPayload = (byte[])payload.Clone();
            changedPayload[2] ^= 0x01;
            Assert.Throws<InvalidDataException>(
                () => ShowcaseStartupBakeContract.Validate(changedPayload, manifest),
                "A manifest must be bound to the exact serialized bake bytes.");
        }

        [Test]
        public void ManifestRejectsStaleLandmarkSignature()
        {
            byte[] payload = { 0x56, 0x58, 0x53, 0x42 };
            string manifest = ShowcaseStartupBakeContract.CreateManifest(payload);
            string expected = ShowcaseStartupBakeContract.RequiredContentSignature.ToString("X8");
            string stale = manifest.Replace(
                "contentSignature=" + expected,
                "contentSignature=00000000");

            Assert.That(stale, Is.Not.EqualTo(manifest),
                "The current content signature unexpectedly collapsed to zero.");
            Assert.Throws<InvalidDataException>(
                () => ShowcaseStartupBakeContract.Validate(payload, stale),
                "A structurally valid old bake must not suppress newly-authored landmarks.");
        }
    }
}
