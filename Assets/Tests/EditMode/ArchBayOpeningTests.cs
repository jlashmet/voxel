using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
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
    }
}
