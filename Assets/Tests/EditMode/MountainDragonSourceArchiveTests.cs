using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonSourceArchiveTests
    {
        private const string SourceDirectory =
            "SceneIssues/open/20260829-050700-000-VoxelShowcaseDragonMeshVoxelization/source";

        [Test]
        public void ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity()
        {
            byte[] obj = MountainDragonSourceArchive.ReconstructObjBytes();

            Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
        }

        [Test]
        public void ReconstructObjBytes_MissingLogicalPartFailsClosed()
        {
            string tempRoot = CreateTempRepositoryCopy(skipLogicalPart: 17);
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => MountainDragonSourceArchive.ReconstructObjBytes(tempRoot));
                StringAssert.Contains("not contiguous", exception.Message);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Test]
        public void ReconstructObjBytes_ChangedPieceFailsPinnedIdentity()
        {
            string tempRoot = CreateTempRepositoryCopy(skipLogicalPart: null);
            try
            {
                string piece = Path.Combine(
                    tempRoot,
                    SourceDirectory,
                    "mountain_dragon_clean.obj.gz.b64.part20");
                string encoded = File.ReadAllText(piece, Encoding.ASCII).Trim();
                char replacement = encoded[0] == 'A' ? 'B' : 'A';
                File.WriteAllText(piece, replacement + encoded.Substring(1), Encoding.ASCII);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => MountainDragonSourceArchive.ReconstructObjBytes(tempRoot));
                StringAssert.Contains("incomplete or changed", exception.Message);
                StringAssert.Contains(MountainDragonSourceArchive.ExpectedGzipSha256, exception.Message);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static string CreateTempRepositoryCopy(int? skipLogicalPart)
        {
            string source = Path.Combine(Directory.GetCurrentDirectory(), SourceDirectory);
            string root = Path.Combine(
                Path.GetTempPath(),
                "mountain-dragon-source-archive-tests-" + Guid.NewGuid().ToString("N"));
            string destination = Path.Combine(root, SourceDirectory);
            Directory.CreateDirectory(destination);

            foreach (string path in Directory.GetFiles(
                         source,
                         "mountain_dragon_clean.obj.gz.b64.part*",
                         SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(path);
                if (skipLogicalPart.HasValue && IsLogicalPart(fileName, skipLogicalPart.Value))
                    continue;
                File.Copy(path, Path.Combine(destination, fileName));
            }

            return root;
        }

        private static bool IsLogicalPart(string fileName, int logicalPart)
        {
            string marker = $"part{logicalPart:00}";
            int index = fileName.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return false;
            int end = index + marker.Length;
            return end == fileName.Length || (end < fileName.Length && fileName[end] == '.');
        }
    }
}
