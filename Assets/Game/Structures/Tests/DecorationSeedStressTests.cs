using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationSeedStressTests
    {
        [Test]
        public void BedroomSceneRemainsCoherentAcrossRepresentativeSeeds()
        {
            DecorationSpace space = new DecorationSpace
            {
                SpaceId = 0xBED001u,
                Kind = DecorationSpaceKind.Bedroom,
                Bounds = new DecorationBounds
                {
                    Min = new int3(-60, 10, -50),
                    MaxExclusive = new int3(60, 58, 50),
                },
            };
            DecorationExclusion[] exclusions =
            {
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Door | DecorationExclusionKind.Navigation,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(-8, 10, -50),
                        MaxExclusive = new int3(8, 34, -36),
                    },
                },
            };

            for (uint seed = 1; seed <= 128; seed++)
            {
                DecorationContext context = new DecorationContext
                {
                    WorldSeed = seed,
                    StructureId = 0xCA571Eu,
                    SpaceId = space.SpaceId,
                    StyleId = 7u + seed % 4u,
                    StructureKind = DecorationStructureKind.Castle,
                    SpaceKind = DecorationSpaceKind.Bedroom,
                    Wealth = (DecorationWealthTier)(seed % 5u),
                    Condition = (DecorationConditionTier)(seed % 5u),
                    Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
                };

                Assert.IsTrue(
                    BedroomSceneResolver.TryResolve(in space, in context, exclusions, out DecorationPlacement[] placements),
                    $"Bedroom scene failed for seed {seed}.");
                Assert.AreEqual(BedroomSceneResolver.PlacementCount, placements.Length,
                    $"Bedroom scene returned the wrong placement count for seed {seed}.");

                for (int i = 0; i < placements.Length; i++)
                {
                    Assert.IsTrue(placements[i].IsWellFormed,
                        $"Placement {i} was malformed for seed {seed}.");
                    Assert.IsTrue(space.Bounds.Contains(in placements[i].Bounds),
                        $"Placement {i} escaped the room for seed {seed}.");

                    for (int e = 0; e < exclusions.Length; e++)
                    {
                        Assert.IsFalse(placements[i].Bounds.Overlaps(in exclusions[e].Bounds),
                            $"Placement {i} overlapped exclusion {e} for seed {seed}.");
                    }
                }
            }
        }
    }
}
