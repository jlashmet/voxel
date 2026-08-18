using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Immutable Burst-friendly Transvoxel lookup data. A scheduler owns exactly one bundle and
    /// all of its build workspaces borrow these arrays read-only. Standalone cache instances used
    /// by focused tests own a private bundle. No worker duplicates these immutable tables.
    /// </summary>
    internal sealed class TransvoxelLookupTables : IDisposable
    {
        internal readonly NativeArray<byte> RegularCellClass;
        internal readonly NativeArray<byte> RegularGeometryCounts;
        internal readonly NativeArray<byte> RegularCellVertexIndices;
        internal readonly NativeArray<ushort> RegularEdgeCodes;

        internal readonly NativeArray<byte> TransitionCellClass;
        internal readonly NativeArray<byte> TransitionGeometryCounts;
        internal readonly NativeArray<byte> TransitionCellIndices;
        internal readonly NativeArray<ushort> TransitionVertexData;
        internal readonly int TransitionVertexStride;
        internal readonly int TransitionIndexStride;

        internal TransvoxelLookupTables()
        {
            RegularCellClass = new NativeArray<byte>(
                TransvoxelRegularTables.CellClass.Length, Allocator.Persistent);
            RegularCellClass.CopyFrom(TransvoxelRegularTables.CellClass);

            RegularGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            RegularCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length * TransvoxelTopologyJob.MaxIndicesPerCell,
                Allocator.Persistent);
            for (int cellClass = 0; cellClass < TransvoxelRegularTables.CellData.Length;
                 cellClass++)
            {
                RegularCellData data = TransvoxelRegularTables.CellData[cellClass];
                RegularGeometryCounts[cellClass] = data.GeometryCounts;
                int length = math.min(data.VertexIndices.Length,
                                      TransvoxelTopologyJob.MaxIndicesPerCell);
                for (int i = 0; i < length; i++)
                    RegularCellVertexIndices[
                        cellClass * TransvoxelTopologyJob.MaxIndicesPerCell + i] =
                        data.VertexIndices[i];
            }

            RegularEdgeCodes = new NativeArray<ushort>(
                TransvoxelRegularTables.VertexData.Length * 12, Allocator.Persistent);
            for (int cell = 0; cell < TransvoxelRegularTables.VertexData.Length; cell++)
            {
                ushort[] edges = TransvoxelRegularTables.VertexData[cell];
                int length = math.min(edges.Length, 12);
                for (int i = 0; i < length; i++) RegularEdgeCodes[cell * 12 + i] = edges[i];
            }

            byte[] transitionClasses = TransvoxelTransitionTables.CellClass;
            RegularCellData[] transitionData = TransvoxelTransitionTables.CellData;
            ushort[][] transitionVertices = TransvoxelTransitionTables.VertexData;

            int vertexStride = 0;
            for (int i = 0; i < transitionVertices.Length; i++)
                vertexStride = math.max(vertexStride, transitionVertices[i].Length);
            int indexStride = 0;
            for (int i = 0; i < transitionData.Length; i++)
                indexStride = math.max(indexStride, transitionData[i].VertexIndices.Length);
            TransitionVertexStride = vertexStride;
            TransitionIndexStride = indexStride;

            TransitionCellClass = new NativeArray<byte>(
                transitionClasses.Length, Allocator.Persistent);
            for (int i = 0; i < transitionClasses.Length; i++)
                TransitionCellClass[i] = transitionClasses[i];

            TransitionGeometryCounts = new NativeArray<byte>(
                transitionData.Length, Allocator.Persistent);
            TransitionCellIndices = new NativeArray<byte>(
                transitionData.Length * math.max(1, indexStride), Allocator.Persistent);
            for (int i = 0; i < transitionData.Length; i++)
            {
                TransitionGeometryCounts[i] = transitionData[i].GeometryCounts;
                byte[] indices = transitionData[i].VertexIndices;
                for (int j = 0; j < indices.Length; j++)
                    TransitionCellIndices[i * indexStride + j] = indices[j];
            }

            TransitionVertexData = new NativeArray<ushort>(
                transitionVertices.Length * math.max(1, vertexStride), Allocator.Persistent);
            for (int i = 0; i < transitionVertices.Length; i++)
            {
                ushort[] row = transitionVertices[i];
                for (int j = 0; j < row.Length; j++)
                    TransitionVertexData[i * vertexStride + j] = row[j];
            }
        }

        public void Dispose()
        {
            if (RegularCellClass.IsCreated) RegularCellClass.Dispose();
            if (RegularGeometryCounts.IsCreated) RegularGeometryCounts.Dispose();
            if (RegularCellVertexIndices.IsCreated) RegularCellVertexIndices.Dispose();
            if (RegularEdgeCodes.IsCreated) RegularEdgeCodes.Dispose();
            if (TransitionCellClass.IsCreated) TransitionCellClass.Dispose();
            if (TransitionGeometryCounts.IsCreated) TransitionGeometryCounts.Dispose();
            if (TransitionCellIndices.IsCreated) TransitionCellIndices.Dispose();
            if (TransitionVertexData.IsCreated) TransitionVertexData.Dispose();
        }
    }
}
