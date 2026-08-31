using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Minimal root-cause discriminator for the historical dragon source transfer.
    /// The immutable tail fixture proves one Base64 character was deleted from part13. This test
    /// applies only that demonstrated repair, then reports whether the resulting gzip expands to
    /// the already pinned OBJ identity. It does not make the historical transfer authoritative.
    /// </summary>
    public sealed class MountainDragonHistoricalArchiveRecoveryTests
    {
        private const string SourceDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/source";
        private const string DiagnosticDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/diagnostics";

        [Test]
        public void RepairedKnownDeletion_ReportsDecompressedObjIdentity()
        {
            string root = Directory.GetCurrentDirectory();
            string sourceDirectory = Path.Combine(root, SourceDirectory);
            string diagnosticDirectory = Path.Combine(root, DiagnosticDirectory);

            string part13 = Read(sourceDirectory, "mountain_dragon_clean.obj.gz.b64.part13");
            string prefix = Read(sourceDirectory, "_probe13first5k");
            string historicalTail = Read(diagnosticDirectory, "historical-part13.02");

            Assert.That(part13, Has.Length.EqualTo(19_999));
            Assert.That(prefix, Has.Length.EqualTo(4_999));
            Assert.That(historicalTail, Has.Length.EqualTo(5_001));
            Assert.That(part13.StartsWith(prefix, StringComparison.Ordinal), Is.True);

            string observedTailWindow = part13.Substring(prefix.Length, historicalTail.Length - 1);
            int divergence = FirstDifference(historicalTail, observedTailWindow);
            Assert.That(divergence, Is.LessThan(historicalTail.Length));

            char recoveredCharacter = historicalTail[divergence];
            string repairedTailWindow = observedTailWindow.Insert(divergence, recoveredCharacter.ToString());
            Assert.That(repairedTailWindow, Is.EqualTo(historicalTail));

            int insertionOffset = prefix.Length + divergence;
            string repairedPart13 = part13.Insert(insertionOffset, recoveredCharacter.ToString());
            Assert.That(repairedPart13, Has.Length.EqualTo(20_000));

            var encoded = new StringBuilder(320_000);
            for (int part = 0; part < 13; part++)
                encoded.Append(Read(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));
            encoded.Append(repairedPart13);
            for (int part = 14; part < 16; part++)
                encoded.Append(Read(sourceDirectory, $"mountain_dragon_clean.obj.gz.b64.part{part:00}"));

            Assert.That(encoded, Has.Length.EqualTo(320_000));
            byte[] compressed = Convert.FromBase64String(encoded.ToString());
            string gzipSha = Sha256Hex(compressed);

            byte[] obj;
            using (var input = new MemoryStream(compressed, writable: false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                obj = output.ToArray();
            }

            string objSha = Sha256Hex(obj);
            TestContext.Out.WriteLine(
                $"Known deletion repair: part13 offset={insertionOffset}, char='{recoveredCharacter}', " +
                $"gzipBytes={compressed.Length}, gzipSha256={gzipSha}, " +
                $"objBytes={obj.Length}, objSha256={objSha}, " +
                $"pinnedObjBytes={MountainDragonSourceArchive.ExpectedObjByteCount}, " +
                $"pinnedObjSha256={MountainDragonSourceArchive.ExpectedObjSha256}.");

            Assert.That(obj.Length, Is.GreaterThan(100_000),
                "The repaired historical transfer must expand to a substantial OBJ before its identity is evaluated.");
        }

        private static string Read(string directory, string fileName) =>
            File.ReadAllText(Path.Combine(directory, fileName), Encoding.ASCII).Trim();

        private static int FirstDifference(string expected, string observed)
        {
            int length = Math.Min(expected.Length, observed.Length);
            for (int i = 0; i < length; i++)
                if (expected[i] != observed[i]) return i;
            return length;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
