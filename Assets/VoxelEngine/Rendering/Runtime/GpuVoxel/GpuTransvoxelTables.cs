using System;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// The Transvoxel lookup tables, on the GPU.
    ///
    /// These are the same tables the CPU mesher uses, widened from byte/ushort to the 32-bit words a
    /// structured buffer takes. Widening rather than repacking is deliberate: the tables are a few
    /// tens of kilobytes and uploaded once, so there is nothing to win by making the shader unpack
    /// them, and a packing bug in a lookup table is uniquely hard to spot — it produces geometry
    /// that is wrong only for particular cell cases.
    /// </summary>
    public sealed class GpuTransvoxelTables : IDisposable
    {
        /// <summary>Indices a regular cell class may reference. Matches the CPU's cell stride.</summary>
        public const int IndicesPerCellClass = 15;

        /// <summary>Edge codes per case code.</summary>
        public const int EdgesPerCase = 12;

        private readonly ComputeBuffer _cellClass;
        private readonly ComputeBuffer _geometryCounts;
        private readonly ComputeBuffer _cellIndices;
        private readonly ComputeBuffer _edgeCodes;
        private readonly ComputeBuffer _transitionCellClass;
        private readonly ComputeBuffer _transitionGeometryCounts;
        private readonly ComputeBuffer _transitionCellIndices;
        private readonly ComputeBuffer _transitionVertexData;
        private bool _disposed;

        /// <summary>Row strides of the transition tables, which are not a fixed 15 like the regular ones.</summary>
        public int TransitionVertexStride { get; }
        public int TransitionIndexStride { get; }

        public ComputeBuffer CellClass => _cellClass;
        public ComputeBuffer GeometryCounts => _geometryCounts;
        public ComputeBuffer CellIndices => _cellIndices;
        public ComputeBuffer EdgeCodes => _edgeCodes;
        public ComputeBuffer TransitionCellClass => _transitionCellClass;
        public ComputeBuffer TransitionGeometryCounts => _transitionGeometryCounts;
        public ComputeBuffer TransitionCellIndices => _transitionCellIndices;
        public ComputeBuffer TransitionVertexData => _transitionVertexData;

        /// <summary>
        /// Builds the GPU tables from the same source the CPU mesher uses.
        ///
        /// The CPU table type is internal and owns native memory, so it is constructed, copied and
        /// released here rather than handed around: the GPU copy is the only long-lived one, and
        /// having two owners of the same tables is how they drift apart.
        /// </summary>
        public static GpuTransvoxelTables CreateDefault()
        {
            var source = new TransvoxelLookupTables();
            try
            {
                return new GpuTransvoxelTables(source);
            }
            finally
            {
                source.Dispose();
            }
        }

        internal GpuTransvoxelTables(TransvoxelLookupTables source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _cellClass = Widen(source.RegularCellClass.ToArray());
            _geometryCounts = Widen(source.RegularGeometryCounts.ToArray());
            _cellIndices = Widen(source.RegularCellVertexIndices.ToArray());
            _edgeCodes = Widen(source.RegularEdgeCodes.ToArray());

            _transitionCellClass = Widen(source.TransitionCellClass.ToArray());
            _transitionGeometryCounts = Widen(source.TransitionGeometryCounts.ToArray());
            _transitionCellIndices = Widen(source.TransitionCellIndices.ToArray());
            _transitionVertexData = Widen(source.TransitionVertexData.ToArray());
            TransitionVertexStride = source.TransitionVertexStride;
            TransitionIndexStride = source.TransitionIndexStride;
        }

        private static ComputeBuffer Widen(byte[] source)
        {
            var words = new uint[source.Length];
            for (int i = 0; i < source.Length; i++) words[i] = source[i];
            return Upload(words);
        }

        private static ComputeBuffer Widen(ushort[] source)
        {
            var words = new uint[source.Length];
            for (int i = 0; i < source.Length; i++) words[i] = source[i];
            return Upload(words);
        }

        private static ComputeBuffer Upload(uint[] words)
        {
            var buffer = new ComputeBuffer(Mathf.Max(1, words.Length), sizeof(uint),
                                           ComputeBufferType.Structured);
            if (words.Length > 0) buffer.SetData(words);
            return buffer;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cellClass?.Release();
            _geometryCounts?.Release();
            _cellIndices?.Release();
            _edgeCodes?.Release();
            _transitionCellClass?.Release();
            _transitionGeometryCounts?.Release();
            _transitionCellIndices?.Release();
            _transitionVertexData?.Release();
        }
    }
}
