using Game.WorldBuilder.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainLandmarkTraversalProfileTests
    {
        [Test]
        public void IndependentConsumer_DerivesClearanceFromPhysicalMeasurements()
        {
            var profile = new MountainLandmarkTraversalProfile(
                voxelSizeMillimetres: 125,
                bodyHeightMillimetres: 1750,
                bodyRadiusMillimetres: 275,
                overheadMarginMillimetres: 375,
                lateralMarginMillimetres: 225,
                maximumGradePercent: 40);

            Assert.That(profile.HeadroomVoxels, Is.EqualTo(17));
            Assert.That(profile.ClearanceWidthVoxels, Is.EqualTo(8));
            Assert.That(profile.SupportsRamp(horizontalAdvanceVoxels: 10, riseVoxels: 4), Is.True);
            Assert.That(profile.SupportsRamp(horizontalAdvanceVoxels: 10, riseVoxels: 5), Is.False);
        }

        [Test]
        public void PhysicalProfile_IsNotCoupledToShowcaseVoxelScale()
        {
            var coarse = new MountainLandmarkTraversalProfile(
                voxelSizeMillimetres: 200,
                bodyHeightMillimetres: 1800,
                bodyRadiusMillimetres: 300,
                overheadMarginMillimetres: 600,
                lateralMarginMillimetres: 500,
                maximumGradePercent: 50);
            var fine = new MountainLandmarkTraversalProfile(
                voxelSizeMillimetres: 50,
                bodyHeightMillimetres: 1800,
                bodyRadiusMillimetres: 300,
                overheadMarginMillimetres: 600,
                lateralMarginMillimetres: 500,
                maximumGradePercent: 50);

            Assert.That(coarse.HeadroomVoxels, Is.EqualTo(12));
            Assert.That(coarse.ClearanceWidthVoxels, Is.EqualTo(8));
            Assert.That(fine.HeadroomVoxels, Is.EqualTo(48));
            Assert.That(fine.ClearanceWidthVoxels, Is.EqualTo(32));
        }
    }
}
