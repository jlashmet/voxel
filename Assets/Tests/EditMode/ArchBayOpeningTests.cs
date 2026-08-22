using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchBayOpeningTests
    {
        [Test]
        public void LowerOpeningCarveIncludesIntegerRadiusEndpoints()
        {
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = 32,
                PierHeight = 40,
                RingThickness = 7,
                Depth = 12,
                VoussoirCount = 13,
                JointRecessDepth = 0,
                StoneMaterial = 1,
            };
            var bay = new ArchBayFeatureDefinition
            {
                Arch = arch,
                ShoulderWidth = 10,
                TopMargin = 8,
                FaceRecess = 1,
                PlinthHeight = 4,
                ImpostHeight = 4,
                Damage = ArchRuinDamage.Intact,
            };
            int3 origin = new(100, 20, 300);

            using var primitives = new NativeList<Primitive>(Allocator.Temp);
            Assert.That(bay.Emit(origin, primitives), Is.True);

            int backingZ = origin.z + 1;
            Primitive lowerOpening = default;
            bool found = false;
            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Shape != PrimitiveShape.Box || primitive.Mode != PrimitiveMode.Carve)
                    continue;
                if (primitive.A.y != origin.y || primitive.B.y != origin.y + arch.PierHeight)
                    continue;
                if (primitive.A.z != backingZ || primitive.B.z != backingZ + arch.Depth - 1)
                    continue;
                lowerOpening = primitive;
                found = true;
                break;
            }

            Assert.That(found, Is.True, "Arch bay must emit the lower rectangular opening carve.");
            int openingCentreX = origin.x + bay.ShoulderWidth + arch.Width / 2;
            int radius = arch.ClearSpan / 2;
            Assert.That(lowerOpening.A.x, Is.LessThanOrEqualTo(openingCentreX - radius),
                "The lower opening must clear the left integer-radius endpoint; otherwise a one-voxel column survives below the springline.");
            Assert.That(lowerOpening.B.x, Is.GreaterThanOrEqualTo(openingCentreX + radius),
                "The lower opening must clear the right integer-radius endpoint; otherwise a one-voxel column survives below the springline.");
        }

        [Test]
        public void AuthoredBoundarySamplesDoNotRequireAxisAlignedOccupancyTransition()
        {
            string cpuPath = Path.Combine(Application.dataPath,
                "VoxelEngine/Rendering/Runtime/SurfaceExtraction/Transvoxel/TransvoxelDensityJob.cs");
            string gpuPath = Path.Combine(Application.dataPath,
                "VoxelEngine/Rendering/Resources/VoxelBrickDensity.hlsl");
            string cpu = File.ReadAllText(cpuPath);
            string gpu = File.ReadAllText(gpuPath);

            Assert.That(Regex.IsMatch(cpu, @"if\s*\(\s*packedBoundary\s*!=\s*0\s*\)"), Is.True,
                "CPU density sampling must consume an authored boundary sample whenever one exists.");
            Assert.That(Regex.IsMatch(gpu, @"if\s*\(\s*packedBoundary\s*!=\s*0u\s*\)"), Is.True,
                "GPU density sampling must consume an authored boundary sample whenever one exists.");

            Assert.That(Regex.IsMatch(cpu,
                @"packedBoundary\s*!=\s*0\s*&&\s*HasOppositeOccupancyNeighbour"), Is.False,
                "CPU sampling must not reject diagonal analytic crossings just because all six axial neighbours share the centre occupancy.");
            Assert.That(Regex.IsMatch(gpu,
                @"packedBoundary\s*!=\s*0u\s*&&\s*HasOppositeOccupancyNeighbour"), Is.False,
                "GPU sampling must not reject diagonal analytic crossings just because all six axial neighbours share the centre occupancy.");
        }

        [Test]
        public void AuthoredPlanarSdfKeepsSmoothFieldNormals()
        {
            Assert.That(TransvoxelTopologyJob.UsesFlatTriangleNormals(
                planar: true, rounded: false, authoredBoundary: false), Is.True,
                "Ordinary planar masonry must retain flat triangle normals.");
            Assert.That(TransvoxelTopologyJob.UsesFlatTriangleNormals(
                planar: true, rounded: false, authoredBoundary: true), Is.False,
                "An authored analytic boundary must override planar material shading so a curved SDF keeps smooth field normals.");
            Assert.That(TransvoxelTopologyJob.UsesFlatTriangleNormals(
                planar: false, rounded: true, authoredBoundary: false), Is.False,
                "Rounded surfaces must continue to use smooth field normals.");

            string gpuPath = Path.Combine(Application.dataPath,
                "VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute");
            string gpu = File.ReadAllText(gpuPath);
            Assert.That(Regex.IsMatch(gpu,
                @"bool\s+UsesFlatTriangleNormals\s*\(\s*bool\s+planar\s*,\s*bool\s+rounded\s*,\s*bool\s+authoredBoundary\s*\)"), Is.True,
                "GPU topology must carry the same authored-boundary normal policy as the CPU path.");
            Assert.That(Regex.IsMatch(gpu, @"if\s*\(\s*flatPlanar\s*\)"), Is.True,
                "GPU geometry emission must branch on the explicit normal policy.");
            Assert.That(Regex.IsMatch(gpu,
                @"if\s*\(\s*outputVertexCount\s*==\s*indexCount\s*\)"), Is.False,
                "GPU emission must not infer flat shading from equal counts; a smooth one-triangle cell can have equal vertex and index counts too.");
        }

        [Test]
        public void AuthoredExtrusionCapRimKeepsPlanarDepthAndFollowsSdf()
        {
            VoxelBoundarySample rim = VoxelBoundarySample.FromSignedQ4(4, extrusionAxis: 2);
            Assert.That(rim.SignedQ3, Is.EqualTo(2));
            Assert.That(TransvoxelTopologyJob.IsExtrusionCapRimSample(rim, edgeAxis: 2), Is.True,
                "A solid authored sample inside half a voxel of an extrusion rim must be projected to the analytic contour.");

            float3 original = new(10f, 20f, 5.5f);
            float3 projected = TransvoxelTopologyJob.ProjectExtrusionCapRim(
                original, edgeAxis: 2, rim, new float3(-1f, 0f, 0.75f));
            Assert.That(projected.x, Is.EqualTo(9.75f).Within(1e-5f),
                "The cap rim must move transversely by the authored signed distance.");
            Assert.That(projected.y, Is.EqualTo(original.y).Within(1e-5f));
            Assert.That(projected.z, Is.EqualTo(original.z).Within(1e-5f),
                "Projection must never move the extrusion coordinate; the cap must stay exactly planar.");

            VoxelBoundarySample capInterior =
                VoxelBoundarySample.FromSignedQ4(8, extrusionAxis: 2);
            Assert.That(TransvoxelTopologyJob.IsExtrusionCapRimSample(
                capInterior, edgeAxis: 2), Is.False,
                "A sample at the half-voxel centre-distance threshold alone is not enough to classify the cap rim.");
            Assert.That(TransvoxelTopologyJob.IsExtrusionCapRimSample(rim, edgeAxis: 0), Is.False,
                "Only an edge along the primitive's extrusion axis is a planar cap crossing.");

            Assert.That(PositiveDepthFaceMask(hasDiagonalInPlaneAir: true), Is.Zero,
                "A diagonal in-plane occupancy transition proves the analytic perimeter crosses the cap neighbourhood. The faceted pass must yield that whole boundary strip even when the centre sample is exactly half a voxel from the SDF boundary.");
            Assert.That(PositiveDepthFaceMask(hasDiagonalInPlaneAir: false), Is.Not.Zero,
                "Authored cap interiors must remain exact faceted planes; only the analytic perimeter strip yields to continuous topology.");

            string gpuPath = Path.Combine(Application.dataPath,
                "VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute");
            string gpu = File.ReadAllText(gpuPath);
            Assert.That(Regex.IsMatch(gpu,
                @"bool\s+IsExtrusionCapRimSample\s*\(\s*uint\s+packedBoundary\s*,\s*int\s+edgeAxis\s*\)"), Is.True,
                "GPU topology must recognize the same authored extrusion-cap rim as the CPU path.");
            Assert.That(Regex.IsMatch(gpu,
                @"ResolveExtrusionCapProfileSample\s*\("), Is.True,
                "GPU topology must recover the unclamped authored profile before projecting a proven cap perimeter cell.");
            Assert.That(Regex.IsMatch(gpu,
                @"local\s*=\s*ProjectExtrusionCapProfile\s*\(\s*local\s*,\s*axis\s*,\s*profileBoundary\s*,\s*profileGrid\s*\)"), Is.True,
                "GPU topology must apply the same recovered-profile transverse projection as the CPU path.");
        }

        private static uint PositiveDepthFaceMask(bool hasDiagonalInPlaneAir)
        {
            const int cellsPerAxis = 1;
            const int gridSize = 4;
            const int padding = 1;
            const int sampleCount = gridSize * gridSize * gridSize;

            var materials = new NativeArray<byte>(sampleCount, Allocator.TempJob);
            var surfaces = new NativeArray<uint>(sampleCount, Allocator.TempJob);
            var boundaries = new NativeArray<byte>(sampleCount, Allocator.TempJob);
            var masks = new NativeArray<uint>(6, Allocator.TempJob);
            try
            {
                for (int i = 0; i < materials.Length; i++) materials[i] = 1;

                int3 centre = new(padding, padding, padding);
                int CentreIndex(int3 p) => p.x + gridSize * (p.y + gridSize * p.z);
                int centreIndex = CentreIndex(centre);
                surfaces[centreIndex] = SurfaceStyles.Planar;
                boundaries[centreIndex] =
                    VoxelBoundarySample.FromSignedQ4(8, extrusionAxis: 2).Packed;

                int3 depthAir = centre + new int3(0, 0, 1);
                materials[CentreIndex(depthAir)] = VoxelGrid.MaterialEmpty;

                if (hasDiagonalInPlaneAir)
                {
                    // Keep all four axial X/Y neighbours solid. The only occupancy evidence is a
                    // diagonal crossing, exactly the corner case that recreates voxel-scale steps.
                    int3 diagonalAir = centre + new int3(1, 1, 0);
                    materials[CentreIndex(diagonalAir)] = VoxelGrid.MaterialEmpty;
                }

                var job = new FacetedMaskJob
                {
                    Materials = materials,
                    SurfaceSemantics = surfaces,
                    BoundarySamples = boundaries,
                    Catalogue = SurfaceCatalogueView.CreateBuiltIns(),
                    Coatings = default,
                    CellsPerAxis = cellsPerAxis,
                    GridSize = gridSize,
                    Padding = padding,
                    FaceMasks = masks,
                };
                job.Execute(0);
                return masks[5];
            }
            finally
            {
                masks.Dispose();
                boundaries.Dispose();
                surfaces.Dispose();
                materials.Dispose();
            }
        }
    }
}
