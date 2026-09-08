using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CharacterMotorCapsuleFootprintTests
    {
        [Test]
        public void DiagonalAabbCornerOutsideCapsuleDoesNotOverlapVoxel()
        {
            Assert.That(
                CharacterMotor.CapsuleFootprintOverlapsVoxel(
                    centreX: -104.64f,
                    centreZ: 28.00f,
                    radius: 0.30f,
                    voxelX: -1050,
                    voxelZ: 282),
                Is.False,
                "A voxel that lies only in the enclosing square AABB corner must not block the circular capsule footprint.");
        }

        [Test]
        public void NearbyVoxelThatEntersCapsuleStillOverlaps()
        {
            Assert.That(
                CharacterMotor.CapsuleFootprintOverlapsVoxel(
                    centreX: -104.64f,
                    centreZ: 28.00f,
                    radius: 0.30f,
                    voxelX: -1049,
                    voxelZ: 282),
                Is.True,
                "The capsule correction must not discard a neighboring voxel whose cell area genuinely intersects the radius.");
        }

        [Test]
        public void TangentVoxelFaceDoesNotCountAsPenetration()
        {
            Assert.That(
                CharacterMotor.CapsuleFootprintOverlapsVoxel(
                    centreX: 0f,
                    centreZ: 0f,
                    radius: 0.30f,
                    voxelX: 3,
                    voxelZ: 0),
                Is.False,
                "Touching exactly at the capsule boundary must preserve the motor's half-open contact semantics.");
        }

        [Test]
        public void InteriorVoxelAreaCountsAsPenetration()
        {
            Assert.That(
                CharacterMotor.CapsuleFootprintOverlapsVoxel(
                    centreX: 0f,
                    centreZ: 0f,
                    radius: 0.30f,
                    voxelX: 2,
                    voxelZ: 0),
                Is.True,
                "A voxel cell with interior area inside the capsule radius must continue to block movement.");
        }
    }
}
