using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonVoxelBakePolicyTests
    {
        [Test]
        public void CreateSettings_UsesExplicitVoxelShellVolumetricBoundedBakePolicy()
        {
            MeshVoxelizationSettings settings = MountainDragonVoxelBakePolicy.CreateSettings(7);

            Assert.That(settings.VoxelSize, Is.EqualTo(0.30f));
            Assert.That(settings.FillInterior, Is.True);
            Assert.That(settings.FallbackMaterial, Is.EqualTo(7));
            Assert.That(settings.MaxDimensions, Is.EqualTo(new int3(127, 511, 127)));
            Assert.That(settings.MaxDenseCells, Is.EqualTo(2_000_000));
            Assert.That(settings.ThinFeaturePaddingVoxels, Is.Zero,
                "The source-specific policy must not globally bloat wings, horns, legs, or tail.");
            Assert.That(settings.OpenSurfacePolicy, Is.EqualTo(MeshVoxelOpenSurfacePolicy.VoxelShellFill),
                "This known topologically-open source may fill only when its conservative voxel raster encloses cells.");
        }

        [Test]
        public void ValidateBakeEnvelope_RejectsChangedSourceOrNonVolumetricBake()
        {
            var cell = new BakedVoxelCell(new int3(0, 0, 0), 7);
            var wrongSource = new BakedVoxelStructure(
                0.30f, int3.zero, new int3(10, 10, 10), new[] { cell },
                sourceTriangleCount: 123, voxelizationMilliseconds: 1.0,
                interiorFilled: true);
            var shell = new BakedVoxelStructure(
                0.30f, int3.zero, new int3(10, 10, 10), new[] { cell },
                MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount, 1.0,
                interiorFilled: false);

            Assert.That(
                () => MountainDragonVoxelBakePolicy.ValidateBakeEnvelope(wrongSource),
                Throws.InvalidOperationException);
            Assert.That(
                () => MountainDragonVoxelBakePolicy.ValidateBakeEnvelope(shell),
                Throws.InvalidOperationException);
        }

        [Test]
        public void ValidateBakeEnvelope_AcceptsBoundedVolumetricArtifactFromExpectedSource()
        {
            var bake = new BakedVoxelStructure(
                0.30f,
                new int3(-10, -10, 10),
                new int3(100, 108, 106),
                new[]
                {
                    new BakedVoxelCell(new int3(0, 0, 0), 7),
                    new BakedVoxelCell(new int3(50, 54, 53), 7),
                },
                MountainDragonVoxelBakePolicy.ExpectedSourceTriangleCount,
                voxelizationMilliseconds: 1.0,
                interiorFilled: true);

            Assert.DoesNotThrow(() => MountainDragonVoxelBakePolicy.ValidateBakeEnvelope(bake));
        }
    }
}
