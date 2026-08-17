using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CityPlannerTests
    {
        [Test]
        public void MixedTown_IsBoundedAndWellFormed()
        {
            CityConfig config = CityPresets.MixedTown();
            Assert.That(config.IsWellFormed, Is.True);
            Assert.That(config.CandidateCount, Is.LessThanOrEqualTo(CityConfig.MaximumCandidateCount));
            Assert.That(config.Palette.Length, Is.GreaterThan(1));
            Assert.That(config.Landmarks.Length, Is.GreaterThan(0));
        }

        [Test]
        public void SameSeedAndCandidate_ProducesIdenticalPlacement()
        {
            CityConfig config = CityPresets.MixedTown();
            int3 origin = new int3(320, 48, -640);
            for (int i = 0; i < config.CandidateCount; i++)
            {
                CityCandidateResult aResult = CityPlanner.ResolveCandidate(
                    in config, 0x12345678ul, origin, i, out CityPlacement a);
                CityCandidateResult bResult = CityPlanner.ResolveCandidate(
                    in config, 0x12345678ul, origin, i, out CityPlacement b);

                Assert.That(bResult, Is.EqualTo(aResult));
                Assert.That(b.StableIdentity, Is.EqualTo(a.StableIdentity));
                Assert.That(b.Grid, Is.EqualTo(a.Grid));
                Assert.That(b.LotOrigin, Is.EqualTo(a.LotOrigin));
                Assert.That(b.StructureOrigin, Is.EqualTo(a.StructureOrigin));
                Assert.That(b.BuildableSize, Is.EqualTo(a.BuildableSize));
                Assert.That(b.Facing, Is.EqualTo(a.Facing));
                Assert.That(b.Archetype, Is.EqualTo(a.Archetype));
                Assert.That(b.PresetId, Is.EqualTo(a.PresetId));
                Assert.That(b.IsLandmark, Is.EqualTo(a.IsLandmark));
            }
        }

        [Test]
        public void CandidateResolution_IsIndependentOfTraversalOrder()
        {
            CityConfig config = CityPresets.MixedTown();
            int count = config.CandidateCount;
            var forward = new ulong[count];
            var reverse = new ulong[count];
            var forwardResults = new CityCandidateResult[count];
            var reverseResults = new CityCandidateResult[count];

            for (int i = 0; i < count; i++)
            {
                forwardResults[i] = CityPlanner.ResolveCandidate(
                    in config, 99ul, int3.zero, i, out CityPlacement placement);
                forward[i] = placement.StableIdentity;
            }
            for (int i = count - 1; i >= 0; i--)
            {
                reverseResults[i] = CityPlanner.ResolveCandidate(
                    in config, 99ul, int3.zero, i, out CityPlacement placement);
                reverse[i] = placement.StableIdentity;
            }

            CollectionAssert.AreEqual(forward, reverse);
            CollectionAssert.AreEqual(forwardResults, reverseResults);
        }

        [Test]
        public void Lots_DoNotOverlapAndKeepConfiguredSpacing()
        {
            CityConfig config = CityPresets.MixedTown();
            for (int a = 0; a < config.CandidateCount; a++)
            {
                CityPlanner.ResolveCandidate(in config, 1ul, int3.zero, a, out CityPlacement first);
                for (int b = a + 1; b < config.CandidateCount; b++)
                {
                    CityPlanner.ResolveCandidate(in config, 1ul, int3.zero, b, out CityPlacement second);
                    bool separatedX = first.LotOrigin.x + first.LotSize.x + config.Lot.MinimumSpacing <= second.LotOrigin.x ||
                                      second.LotOrigin.x + second.LotSize.x + config.Lot.MinimumSpacing <= first.LotOrigin.x;
                    bool separatedZ = first.LotOrigin.z + first.LotSize.y + config.Lot.MinimumSpacing <= second.LotOrigin.z ||
                                      second.LotOrigin.z + second.LotSize.y + config.Lot.MinimumSpacing <= first.LotOrigin.z;
                    Assert.That(separatedX || separatedZ, Is.True,
                        $"Lots {a} and {b} overlap or violate minimum spacing.");
                }
            }
        }

        [Test]
        public void FrontageAlwaysMapsToRoadFacingOrientation()
        {
            CityConfig config = CityPresets.MixedTown();
            for (int i = 0; i < config.CandidateCount; i++)
            {
                CityPlanner.ResolveCandidate(in config, 7ul, int3.zero, i, out CityPlacement placement);
                Facing expected;
                switch (placement.Frontage)
                {
                    case CityRoadFrontage.North: expected = Facing.North; break;
                    case CityRoadFrontage.East: expected = Facing.East; break;
                    case CityRoadFrontage.South: expected = Facing.South; break;
                    default: expected = Facing.West; break;
                }
                Assert.That(placement.Facing, Is.EqualTo(expected));
            }
        }

        [Test]
        public void PriorityLandmarkRule_PromotesEligibleLotWithoutGlobalState()
        {
            CityConfig config = CityPresets.MixedTown();
            config.PlazaRadiusLots = 0;
            config.OpenSpacePermille = 0;
            config.ResidentialDensityPermille = 1000;
            config.MixedDensityPermille = 1000;
            config.CivicDensityPermille = 1000;
            config.Lot.OccupancyPermille = 1000;
            config.Landmarks.Clear();
            config.Landmarks.Add(new CityLandmarkRule
            {
                Archetype = CityStructureArchetype.Castle,
                PresetId = CityStructurePresetId.KeepCastle,
                Districts = CityDistrictMask.All,
                MinimumBuildableWidth = 40,
                MinimumBuildableDepth = 40,
                EveryNthEligibleLot = 1,
                Priority = 100,
            });

            CityCandidateResult result = CityPlanner.ResolveCandidate(
                in config, 17ul, int3.zero, 0, out CityPlacement placement);

            Assert.That(result, Is.EqualTo(CityCandidateResult.Placed));
            Assert.That(placement.IsLandmark, Is.True);
            Assert.That(placement.Archetype, Is.EqualTo(CityStructureArchetype.Castle));
            Assert.That(placement.PresetId, Is.EqualTo(CityStructurePresetId.KeepCastle));
        }

        [Test]
        public void CentralPlaza_RejectsBuildingCandidateDeterministically()
        {
            CityConfig config = CityPresets.MixedTown();
            config.BlocksX = 5;
            config.BlocksZ = 5;
            config.PlazaRadiusLots = 1;
            int center = 2 + 2 * config.BlocksX;

            CityCandidateResult result = CityPlanner.ResolveCandidate(
                in config, 123ul, int3.zero, center, out _);

            Assert.That(result, Is.EqualTo(CityCandidateResult.Plaza));
        }

        [Test]
        public void ConfigRejectsUnboundedGlobalScalePlans()
        {
            CityConfig config = CityPresets.MixedTown();
            config.BlocksX = CityConfig.MaximumBlocksPerAxis + 1;
            Assert.That(config.IsWellFormed, Is.False);

            config = CityPresets.MixedTown();
            config.BlocksZ = CityConfig.MaximumBlocksPerAxis + 1;
            Assert.That(config.IsWellFormed, Is.False);
        }
    }
}
