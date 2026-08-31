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
        private const string DiagnosticDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/diagnostics";

        [Test]
        public void ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity()
        {
            byte[] obj = MountainDragonSourceArchive.ReconstructObjBytes();

            Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
        }

        [Test]
        public void DiagnosePart13TransferLoss_HistoricalTailLocatesPinnedCharacter()
        {
            string root = Directory.GetCurrentDirectory();
            string sourceDirectory = Path.Combine(root, SourceDirectory);
            string diagnosticDirectory = Path.Combine(root, DiagnosticDirectory);
            string part13 = ReadSourcePiece(sourceDirectory, "mountain_dragon_clean.obj.gz.b64.part13");
            string prefixProbe = ReadSourcePiece(sourceDirectory, "_probe13first5k");
            string historicalTail = ReadSourcePiece(diagnosticDirectory, "historical-part13.02");

            Assert.Multiple(() =>
            {
                Assert.That(part13, Has.Length.EqualTo(19_999),
                    "The minimal repro is scoped to the demonstrated one-character part13 transfer loss.");
                Assert.That(prefixProbe, Has.Length.EqualTo(4_999),
                    "The historical first probe must reproduce the observed 4,999-character prefix.");
                Assert.That(historicalTail, Has.Length.EqualTo(5_001),
                    "The immutable historical tail fixture must remain the exact staged 5,001-character blob.");
                Assert.That(part13.StartsWith(prefixProbe, StringComparison.Ordinal), Is.True,
                    "The historical prefix probe must be the exact current part13 prefix.");
            });

            string observedTailWindow = part13.Substring(prefixProbe.Length, historicalTail.Length - 1);
            int divergence = FirstDifference(historicalTail, observedTailWindow);
            Assert.That(divergence, Is.LessThan(historicalTail.Length),
                "The historical 5,001-character tail unexpectedly equals the 5,000-character observed window.");

            char recoveredCharacter = historicalTail[divergence];
            string repairedTailWindow = observedTailWindow.Insert(divergence, recoveredCharacter.ToString());
            Assert.That(repairedTailWindow, Is.EqualTo(historicalTail),
                $"Historical tail is not the observed current window plus one deletion. First divergence={divergence}; " +
                $"historical={Context(historicalTail, divergence)}; observed={Context(observedTailWindow, divergence)}.");

            int part13InsertionOffset = prefixProbe.Length + divergence;
            string repairedPart13 = part13.Insert(part13InsertionOffset, recoveredCharacter.ToString());

            var encoded = new StringBuilder(320_000);
            for (int part = 0; part < 13; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));
            encoded.Append(repairedPart13);
            for (int part = 14; part < 16; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));

            Assert.That(encoded, Has.Length.EqualTo(320_000));
            byte[] compressed = Convert.FromBase64String(encoded.ToString());
            Assert.That(Sha256Hex(compressed), Is.EqualTo(MountainDragonSourceArchive.ExpectedGzipSha256),
                $"Historical tail localized candidate '{recoveredCharacter}' at part13 offset {part13InsertionOffset}, " +
                "but that candidate does not match the pinned gzip identity.");

            byte[] obj = Decompress(compressed);
            Assert.Multiple(() =>
            {
                Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
                Assert.That(Sha256Hex(obj), Is.EqualTo(MountainDragonSourceArchive.ExpectedObjSha256));
            });

            TestContext.Out.WriteLine(
                $"Recovered unique part13 transfer character '{recoveredCharacter}' at logical offset " +
                $"{part13InsertionOffset}; full archive matches pinned gzip and OBJ identities.");
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

        private static int FirstDifference(string expected, string observed)
        {
            int length = Math.Min(expected.Length, observed.Length);
            for (int i = 0; i < length; i++)
            {
                if (expected[i] != observed[i])
                    return i;
            }

            return length;
        }

        private static string Context(string value, int offset)
        {
            int start = Math.Max(0, offset - 12);
            int length = Math.Min(value.Length - start, 25);
            return "'" + value.Substring(start, length) + "'";
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
