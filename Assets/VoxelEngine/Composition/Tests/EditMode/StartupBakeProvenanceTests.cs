using System.IO;
using NUnit.Framework;
using VoxelEngine.Composition;

namespace VoxelEngine.Composition.Tests
{
    public sealed class StartupBakeProvenanceTests
    {
        [Test]
        public void ManifestRoundTripsExactPayloadAndSemanticSignature()
        {
            byte[] payload = { 1, 3, 3, 7, 9 };
            const uint signature = 0xA17C04D2;
            string manifest = StartupBakeProvenance.CreateManifest(2, signature, payload);

            Assert.DoesNotThrow(() => StartupBakeProvenance.Validate(2, signature, payload, manifest));
            Assert.That(manifest, Does.Contain("contentSignature=A17C04D2"));
            Assert.That(manifest, Does.Contain("payloadSha256=" + StartupBakeProvenance.ComputePayloadSha256(payload)));
        }

        [Test]
        public void ManifestRejectsChangedPayloadBytes()
        {
            byte[] payload = { 1, 2, 3 };
            string manifest = StartupBakeProvenance.CreateManifest(1, 0x12345678, payload);
            byte[] changed = { 1, 2, 4 };

            Assert.Throws<InvalidDataException>(
                () => StartupBakeProvenance.Validate(1, 0x12345678, changed, manifest));
        }
    }
}
