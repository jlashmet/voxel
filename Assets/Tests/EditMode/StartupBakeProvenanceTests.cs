using System.IO;
using NUnit.Framework;
using VoxelEngine.Composition;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StartupBakeProvenanceTests
    {
        [Test]
        public void CallerProvidedSignatureRoundTripsWithoutShowcasePolicy()
        {
            byte[] payload = { 1, 3, 3, 7, 42 };
            const int version = 3;
            const uint sourceSignature = 0xA17E5EEDu;

            string manifest = StartupBakeProvenance.CreateManifest(
                version,
                sourceSignature,
                payload);

            Assert.DoesNotThrow(() => StartupBakeProvenance.Validate(
                version,
                sourceSignature,
                payload,
                manifest,
                "independent fixture"));
            Assert.Throws<InvalidDataException>(() => StartupBakeProvenance.Validate(
                version,
                sourceSignature + 1,
                payload,
                manifest,
                "independent fixture"));
        }
    }
}
