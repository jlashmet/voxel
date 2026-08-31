using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonSourceArchiveTests
    {
        private const string SourceDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/source";
        private const string Base64Alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

        [Test]
        public void ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity()
        {
            byte[] obj = MountainDragonSourceArchive.ReconstructObjBytes();

            Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
        }

        [Test]
        public void DiagnosePart13TransferLoss_MissingProbeBoundaryCharacterHasUniquePinnedIdentity()
        {
            string sourceDirectory = Path.Combine(Directory.GetCurrentDirectory(), SourceDirectory);
            string part13 = ReadSourcePiece(sourceDirectory, "mountain_dragon_clean.obj.gz.b64.part13");
            string probe = ReadSourcePiece(sourceDirectory, "_probe13first5k");

            Assert.Multiple(() =>
            {
                Assert.That(part13, Has.Length.EqualTo(19_999),
                    "The minimal repro is scoped to the demonstrated one-character part13 transfer loss.");
                Assert.That(probe, Has.Length.EqualTo(4_999),
                    "The historical chunk deliberately staged as a 5k transport probe must reproduce the observed loss.");
                Assert.That(part13.StartsWith(probe, StringComparison.Ordinal), Is.True,
                    "The historical 4,999-character probe must be the exact current part13 prefix before searching its boundary.");
            });

            var encoded = new StringBuilder(320_000);
            for (int part = 0; part < 13; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));

            int insertionOffset = encoded.Length + probe.Length;
            encoded.Append(part13);
            for (int part = 14; part < 16; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));

            Assert.That(encoded, Has.Length.EqualTo(319_999));

            char? match = null;
            byte[] matchedCompressed = null;
            for (int i = 0; i < Base64Alphabet.Length; i++)
            {
                string candidate = encoded.ToString().Insert(insertionOffset, Base64Alphabet[i].ToString());
                byte[] compressed = Convert.FromBase64String(candidate);
                if (!string.Equals(Sha256Hex(compressed), MountainDragonSourceArchive.ExpectedGzipSha256,
                        StringComparison.Ordinal))
                    continue;

                Assert.That(match.HasValue, Is.False,
                    "More than one Base64 character at the demonstrated transfer boundary matched the pinned gzip identity.");
                match = Base64Alphabet[i];
                matchedCompressed = compressed;
            }

            Assert.That(match.HasValue, Is.True,
                $"No Base64 character inserted at logical part13 offset {probe.Length} matched pinned gzip SHA-256 " +
                MountainDragonSourceArchive.ExpectedGzipSha256 + ".");

            byte[] obj = Decompress(matchedCompressed);
            Assert.Multiple(() =>
            {
                Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
                Assert.That(Sha256Hex(obj), Is.EqualTo(MountainDragonSourceArchive.ExpectedObjSha256));
            });

            TestContext.Out.WriteLine(
                $"Recovered unique part13 transfer character '{match.Value}' at logical offset {probe.Length}; " +
                $"full Base64 offset {insertionOffset} matches pinned gzip and OBJ identities.");
        }

        [Test]
        public void Diagnostic_FirstTransferChunk_RevealsHistoricalObjPrefix()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                SourceDirectory + "/mountain_dragon_clean.obj.gz.b64.part00");
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

        private static string ReadSourcePiece(string sourceDirectory, string fileName)
        {
            return File.ReadAllText(Path.Combine(sourceDirectory, fileName), Encoding.ASCII).Trim();
        }

        private static byte[] Decompress(byte[] compressed)
        {
            using var input = new MemoryStream(compressed, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
