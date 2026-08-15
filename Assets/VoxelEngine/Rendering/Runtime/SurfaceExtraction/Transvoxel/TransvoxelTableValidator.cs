using System;
using UnityEngine;

namespace VoxelEngine.Rendering.SurfaceExtraction.Transvoxel
{
    /// <summary>
    /// Fails immediately if the vendored lookup data was truncated or corrupted. These tables are
    /// topology, not tuning data: silently accepting one missing case would produce a geometry
    /// hole that looks exactly like a streaming bug.
    /// </summary>
    internal static class TransvoxelTableValidator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Validate()
        {
            if (TransvoxelRegularTables.CellClass.Length != 256)
                throw new InvalidOperationException(
                    $"Transvoxel CellClass must contain 256 cases, found {TransvoxelRegularTables.CellClass.Length}.");
            if (TransvoxelRegularTables.VertexData.Length != 256)
                throw new InvalidOperationException(
                    $"Transvoxel VertexData must contain 256 cases, found {TransvoxelRegularTables.VertexData.Length}.");
            if (TransvoxelRegularTables.CellData.Length != 16)
                throw new InvalidOperationException(
                    $"Transvoxel regular CellData must contain 16 classes, found {TransvoxelRegularTables.CellData.Length}.");

            for (int caseCode = 0; caseCode < 256; caseCode++)
            {
                int cellClass = TransvoxelRegularTables.CellClass[caseCode];
                if ((uint)cellClass >= (uint)TransvoxelRegularTables.CellData.Length)
                    throw new InvalidOperationException($"Transvoxel case {caseCode} has invalid class {cellClass}.");

                RegularCellData data = TransvoxelRegularTables.CellData[cellClass];
                ushort[] vertices = TransvoxelRegularTables.VertexData[caseCode];
                if (vertices.Length < data.VertexCount)
                    throw new InvalidOperationException(
                        $"Transvoxel case {caseCode} needs {data.VertexCount} vertices but has {vertices.Length} edge codes.");

                for (int i = 0; i < data.VertexCount; i++)
                {
                    int c0 = (vertices[i] >> 4) & 0x0F;
                    int c1 = vertices[i] & 0x0F;
                    if (c0 >= 8 || c1 >= 8)
                        throw new InvalidOperationException(
                            $"Transvoxel case {caseCode} edge {i} references cube corners {c0}/{c1}.");
                }

                for (int i = 0; i < data.VertexIndices.Length; i++)
                    if (data.VertexIndices[i] >= data.VertexCount)
                        throw new InvalidOperationException(
                            $"Transvoxel class {cellClass} index {data.VertexIndices[i]} exceeds vertex count {data.VertexCount}.");
            }
        }
    }
}
