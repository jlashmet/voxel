using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned review composition. The shared Structures registry remains semantic and reusable;
    /// this catalog contributes only the neutral foundation reference and deterministic camera poses.
    /// </summary>
    internal static class HouseShowcaseArchitectureReviewCatalog
    {
        public const string ReviewKey = "house-foundation-neutral";
        public const string ReferenceKey = "house-foundation-neutral-reference";
        public const string ReferenceAssetPath = "Documentation/ArchitectureReferences/HouseShowcaseFoundation.svg";
        public const uint CanonicalSeed = 0x484F5553u;
        public const uint StructureId = 0x48534F57u;

        private static bool _installed;

        public static ArchitectureCanonicalReviewDescriptor EnsureRegistered()
        {
            if (_installed && ArchitectureReviewRegistry.TryGet(ReviewKey, out ArchitectureCanonicalReviewDescriptor existing))
                return existing;

            var request = new ArchitectureGenerationRequest(
                "kentridge",
                GuildHouseArchitectureProvider.ProviderKey,
                "adventurers",
                CanonicalSeed,
                StructureId,
                new int3(0, 16, 0),
                new ArchitectureGenerationParameters(
                    width: 0,
                    depth: 0,
                    storeyCount: 2,
                    floorHeight: 30,
                    roomCount: 5));

            var references = new[]
            {
                new ArchitectureReferenceImageDescriptor(
                    ReferenceKey,
                    ReferenceAssetPath,
                    "Neutral foundation review: two-storey massing, readable gable, centered entrance, real windows and grounded foundation."),
            };
            var poses = new[]
            {
                new ArchitectureReviewPose(
                    "exterior-three-quarter",
                    ArchitectureReviewPoseKind.Exterior,
                    new float3(17.5f, 10.5f, -20.0f),
                    new float3(6.2f, 5.0f, 5.5f),
                    46f),
                new ArchitectureReviewPose(
                    "interior-ground-floor",
                    ArchitectureReviewPoseKind.Interior,
                    new float3(6.1f, 1.8f, 2.1f),
                    new float3(6.1f, 1.8f, 7.2f),
                    60f),
            };
            var descriptor = new ArchitectureCanonicalReviewDescriptor(ReviewKey, request, references, poses);
            if (!ArchitectureReviewRegistry.TryRegister(descriptor) &&
                !ArchitectureReviewRegistry.TryGet(ReviewKey, out descriptor))
                throw new System.InvalidOperationException("Could not register the neutral HouseShowcase architecture review descriptor.");

            _installed = true;
            return descriptor;
        }
    }
}
