using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuFarCoverageDispatcherTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct QueryBounds
        {
            public Vector4 Min, Max;
            public QueryBounds(Vector3 min, Vector3 max) { Min=min; Max=max; }
        }

        [Test]
        public void ReplacementRequiresCompletedDiscoveryAndCurrentProductionGpuSelection()
        {
            var pageShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            var drawShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfaceDrawCompact"));
            var discoveryShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfaceDiscovery"));
            var coverageShader = Object.Instantiate(Resources.Load<ComputeShader>("GpuFarCoverage"));
            try
            {
                using var arena = new GpuSurfacePageArena(pageShader,4096,8192,16);
                using var draw = new GpuSurfaceDrawDispatcher(drawShader,arena);
                using var coverage = new GpuFarCoverageDispatcher(coverageShader,discoveryShader,128);
                var live = new uint[16*8]; live[0]=1; live[3]=3; live[4]=3; live[5]=1; live[6]=1; live[7]=1;
                arena.LiveChunkGeometry.SetData(live);
                var key = new SurfaceLodNodeKey(8,int3.zero);
                var drawable = new List<SurfaceLodNodeKey>{key};
                var complete = new List<SurfaceLodNodeKey>{key};
                var handles = new List<int>{0};
                var discovery = new SurfaceDiscoveryCoverage();
                discovery.Begin(int3.zero); discovery.AddSurfaceBlock(int3.zero);
                var bounds = new []
                {
                    new QueryBounds(Vector3.zero,Vector3.one*64),
                    new QueryBounds(new Vector3(64,0,0),new Vector3(128,64,64)),
                    new QueryBounds(new Vector3(-1,0,0),Vector3.one),
                    new QueryBounds(Vector3.zero,Vector3.one*65536),
                    new QueryBounds(Vector3.one*1000001,Vector3.one*1000002),
                    new QueryBounds(Vector3.one,Vector3.zero)
                };
                using var queries = new ComputeBuffer(bounds.Length,32);
                using var results = new ComputeBuffer(bounds.Length,4);
                queries.SetData(bounds);
                var actual = new uint[bounds.Length];
                for(int phase=0;phase<9;phase++)
                {
                    if(phase==1)discovery.Complete(int3.zero);
                    if(phase==2)complete.Clear();
                    if(phase==3){complete.Add(key);live[7]=0;arena.LiveChunkGeometry.SetData(live);}
                    if(phase==4){live[7]=1;arena.LiveChunkGeometry.SetData(live);}
                    if(phase==6)discovery.Invalidate(int3.zero);
                    if(phase==7){discovery.Begin(int3.zero);discovery.Complete(int3.zero);}
                    if(phase==8)discovery.Forget(int3.zero);
                    draw.PrepareLod(drawable,handles,complete,phase,drawable);
                    coverage.Prepare(discovery,draw.ActiveLodNodes,draw.LodNodeCount,
                        draw.ActiveLodSelection,draw.ActiveLodState,phase,phase!=4,Vector3.zero,1,1000000);
                    coverage.Query(queries,results,bounds.Length);
                    results.GetData(actual);
                    bool ready = phase==1 || phase==5 || phase==7;
                    bool empty = phase==1 || phase==2 || phase==3 || phase==5 || phase==7;
                    CollectionAssert.AreEqual(new uint[]{ready?1u:0u,empty?1u:0u,0,0,0,0},actual,$"Phase {phase}");
                    if(phase==2)
                    {
                        var planes=new Plane[6];planes[0]=new Plane(Vector3.right,-1000);
                        coverage.Prepare(discovery,draw.ActiveLodNodes,draw.LodNodeCount,
                            draw.ActiveLodSelection,draw.ActiveLodState,phase,true,Vector3.zero,1,1000000,planes);
                        coverage.Query(queries,results,bounds.Length);results.GetData(actual);
                        CollectionAssert.AreEqual(new uint[]{1,1,0,0,0,0},actual,
                            "Offscreen cells need no selected draw, but unknown regions still reject replacement.");
                    }
                }
                // Discovery is deliberately conservative: a completed empty extraction is
                // another valid proof, but only while its production GPU ring owns the node.
                discovery.Begin(int3.zero);discovery.AddSurfaceBlock(int3.zero);discovery.Complete(int3.zero);
                var noDrawable=new List<SurfaceLodNodeKey>();var noHandles=new List<int>();
                draw.PrepareLod(noDrawable,noHandles,complete,10,drawable);
                coverage.Prepare(discovery,draw.ActiveLodNodes,draw.LodNodeCount,
                    draw.ActiveLodSelection,draw.ActiveLodState,10,true,Vector3.zero,1,1000000);
                coverage.Query(queries,results,bounds.Length);results.GetData(actual);
                CollectionAssert.AreEqual(new uint[]{1,1,0,0,0,0},actual,"Current empty extraction must replace conservative discovery.");
                var bands=new[]{new Vector4(0,1000000,0,0),new Vector4(0,1000000,0,0),
                    new Vector4(0,1000000,0,0),new Vector4(0,1000000,1,0)};
                draw.PrepareLod(noDrawable,noHandles,complete,11,drawable,null,1,bands);
                coverage.Prepare(discovery,draw.ActiveLodNodes,draw.LodNodeCount,
                    draw.ActiveLodSelection,draw.ActiveLodState,11,true,Vector3.zero,1,1000000);
                coverage.Query(queries,results,bounds.Length);results.GetData(actual);
                CollectionAssert.AreEqual(new uint[]{0,1,0,0,0,0},actual,"Out-of-band empty publication is not a replacement proof.");
                // Cross the dispatch row boundary; every output is initialized with a sentinel.
                discovery.Begin(int3.zero);discovery.Complete(int3.zero);
                coverage.Prepare(discovery,draw.ActiveLodNodes,draw.LodNodeCount,
                    draw.ActiveLodSelection,draw.ActiveLodState,10,true,Vector3.zero,1,1000000);
                var many=new QueryBounds[1025];var sentinels=new uint[1025];
                for(int i=0;i<many.Length;i++){many[i]=new QueryBounds(Vector3.zero,Vector3.one);sentinels[i]=uint.MaxValue;}
                many[0]=new QueryBounds(Vector3.one,Vector3.zero);
                using var manyQueries=new ComputeBuffer(many.Length,32);
                using var manyResults=new ComputeBuffer(many.Length,4);
                manyQueries.SetData(many);manyResults.SetData(sentinels);
                coverage.Query(manyQueries,manyResults,many.Length);manyResults.GetData(sentinels);
                for(int i=0;i<sentinels.Length;i++)Assert.AreEqual(i==0?0u:1u,sentinels[i],$"Query {i}");
            }
            finally
            {
                Object.DestroyImmediate(pageShader);Object.DestroyImmediate(drawShader);
                Object.DestroyImmediate(discoveryShader);Object.DestroyImmediate(coverageShader);
            }
        }
    }
}
