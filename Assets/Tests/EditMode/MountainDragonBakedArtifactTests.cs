using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonBakedArtifactTests
    {
        [Test]
        public void CheckedInBake_ResourceTextIsValidBase64()
        {
            TextAsset payload = Resources.Load<TextAsset>(MountainDragonBakedArtifact.ResourcePath);
            Assert.That(payload, Is.Not.Null);

            string text = payload.text;
            int paddingIndex = text.IndexOf('=');
            Assert.That(paddingIndex, Is.GreaterThan(0));
            Assert.That(text.Length % 4, Is.EqualTo(3), "Diagnostic assumes exactly one missing Base64 symbol.");

            string endPatched = text.Insert(paddingIndex, "A");
            byte[] probeBytes = Convert.FromBase64String(endPatched);
            long compressedFailureOffset;
            long decompressedBeforeFailure = 0;
            using (var input = new OneByteReadStream(probeBytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true))
            {
                var buffer = new byte[4096];
                try
                {
                    while (true)
                    {
                        int read = gzip.Read(buffer, 0, buffer.Length);
                        if (read == 0) break;
                        decompressedBeforeFailure += read;
                    }
                }
                catch (IOException)
                {
                    // Mono reports corrupt gzip/deflate data as IOException; expected for this probe.
                }
                compressedFailureOffset = input.Position;
            }

            int estimatedSymbol = checked((int)(compressedFailureOffset * 4L / 3L));
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            const int searchRadius = 512;
            int start = Math.Max(0, estimatedSymbol - searchRadius);
            int end = Math.Min(paddingIndex, estimatedSymbol + searchRadius);
            string found = null;
            using (SHA256 sha = SHA256.Create())
            {
                for (int index = start; index <= end && found == null; index++)
                {
                    for (int symbol = 0; symbol < alphabet.Length; symbol++)
                    {
                        string candidate = text.Insert(index, alphabet[symbol].ToString());
                        byte[] compressed = Convert.FromBase64String(candidate);
                        string hash = Hex(sha.ComputeHash(compressed));
                        if (string.Equals(hash, MountainDragonBakedArtifact.ExpectedTransportSha256, StringComparison.Ordinal))
                        {
                            found = $"index={index}, symbol='{alphabet[symbol]}', compressedBytes={compressed.Length}";
                            break;
                        }
                    }
                }
            }

            Assert.Fail(
                $"Missing-symbol diagnostic: found={found ?? "none"}; compressedFailureOffset={compressedFailureOffset}; " +
                $"decompressedBeforeFailure={decompressedBeforeFailure}; estimatedSymbol={estimatedSymbol}; " +
                $"searched=[{start},{end}]; textLength={text.Length}; paddingIndex={paddingIndex}.");
        }

        [Test]
        public void CheckedInBake_DecodesCanonicalArtifactAndPreservesDragonAnatomy()
        {
            BakedVoxelStructure bake = MountainDragonBakedArtifact.Load();

            Assert.That(bake.Cells.Length, Is.EqualTo(MountainDragonBakedArtifact.ExpectedCellCount));
            Assert.That(bake.SourceTriangleCount, Is.EqualTo(MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount));
            Assert.That(bake.VoxelSize, Is.EqualTo(MountainDragonVoxelBakePolicy.SourceVoxelSize));
            Assert.That(bake.Size, Is.EqualTo(new int3(99, 107, 107)));
            Assert.That(bake.GridOrigin, Is.EqualTo(new int3(-47, -32, 16)));
            Assert.That(bake.InteriorFilled, Is.True);

            AssertRegion(bake, "body",       new int3(35, 40, 35), new int3(70, 75, 75), 20_000);
            AssertRegion(bake, "left wing",  new int3(0, 62, 55),  new int3(35, 107, 107), 3_500);
            AssertRegion(bake, "right wing", new int3(70, 62, 55), new int3(99, 107, 107), 3_000);
            AssertRegion(bake, "head/horns", new int3(45, 35, 75), new int3(80, 65, 107), 3_000);
            AssertRegion(bake, "left foot/claws",  new int3(20, 15, 42), new int3(48, 38, 82), 6_000);
            AssertRegion(bake, "right foot/claws", new int3(52, 15, 42), new int3(80, 38, 82), 4_500);
            AssertRegion(bake, "curled tail", new int3(30, 65, 10), new int3(70, 105, 45), 4_500);
        }

        [Test]
        public void CheckedInBake_CorruptTransportFailsClosed()
        {
            TextAsset payload = Resources.Load<TextAsset>(MountainDragonBakedArtifact.ResourcePath);
            Assert.That(payload, Is.Not.Null);
            string text = payload.text;
            int index = text.Length / 2;
            char replacement = text[index] == 'A' ? 'B' : 'A';
            string corrupt = text.Substring(0, index) + replacement + text.Substring(index + 1);

            Assert.Throws<InvalidOperationException>(() => MountainDragonBakedArtifact.DecodeBase64(corrupt));
        }

        private static string Hex(byte[] bytes)
        {
            var text = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
            return text.ToString();
        }

        private sealed class OneByteReadStream : Stream
        {
            private readonly MemoryStream inner;

            public OneByteReadStream(byte[] bytes) => inner = new MemoryStream(bytes, writable: false);
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(1, count));
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing)
            {
                if (disposing) inner.Dispose();
                base.Dispose(disposing);
            }
        }

        private static void AssertRegion(
            BakedVoxelStructure bake,
            string label,
            int3 minInclusive,
            int3 maxExclusive,
            int minimumCells)
        {
            int count = 0;
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                int3 p = bake.Cells[i].Position;
                if (math.all(p >= minInclusive) && math.all(p < maxExclusive)) count++;
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(minimumCells),
                $"Pinned mountain-dragon {label} region lost required baked anatomy.");
        }
    }
}
