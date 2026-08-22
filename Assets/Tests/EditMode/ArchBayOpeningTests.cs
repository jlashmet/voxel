using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
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
    }
}
