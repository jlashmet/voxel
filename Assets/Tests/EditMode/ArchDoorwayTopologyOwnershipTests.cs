using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchDoorwayTopologyOwnershipTests
    {
        [Test]
        public void RetainedProfileOwnsTriangle_WhenTriangleBridgesClearOpeningIntoAnnulus()
        {
            var block = new ProfileBlock
            {
                Centre = int3.zero,
                InnerRadiusQ4 = 14 * 16,
                OuterRadiusQ4 = 21 * 16,
                FrontQ4 = -8,
                BackQ4 = 12 * 16 + 8,
                StartDirection = new int2(4096, 0),
                EndDirection = new int2(0, 4096),
                Axis = 2,
                Material = 9,
                JointHalfWidthQ4 = 4,
            };

            float3 apertureA = new float3(8f, 4f, 6f);
            float3 annulus = new float3(18f, 4f, 6f);
            float3 apertureB = new float3(8f, 6f, 6.2f);
            float3 centroid = (apertureA + annulus + apertureB) / 3f;
            Assert.Less(math.length(centroid.xy), 14f - 0.55f,
                "the regression must keep the centroid inside the clear opening so centroid-only ownership cannot pass it");

            Assert.True(CpuTransvoxelChunkCache.RetainedProfileOwnsTriangle(
                in block, apertureA, annulus, apertureB, 9),
                "a same-material topology triangle that crosses the intrados into the retained annulus must be removed instead of spanning the clear doorway");
        }
    }
}
