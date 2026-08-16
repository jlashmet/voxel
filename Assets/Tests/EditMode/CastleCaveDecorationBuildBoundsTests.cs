using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationBuildBoundsTests
    {
        [Test]
        public void PlannedDecorationBoundsContainEveryPrimitiveFootprint()
        {
            CavePlanningConstraints constraints = StandardConstraints();
            for (uint seed = 1; seed <= 32; seed++)
            {
                CavePlan cave = CavePlanner.Create(seed, in constraints);
                CastleCaveDecorationPlan decoration = CastleCaveDecorationPlanner.Create(cave);
                CastleCaveDecorationBuildBounds bounds =
                    CastleCaveDecorationBuildBoundsResolver.Resolve(cave, decoration);

                for (int i = 0; i < decoration.Elements.Length; i++)
                {
                    CastleCaveDecorationSpec spec = decoration.Elements[i];
                    AssertElementExtremes(bounds, in spec, seed);
                }
            }
        }

        private static void AssertElementExtremes(
            CastleCaveDecorationBuildBounds bounds,
            in CastleCaveDecorationSpec spec,
            uint seed)
        {
            switch (spec.Kind)
            {
                case CastleCaveDecorationKind.EntryPool:
                    AssertContains(bounds,
                        new int3(spec.Position.x - spec.Radius, spec.Position.y, spec.Position.z),
                        seed, spec.Id);
                    AssertContains(bounds,
                        new int3(spec.Position.x + spec.Radius,
                                 spec.Position.y + spec.Height - 1,
                                 spec.Position.z),
                        seed, spec.Id);
                    AssertContains(bounds,
                        new int3(spec.Position.x, spec.Position.y,
                                 spec.Position.z - spec.Radius),
                        seed, spec.Id);
                    AssertContains(bounds,
                        new int3(spec.Position.x, spec.Position.y,
                                 spec.Position.z + spec.Radius),
                        seed, spec.Id);
                    return;

                case CastleCaveDecorationKind.DryCauseway:
                    AssertContains(bounds, spec.Position, seed, spec.Id);
                    AssertContains(bounds, spec.Position + spec.Size - 1, seed, spec.Id);
                    return;

                case CastleCaveDecorationKind.CrystalSpire:
                case CastleCaveDecorationKind.MossSpire:
                case CastleCaveDecorationKind.Stalagmite:
                    AssertContains(bounds,
                        new int3(spec.Position.x - spec.Radius, spec.Position.y, spec.Position.z),
                        seed, spec.Id);
                    AssertContains(bounds,
                        new int3(spec.Position.x + spec.Radius,
                                 spec.Position.y + spec.Height - 1,
                                 spec.Position.z + spec.Radius),
                        seed, spec.Id);
                    return;

                case CastleCaveDecorationKind.Stalactite:
                    AssertContains(bounds,
                        new int3(spec.Position.x - spec.Radius,
                                 spec.Position.y - spec.Height + 1,
                                 spec.Position.z - spec.Radius),
                        seed, spec.Id);
                    AssertContains(bounds,
                        new int3(spec.Position.x + spec.Radius,
                                 spec.Position.y,
                                 spec.Position.z + spec.Radius),
                        seed, spec.Id);
                    return;

                case CastleCaveDecorationKind.LightMarker:
                    AssertContains(bounds, spec.Position - new int3(1, 1, 1), seed, spec.Id);
                    AssertContains(bounds, spec.Position + new int3(1, 2, 1), seed, spec.Id);
                    return;
            }
        }

        private static void AssertContains(
            CastleCaveDecorationBuildBounds bounds,
            int3 voxel,
            uint seed,
            int elementId) =>
            Assert.IsTrue(bounds.Contains(voxel),
                $"seed {seed}, element {elementId}: {voxel} escaped decoration bounds");

        private static CavePlanningConstraints StandardConstraints() =>
            new CavePlanningConstraints
            {
                Entrance = new int3(120, 80, -40),
                EntranceToMainOffset = new int3(0, 18, 0),
                MainRadii = new int3(82, 36, 104),
                SecondaryChamberCount = 4,
                SecondaryMinRadii = new int3(30, 22, 34),
                SecondaryMaxRadii = new int3(62, 38, 78),
                MinimumHorizontalSpread = 54,
                MaximumHorizontalSpread = 126,
                VerticalSpread = 18,
                PassageWidth = 20,
                PassageHeight = 30,
            };
    }
}
