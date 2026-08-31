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
        public void DiagnosePart13TransferLoss_HistoricalTransfersRemainNonAuthoritative()
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

            // The current prefix probe is self-consistent with part13, but it is not the older historical
            // part13.00 Git blob (d87618d...). This contradictory repository history is the key minimal
            // repro: no source mutation can be justified by assuming the staged transfers all came from
            // one authoritative byte stream.
            string currentPrefixBlob = GitBlobSha1(prefixProbe);
            Assert.Multiple(() =>
            {
                Assert.That(currentPrefixBlob,
                    Is.EqualTo("ee2a84d752398b31be7d2c1f8c7884adee87dacd"),
                    "The current prefix probe changed; re-isolate transfer provenance before another source repair.");
                Assert.That(currentPrefixBlob,
                    Is.Not.EqualTo("d87618d052f83b18b0510951b2fef33b760f217b"),
                    "The current prefix unexpectedly converged with the older historical part13.00 transfer.");
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
            Assert.That(repairedPart13, Has.Length.EqualTo(20_000));

            var encoded = new StringBuilder(320_000);
            for (int part = 0; part < 13; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));
            encoded.Append(repairedPart13);
            for (int part = 14; part < 16; part++)
                encoded.Append(ReadSourcePiece(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));

            Assert.That(encoded, Has.Length.EqualTo(320_000));
            byte[] compressed = Convert.FromBase64String(encoded.ToString());
            string candidateGzipSha = Sha256Hex(compressed);
            Assert.That(candidateGzipSha, Is.Not.EqualTo(MountainDragonSourceArchive.ExpectedGzipSha256),
                "Repository history unexpectedly recovered the pinned source; replace this diagnostic with the exact-source regression.");

            TestContext.Out.WriteLine(
                $"Current prefix blob {currentPrefixBlob} contradicts historical part13.00 d87618d052f83b18b0510951b2fef33b760f217b. " +
                $"The independently staged tail still proves a one-character deletion ('{recoveredCharacter}' at part13 offset {part13InsertionOffset}), " +
                $"but applying that repair yields gzip SHA-256 {candidateGzipSha}, not pinned " +
                $"{MountainDragonSourceArchive.ExpectedGzipSha256}. Known repository transfers are not an authoritative reconstruction source.");
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

        private static string GitBlobSha1(string value)
        {
            byte[] content = Encoding.ASCII.GetBytes(value);
            byte[] header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
            var blob = new byte[header.Length + content.Length];
            Buffer.BlockCopy(header, 0, blob, 0, header.Length);
            Buffer.BlockCopy(content, 0, blob, header.Length, content.Length);
            using SHA1 sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(blob);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                text.Append(hash[i].ToString("x2"));
            return text.ToString();
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
