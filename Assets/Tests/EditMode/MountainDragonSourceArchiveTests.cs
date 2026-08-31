using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonSourceArchiveTests
    {
        [Test]
        public void ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity()
        {
            byte[] obj = MountainDragonSourceArchive.ReconstructObjBytes();

            Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
        }

        [Test]
        public void Diagnostic_FirstTransferChunk_RevealsHistoricalObjPrefix()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/source/" +
                "mountain_dragon_clean.obj.gz.b64.part00");
            string encoded = File.ReadAllText(path, Encoding.ASCII).Trim();
            byte[] compressedPrefix = Convert.FromBase64String(encoded);

            using var input = new MemoryStream(compressedPrefix, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            try
            {
                while (output.Length < 16 * 1024)
                {
                    int read = gzip.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        break;
                    output.Write(buffer, 0, read);
                }
            }
            catch (InvalidDataException)
            {
                // Expected: part00 is intentionally only a prefix of the complete gzip member.
            }

            string text = Encoding.UTF8.GetString(output.ToArray());
            Assert.That(text.Length, Is.GreaterThan(2048),
                "The first transfer chunk should expose enough historical OBJ text to discriminate serialization.");
            int printableLength = Math.Min(text.Length, 8192);
            TestContext.Out.WriteLine("=== MOUNTAIN DRAGON HISTORICAL OBJ PREFIX ===");
            TestContext.Out.WriteLine(text.Substring(0, printableLength));
            TestContext.Out.WriteLine("=== END MOUNTAIN DRAGON HISTORICAL OBJ PREFIX ===");
        }
    }
}
