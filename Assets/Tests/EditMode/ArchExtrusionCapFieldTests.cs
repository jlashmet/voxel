using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.Emitters;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchExtrusionCapFieldTests
    {
        [Test]
        public void RearCapClampHidesDiagonalRimDistanceThatInteriorSampleRecovers()
        {
            Primitive annulus = CurvedPrimitiveEmitter.Annulus(
                centre: int3.zero,
                outerRadius: 23,
                innerRadius: 16,
                depth: 12,
                axis: 2,
                half: false,
                material: 1,
                style: SurfaceStyles.MasonryJoint,
                mode: PrimitiveMode.Fill,
                order: 0);

            int3 cap = new(16, 6, annulus.B.z);
            int3 diagonalOpening = cap + new int3(-1, -1, 0);

            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, cap), Is.True);
            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, cap + new int3(-1, 0, 0)), Is.True);
            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, cap + new int3(1, 0, 0)), Is.True);
            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, cap + new int3(0, -1, 0)), Is.True);
            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, cap + new int3(0, 1, 0)), Is.True);
            Assert.That(CurvedPrimitiveEmitter.Contains(in annulus, diagonalOpening), Is.False,
                "This is a diagonal-only intrados crossing: the analytic contour cuts the cap neighbourhood even though every axial X/Y neighbour remains solid.");

            Assert.That(CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(in annulus, cap, out int capQ4), Is.True);
            int3 twoVoxelsInside = cap + new int3(0, 0, -2);
            Assert.That(CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                in annulus, twoVoxelsInside, out int interiorQ4), Is.True);

            Assert.That(capQ4, Is.EqualTo(8),
                "The planar rear cap clamps the stored combined SDF to exactly half a voxel.");
            Assert.That(interiorQ4, Is.GreaterThan(16),
                "Two voxels inside the extrusion, the cap no longer wins the min() and the same X/Y sample exposes the much larger radial signed distance.");

            VoxelBoundarySample capBoundary = VoxelBoundarySample.FromSignedQ4(capQ4, extrusionAxis: 2);
            VoxelBoundarySample recoveredProfile =
                VoxelBoundarySample.FromSignedQ4(interiorQ4, extrusionAxis: 2);
            Assert.That(capBoundary.SignedQ3, Is.EqualTo(4));
            Assert.That(recoveredProfile.SignedQ3, Is.GreaterThan(8),
                "The hidden transverse correction is more than one voxel at this real arch-scale diagonal rim cell.");

            float trueRadialDistance = math.sqrt(16f * 16f + 6f * 6f) - 15.5f;
            Assert.That(trueRadialDistance, Is.GreaterThan(1f));
            Assert.That(recoveredProfile.SignedQ3 * 0.125f,
                Is.EqualTo(trueRadialDistance).Within(0.125f),
                "The interior authored sample retains enough precision to recover the analytic intrados even though the cap-layer sample does not.");

            float3 gridPinned = new(16f, 6f, annulus.B.z + 0.5f);
            float3 unchanged = TransvoxelTopologyJob.ProjectExtrusionCapRim(
                gridPinned, edgeAxis: 2, capBoundary, densityNormal: new float3(0f, 0f, 1f));
            Assert.That(math.distance(unchanged, gridPinned), Is.LessThan(1e-6f),
                "With the cap-clamped field all transverse gradient components can vanish, so the current projection cannot move this diagonal rim vertex off the voxel lattice.");
        }
    }
}
