using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using UnityEngine.Rendering;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Runtime.FarWorld
{
    /// <summary>One bounded GPU visibility/argument dispatch for all far batches in a source set.</summary>
    internal sealed class GpuFarDrawSet : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct QueryBounds { internal Vector4 Min, Max; }
        private readonly ComputeBuffer _bounds, _replacement, _counts;
        private double _nextDiagnosticTime;
        private AsyncGPUReadbackRequest _request;
        private readonly Action<AsyncGPUReadbackRequest> _receiveCounts;
        private bool _pending, _disposed;
        private GpuFarMaterialDraws _materialDraws;
        private GpuFarSelectionDispatcher _selection;
        private readonly ulong[] _ids;
        internal bool Prepared { get; private set; }
        internal void UseLegacyDraw() => Prepared = false;
        internal void Record(CommandBuffer command) => _materialDraws.Record(command);
        internal int Count { get; }
        // Diagnostics only: delayed counts must never decide which batches are submitted.
        internal int VisibleCount { get; private set; }
        internal int NearCount { get; private set; }

        internal GpuFarDrawSet(IReadOnlyList<GpuFarInstanceBatch> batches, IReadOnlyList<Bounds> bounds,
                               IReadOnlyList<FarFeatureVisualFlags> flags = null, IReadOnlyList<ulong> ids = null)
        {
            _receiveCounts=ReceiveCounts;
            if(ids != null)
            {
                if(ids.Count != bounds.Count)throw new ArgumentException("IDs must match bounds.");
                _ids = new ulong[ids.Count];
                for(int i=0;i<ids.Count;i++)_ids[i]=ids[i];
            }
            Count=bounds.Count; VisibleCount=Count;
            if(Count==0 || batches.Count==0)throw new ArgumentException("A nonempty far source set is required.");
            int first=0;
            foreach(var batch in batches)first=checked(first+batch.Count);
            if(first!=Count)throw new ArgumentException("Batch bounds do not match source membership.");
            var query=new QueryBounds[Count];
            if(flags != null && flags.Count != Count)throw new ArgumentException("Flags must match bounds.");
            for(int i=0;i<Count;i++)
            {
                query[i]=new QueryBounds{Min=bounds[i].min,Max=bounds[i].max};
                query[i].Min.w=flags == null ? 0 : (byte)flags[i];
            }
            try
            {
                _bounds=new ComputeBuffer(Count,32);_replacement=new ComputeBuffer(Count,4);
                _counts=new ComputeBuffer(2,4);
                _bounds.SetData(query);
                _materialDraws = new GpuFarMaterialDraws(batches);
            }
            catch{Dispose();throw;}
        }
        internal void InheritSelection(GpuFarDrawSet previous)
        {
            if(previous?._selection == null || previous._ids == null || _ids == null)return;
            var indices = new Dictionary<ulong,int>();
            for(int i=0;i<previous._ids.Length;i++)indices[previous._ids[i]]=i;
            var remap = new int[Count];
            for(int i=0;i<Count;i++)remap[i]=indices.TryGetValue(_ids[i],out int source)?source:-1;
            _selection=new GpuFarSelectionDispatcher(Count);
            _selection.Inherit(previous._selection,remap);
        }

        internal void Prepare(GpuFarCoverageDispatcher coverage, FarFeatureSelectionSettings? settings = null,
                              float3 camera = default, float radius = 0, float voxelSize = 0)
        {
            if(_disposed)throw new ObjectDisposedException(nameof(GpuFarDrawSet));
            if(coverage != null)coverage.Query(_bounds,_replacement,Count);
            else
            {
                _selection ??= new GpuFarSelectionDispatcher(Count);
                _selection.ClearReplacement(_replacement);
            }
            if(settings.HasValue)
            {
                _selection ??= new GpuFarSelectionDispatcher(Count);
                _selection.Select(_bounds, _replacement, settings.Value, camera, radius, voxelSize);
            }
            _materialDraws.Prepare(_replacement); Prepared = true;
            // Read back only aggregate diagnostics, at most ten times per second.
            // Presentation always consumes the current GPU arguments above.
            double now=Time.realtimeSinceStartupAsDouble;
            if(!_pending && now>=_nextDiagnosticTime)
            {
                _nextDiagnosticTime=now+0.1;
                _materialDraws.CountVisible(_replacement,_counts, true);
                _pending=true;
                _request=AsyncGPUReadback.Request(_counts,_receiveCounts);
            }
        }
        private void ReceiveCounts(AsyncGPUReadbackRequest request)
        {
            _pending=false;
            if(_disposed || request.hasError)return;
            var counts=request.GetData<uint>();
            VisibleCount=(int)counts[0];
            NearCount=(int)counts[1];
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            if(_pending)_request.WaitForCompletion();
            _selection?.Dispose();
            _materialDraws?.Dispose();
            _bounds?.Release();_replacement?.Release();_counts?.Release();
        }
    }
}
