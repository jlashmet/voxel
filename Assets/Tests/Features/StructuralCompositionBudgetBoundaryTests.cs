using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    public sealed class StructuralCompositionBudgetBoundaryTests
    {
        private const uint Seed = 0x51A7C0DEu;

        [Test]
        public void ConservativeVoxelBudgetAcceptsExactLimitAndRejectsOneVoxelOver()
        {
            FeatureCatalogue catalogue = StructuralCompositionFixture.Build(Allocator.Temp);
            try
            {
                FeatureDefinition definition =
                    catalogue.Definitions[StructuralCompositionFixture.ChildId];

                definition.Footprint = new int3(256, 256, 256);
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = definition;

                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                StructuralCompositionReport atLimit = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.ChildId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.Ok, atLimit.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionVoxelCost, atLimit.VoxelCost);
                Assert.AreEqual(0, atLimit.ChildCount);

                definition.Footprint = new int3(97, 257, 673); // 16,777,217 voxels.
                catalogue.Definitions[StructuralCompositionFixture.ChildId] = definition;

                StructuralCompositionReport overLimit = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue, Seed, StructuralCompositionFixture.ChildId,
                    catalogue.ExplicitPlacements[0], instances);

                Assert.AreEqual(StructuralCompositionResult.VoxelBudgetExceeded, overLimit.Result);
                Assert.AreEqual(FeatureBudget.MaxCompositionVoxelCost + 1, overLimit.VoxelCost);
                Assert.AreEqual(0, overLimit.ChildCount);
            }
            finally
            {
                catalogue.Dispose();
            }
        }
    }
}
