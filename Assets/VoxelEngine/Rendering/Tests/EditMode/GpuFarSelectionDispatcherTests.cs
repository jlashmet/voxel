using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuFarSelectionDispatcherTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct QueryBounds { public float4 Min, Max; }

        [TestCase(1)]
        [TestCase(65)]
        [TestCase(1025)]
        public void ResidentSelectionMatchesProductionPolicyAcrossCamerasAndRetainsReplacement(int count)
        {
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24,18,4,3,1.5f,1),
                new FarFeatureSelectionPolicy.DistanceCaps(3000,5000,9000),60,1080);
            var random = new System.Random(98131);
            var bounds = new QueryBounds[count];
            var centers = new float3[count]; var extents = new float3[count];
            var importance = new FarFeatureImportance[count];
            var replacement = new uint[count]; var actual = new uint[count]; var tiers = new uint[count];
            for(int i=0;i<count;i++)
            {
                centers[i]=new float3(random.Next(-6000,6000),random.Next(-500,500),random.Next(-6000,6000));
                extents[i]=new float3((float)random.NextDouble()*60f+0.1f);
                importance[i]=(FarFeatureImportance)(i%3);
                int flags=i%3==0?0:i%3==1?2:6;
                bounds[i]=new QueryBounds {Min=new float4(centers[i]-extents[i],flags),Max=new float4(centers[i]+extents[i],0)};
            }
            using var queries=new ComputeBuffer(count,32);
            using var results=new ComputeBuffer(count,4);
            using var selection=new GpuFarSelectionDispatcher(count);
            queries.SetData(bounds);
            for(int frame=0;frame<16;frame++)
            {
                float3 camera=frame==8?new float3(20000):new float3(frame*97,0,frame*-143);
                for(int i=0;i<count;i++)replacement[i]=(uint)((i+frame)%2);
                results.SetData(replacement);
                selection.Select(queries,results,policy.GpuSettings,camera,100000,0.1f);
                results.GetData(actual);selection.PreviousTier.GetData(tiers);
                for(int i=0;i<count;i++)
                {
                    var expected=policy.Select((ulong)i,centers[i],extents[i],camera,importance[i]);
                    Assert.AreEqual((uint)expected,tiers[i],$"frame {frame}, instance {i}");
                    Assert.AreEqual(replacement[i] | (expected==FarFeatureTier.Culled?2u:0u),actual[i]);
                }
            }
        }

        [Test]
        public void PaddedSourcesObeyActiveQueryAndGpuHistorySurvivesMembershipRemapping()
        {
            var settings=new FarFeatureSelectionSettings(new float4(24,18,4,3),new float2(1.5f,1),new float3(1000),60,1080);
            using var queries=new ComputeBuffer(3,32);
            using var result=new ComputeBuffer(3,4);
            using var selection=new GpuFarSelectionDispatcher(3);
            queries.SetData(new [] {
                new QueryBounds{Min=new float4(-1,-1,-1,0),Max=new float4(1,1,1,0)},
                new QueryBounds{Min=new float4(101,-1,-1,2),Max=new float4(102,1,1,0)},
                new QueryBounds{Min=new float4(-102,-1,-1,2),Max=new float4(-100,1,1,0)} });
            result.SetData(new uint[3]);
            selection.Select(queries,result,settings,float3.zero,100,1);
            var visibility=new uint[3];result.GetData(visibility);
            CollectionAssert.AreEqual(new uint[]{0,2,2},visibility,
                "Resident padding must not increase the exact active spatial query.");
            // Move the camera without uploading bounds: previously absent sources become selectable.
            result.SetData(new uint[3]);
            selection.Select(queries,result,settings,new float3(100,0,0),100,1);
            var prior=new uint[3];selection.PreviousTier.GetData(prior);
            Assert.That(prior[1],Is.EqualTo((uint)FarFeatureTier.Mid));
            for(int lifetime=0;lifetime<4;lifetime++)
            {
                var next=new GpuFarSelectionDispatcher(4);
                next.Inherit(selection,new[]{1,-1,0,2});
                var remapped=new uint[4];next.PreviousTier.GetData(remapped);
                CollectionAssert.AreEqual(new[]{prior[1],0u,prior[0],prior[2]},remapped);
                next.Dispose();next.Dispose();
                Assert.Throws<ObjectDisposedException>(()=>next.Inherit(selection,new[]{1,-1,0,2}));
            }
        }
    }
}
