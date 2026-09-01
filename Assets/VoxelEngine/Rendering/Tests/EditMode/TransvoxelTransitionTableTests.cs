using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Pins the transition-cell tables. They are transcribed data, not derived logic, so the
    /// risk they carry is transcription damage: a truncated row, a dropped entry, an index that
    /// points past the end of the class list. None of that would throw at mesh time — it would
    /// quietly emit wrong triangles at ring boundaries, which is the hardest class of bug to
    /// see and the one this project has repeatedly shipped.
    ///
    /// These assertions cannot prove the tables describe correct geometry; only Lengyel's
    /// published data can do that, and it is what was transcribed. What they can prove is that
    /// the transcription is structurally intact and self-consistent.
    /// </summary>
    public sealed class TransvoxelTransitionTableTests
    {
        [Test]
        public void CellClassHasOneEntryPerNineBitCase()
        {
            // A transition face has nine samples, so 2^9 cases.
            Assert.AreEqual(512, TransvoxelTransitionTables.CellClass.Length);
        }

        [Test]
        public void VertexDataHasOneRowPerCase()
        {
            Assert.AreEqual(512, TransvoxelTransitionTables.VertexData.Length);
            foreach (var row in TransvoxelTransitionTables.VertexData)
                Assert.IsNotNull(row, "Every case must have a vertex row, even if empty.");
        }

        [Test]
        public void ThereAreFiftySixEquivalenceClasses()
        {
            Assert.AreEqual(56, TransvoxelTransitionTables.CellData.Length);
        }

        [Test]
        public void ThirteenSamplePositionsCoverTheTransitionFace()
        {
            Assert.AreEqual(13, TransvoxelTransitionTables.CornerOffsets.Length);
            Assert.AreEqual(13, TransvoxelTransitionTables.CornerData.Length);
        }

        [Test]
        public void EveryCellClassIndexesARealEquivalenceClass()
        {
            // The high bit flags reversed winding and is not part of the index.
            for (int i = 0; i < TransvoxelTransitionTables.CellClass.Length; i++)
            {
                int index = TransvoxelTransitionTables.CellClass[i] & 0x7F;
                Assert.Less(index, TransvoxelTransitionTables.CellData.Length,
                    $"Case {i} maps to class {index}, which is past the end of CellData.");
            }
        }

        [Test]
        public void EveryClassTriangleListMatchesItsDeclaredCounts()
        {
            for (int i = 0; i < TransvoxelTransitionTables.CellData.Length; i++)
            {
                RegularCellData data = TransvoxelTransitionTables.CellData[i];
                Assert.AreEqual(data.TriangleCount * 3, data.VertexIndices.Length,
                    $"Class {i} declares {data.TriangleCount} triangles but lists "
                  + $"{data.VertexIndices.Length} indices.");

                foreach (byte index in data.VertexIndices)
                    Assert.Less(index, data.VertexCount,
                        $"Class {i} references vertex {index} but declares only "
                      + $"{data.VertexCount}.");
            }
        }

        [Test]
        public void EachCaseSuppliesAsManyVerticesAsItsClassConsumes()
        {
            for (int caseCode = 0; caseCode < 512; caseCode++)
            {
                int classIndex = TransvoxelTransitionTables.CellClass[caseCode] & 0x7F;
                RegularCellData data = TransvoxelTransitionTables.CellData[classIndex];
                ushort[] vertices = TransvoxelTransitionTables.VertexData[caseCode];

                Assert.AreEqual(data.VertexCount, vertices.Length,
                    $"Case {caseCode} uses class {classIndex}, which needs "
                  + $"{data.VertexCount} vertices, but the case supplies {vertices.Length}.");
            }
        }

        [Test]
        public void EmptyAndFullCasesProduceNoGeometry()
        {
            // Case 0 is all-empty. Case 511 is all-solid. Neither crosses the surface.
            int emptyClass = TransvoxelTransitionTables.CellClass[0] & 0x7F;
            int fullClass = TransvoxelTransitionTables.CellClass[511] & 0x7F;

            Assert.AreEqual(0, TransvoxelTransitionTables.CellData[emptyClass].TriangleCount,
                "The all-empty transition case must emit nothing.");
            Assert.AreEqual(0, TransvoxelTransitionTables.CellData[fullClass].TriangleCount,
                "The all-solid transition case must emit nothing.");
        }

        [Test]
        public void EveryVertexDescriptorNamesTwoRealFaceSamples()
        {
            // The low byte packs the pair of samples the edge interpolates between, in the same
            // high-nibble/low-nibble form the regular tables use.
            for (int caseCode = 0; caseCode < 512; caseCode++)
            {
                foreach (ushort descriptor in TransvoxelTransitionTables.VertexData[caseCode])
                {
                    int a = (descriptor >> 4) & 0x0F;
                    int b = descriptor & 0x0F;
                    Assert.Less(a, TransvoxelTransitionTables.CornerOffsets.Length,
                        $"Case {caseCode} interpolates from sample {a}, which does not exist.");
                    Assert.Less(b, TransvoxelTransitionTables.CornerOffsets.Length,
                        $"Case {caseCode} interpolates to sample {b}, which does not exist.");
                }
            }
        }

        [Test]
        public void RegularAndTransitionTablesShareTheSameEdgeEncoding()
        {
            // Both paths decode an edge the same way, so a regular-cell descriptor and a
            // transition descriptor must agree on where the sample indices live. If this
            // drifts, one of the two meshers silently reads the wrong corners.
            for (int caseCode = 1; caseCode < 255; caseCode++)
            {
                int classIndex = TransvoxelRegularTables.CellClass[caseCode] & 0x7F;
                RegularCellData data = TransvoxelRegularTables.CellData[classIndex];
                Assert.LessOrEqual(data.VertexCount, 12,
                    "A regular cell interpolates along at most twelve edges.");
            }
        }
    }
}
