using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class ArchitectureRegistryTests
    {
        [Test]
        public void RegistryExposesSemanticProfilesAndProductionGuildProvider()
        {
            ArchitectureStyleProfileDescriptor[] profiles = ArchitectureRegistry.StyleProfiles();
            ArchitectureProviderDescriptor[] providers = ArchitectureRegistry.ProviderDescriptors();

            Assert.That(profiles.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(Array.Exists(profiles, p => p.Key == "kentridge" && p.IsWellFormed), Is.True);
            Assert.That(
                Array.Exists(providers, p =>
                    p.Key == GuildHouseArchitectureProvider.ProviderKey &&
                    p.IsWellFormed &&
                    (p.SupportedParameters & ArchitectureParameterSupport.Footprint) != 0 &&
                    (p.SupportedParameters & ArchitectureParameterSupport.StoreyCount) != 0 &&
                    (p.SupportedParameters & ArchitectureParameterSupport.FloorHeight) != 0),
                Is.True);
        }

        [Test]
        public void SameRequestProducesSameAuthoritativeIdentity()
        {
            Assert.That(
                ArchitectureRegistry.TryGetProvider(GuildHouseArchitectureProvider.ProviderKey, out IArchitectureProvider provider),
                Is.True);
            ArchitectureGenerationRequest request = Request(
                seed: 0x12345678u,
                new ArchitectureGenerationParameters(
                    width: 128,
                    depth: 120,
                    storeyCount: 2,
                    floorHeight: 30,
                    roomCount: 5));

            Assert.That(provider.TryPlan(in request, out ArchitectureGenerationResult first, out string firstError),
                Is.True, firstError);
            Assert.That(provider.TryPlan(in request, out ArchitectureGenerationResult second, out string secondError),
                Is.True, secondError);

            Assert.That(first.Identity.RequestHash, Is.EqualTo(second.Identity.RequestHash));
            Assert.That(first.Identity.RealizationHash, Is.EqualTo(second.Identity.RealizationHash));
            Assert.That(first.Summary.Min, Is.EqualTo(second.Summary.Min));
            Assert.That(first.Summary.MaxExclusive, Is.EqualTo(second.Summary.MaxExclusive));
            Assert.That(first.Summary.StoreyCount, Is.EqualTo(2));
            Assert.That(first.Summary.FloorHeight, Is.EqualTo(30));
            Assert.That(first.Summary.StairCount, Is.EqualTo(1));
        }

        [Test]
        public void FourSeedsProduceDistinctStructuralMassingAndRemainValid()
        {
            Assert.That(
                ArchitectureRegistry.TryGetProvider(GuildHouseArchitectureProvider.ProviderKey, out IArchitectureProvider provider),
                Is.True);
            uint[] seeds = { 1u, 2u, 3u, 4u };
            var realizations = new HashSet<ulong>();
            var footprints = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < seeds.Length; i++)
            {
                ArchitectureGenerationRequest request = Request(
                    seeds[i],
                    new ArchitectureGenerationParameters(roomCount: 5));
                Assert.That(provider.TryPlan(in request, out ArchitectureGenerationResult result, out string error),
                    Is.True, error);
                Assert.That(result.IsWellFormed, Is.True);
                Assert.That(result.Summary.DoorCount, Is.GreaterThan(0));
                Assert.That(result.Summary.WindowCount, Is.GreaterThan(0));
                Assert.That(result.Summary.StoreyCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(result.Summary.StairCount, Is.EqualTo(result.Summary.StoreyCount - 1));
                Assert.That(
                    (result.Summary.Capabilities & ArchitecturePresentationCapabilities.MultiStoreyCirculation) != 0,
                    Is.True);

                int3 size = result.Summary.MaxExclusive - result.Summary.Min;
                realizations.Add(result.Identity.RealizationHash);
                footprints.Add($"{size.x}x{size.z}");
            }

            Assert.That(realizations.Count, Is.EqualTo(4), "four review seeds should realize four structural variants");
            Assert.That(footprints.Count, Is.EqualTo(4), "seed variation should alter visible massing, not just cosmetic noise");
        }

        [Test]
        public void ProviderRejectsUnsupportedArchitecturalOverridesInsteadOfSilentlyClamping()
        {
            Assert.That(
                ArchitectureRegistry.TryGetProvider(GuildHouseArchitectureProvider.ProviderKey, out IArchitectureProvider provider),
                Is.True);
            ArchitectureGenerationRequest badRoof = Request(
                9u,
                new ArchitectureGenerationParameters(
                    width: 128,
                    depth: 128,
                    roomCount: 5,
                    roofForm: ArchitectureRoofForm.Gable));
            Assert.That(provider.TryPlan(in badRoof, out _, out string roofError), Is.False);
            StringAssert.Contains("roof", roofError.ToLowerInvariant());

            ArchitectureGenerationRequest tooShort = Request(
                9u,
                new ArchitectureGenerationParameters(
                    width: 128,
                    depth: 128,
                    storeyCount: 2,
                    floorHeight: 18,
                    roomCount: 5));
            Assert.That(provider.TryPlan(in tooShort, out _, out string heightError), Is.False);
            StringAssert.Contains("floor height", heightError.ToLowerInvariant());
        }

        [Test]
        public void RegistrationBoundaryAcceptsIndependentProviderWithoutEditingShowcaseSelectionLogic()
        {
            const string key = "architecture-test-independent-provider";
            if (!ArchitectureRegistry.TryGetProvider(key, out IArchitectureProvider existing))
            {
                var candidate = new IndependentProvider(key);
                Assert.That(ArchitectureRegistry.TryRegisterProvider(candidate), Is.True);
                existing = candidate;
            }

            Assert.That(existing.Descriptor.Key, Is.EqualTo(key));
            Assert.That(existing.Descriptor.StructureClass, Is.EqualTo(ArchitectureStructureClass.Shop));
            Assert.That(
                Array.Exists(ArchitectureRegistry.ProviderDescriptors(), p => p.Key == key),
                Is.True);
        }

        [Test]
        public void CanonicalReviewContractCarriesReferenceAndDeterministicCameraMetadata()
        {
            ArchitectureGenerationRequest request = Request(
                0xCAFEBABEu,
                new ArchitectureGenerationParameters(width: 128, depth: 120, roomCount: 5));
            var review = new ArchitectureCanonicalReviewDescriptor(
                "foundation-review",
                request,
                new[]
                {
                    new ArchitectureReferenceImageDescriptor(
                        "concept-front",
                        "Assets/References/Architecture/foundation-front.png",
                        "Foundation contract placeholder reference"),
                },
                new[]
                {
                    new ArchitectureReviewPose(
                        "exterior-three-quarter",
                        ArchitectureReviewPoseKind.Exterior,
                        new float3(18f, 10f, -24f),
                        new float3(6f, 4f, 6f),
                        50f),
                    new ArchitectureReviewPose(
                        "interior-entry",
                        ArchitectureReviewPoseKind.Interior,
                        new float3(6f, 2f, 2f),
                        new float3(6f, 2f, 8f),
                        60f),
                });

            Assert.That(review.IsWellFormed, Is.True);
            Assert.That(review.References[0].IsWellFormed, Is.True);
            Assert.That(review.Poses[0].IsWellFormed, Is.True);
            Assert.That(review.Poses[1].Kind, Is.EqualTo(ArchitectureReviewPoseKind.Interior));
        }

        private static ArchitectureGenerationRequest Request(
            uint seed,
            ArchitectureGenerationParameters parameters)
        {
            return new ArchitectureGenerationRequest(
                "kentridge",
                GuildHouseArchitectureProvider.ProviderKey,
                "adventurers",
                seed,
                0x48534F57u,
                new int3(0, 16, 0),
                parameters);
        }

        private sealed class IndependentProvider : IArchitectureProvider
        {
            private readonly ArchitectureProviderDescriptor _descriptor;

            public IndependentProvider(string key)
            {
                _descriptor = new ArchitectureProviderDescriptor(
                    key,
                    "Independent Shop Provider",
                    ArchitectureStructureClass.Shop,
                    new[] { "shop" },
                    ArchitectureParameterSupport.Footprint,
                    ArchitecturePresentationCapabilities.ProductionVoxelOccupancy);
            }

            public ArchitectureProviderDescriptor Descriptor => _descriptor;

            public bool TryPlan(
                in ArchitectureGenerationRequest request,
                out ArchitectureGenerationResult result,
                out string error)
            {
                result = default;
                error = "Test provider does not realize geometry.";
                return false;
            }

            public bool TryAuthor(
                IStructureAuthoringSession authoring,
                in ArchitectureGenerationRequest request,
                out ArchitectureGenerationResult result,
                out string error)
            {
                result = default;
                error = "Test provider does not realize geometry.";
                return false;
            }
        }
    }
}
