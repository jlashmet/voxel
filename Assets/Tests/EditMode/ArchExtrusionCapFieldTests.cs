using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
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
            int3 oneVoxelInside = cap + new int3(0, 0, -1);
            int3 twoVoxelsInside = cap + new int3(0, 0, -2);
            Assert.That(CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                in annulus, oneVoxelInside, out int inwardQ4), Is.True);
            Assert.That(CurvedPrimitiveEmitter.TryBoundaryDistanceQ4(
                in annulus, twoVoxelsInside, out int interiorQ4), Is.True);

            Assert.That(capQ4, Is.EqualTo(8),
                "The planar rear cap clamps the stored combined SDF to exactly half a voxel.");
            Assert.That(interiorQ4, Is.GreaterThan(16),
                "Two voxels inside the extrusion, the cap no longer wins the min() and the same X/Y sample exposes the much larger radial signed distance.");

            VoxelBoundarySample capBoundary = VoxelBoundarySample.FromSignedQ4(capQ4, extrusionAxis: 2);
            VoxelBoundarySample inwardBoundary = VoxelBoundarySample.FromSignedQ4(inwardQ4, extrusionAxis: 2);
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
                "With the cap-clamped field all transverse gradient components can vanish, so the old cap-layer projection cannot move this diagonal rim vertex off the voxel lattice.");

            VoxelBoundarySample resolved = TransvoxelTopologyJob.ResolveExtrusionCapProfileSample(
                capBoundary, inwardBoundary, recoveredProfile,
                edgeAxis: 2, inPlaneBoundary: true);
            Assert.That(resolved.Packed, Is.EqualTo(recoveredProfile.Packed),
                "A proven cap-boundary cell must recover the least-clamped compatible authored profile sample from inside the extrusion.");

            VoxelBoundarySample interiorCap = TransvoxelTopologyJob.ResolveExtrusionCapProfileSample(
                capBoundary, inwardBoundary, recoveredProfile,
                edgeAxis: 2, inPlaneBoundary: false);
            Assert.That(interiorCap.IsAuthored, Is.False,
                "A Q3=4 cap sample with no in-plane boundary evidence is an ordinary cap interior and must not be pulled toward an unrelated profile contour.");

            float3 projected = TransvoxelTopologyJob.ProjectExtrusionCapProfile(
                gridPinned, edgeAxis: 2, resolved, densityNormal: new float3(-1f, 0f, 0.5f));
            Assert.That(projected.x, Is.EqualTo(gridPinned.x - resolved.SignedQ3 * 0.125f).Within(1e-5f));
            Assert.That(projected.y, Is.EqualTo(gridPinned.y).Within(1e-5f));
            Assert.That(projected.z, Is.EqualTo(gridPinned.z).Within(1e-5f),
                "Recovered profile projection must never move the extrusion coordinate; the rear cap stays exactly planar.");
            Assert.That(math.abs(projected.x - gridPinned.x), Is.GreaterThan(1f),
                "The recovered profile must be able to remove the >1-voxel diagonal staircase that the clamped cap sample could not represent.");

            string gpuPath = Path.Combine(Application.dataPath,
                "VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute");
            string gpu = File.ReadAllText(gpuPath);
            Assert.That(Regex.IsMatch(gpu,
                @"ResolveExtrusionCapProfileSample\s*\("), Is.True,
                "GPU topology must recover the same unclamped extrusion profile as CPU topology.");
            Assert.That(Regex.IsMatch(gpu,
                @"HasInPlaneOccupancyTransition\s*\(\s*solidGrid\s*,\s*axis\s*\)"), Is.True,
                "GPU topology must limit profile recovery to a proven cap perimeter cell.");
            Assert.That(Regex.IsMatch(gpu,
                @"ProjectExtrusionCapProfile\s*\(\s*local\s*,\s*axis\s*,\s*profileBoundary\s*,\s*profileGrid\s*\)"), Is.True,
                "GPU topology must project with the recovered profile sample and its recovered transverse gradient.");
        }
    }
}
