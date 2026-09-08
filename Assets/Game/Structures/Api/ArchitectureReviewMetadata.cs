using System;

namespace Game.Structures.Api
{
    /// <summary>
    /// Durable identity for one runtime review capture. CI may persist the rendered image separately;
    /// this record preserves the semantic inputs required to pair it with a reference without using
    /// pixel similarity as an art oracle.
    /// </summary>
    public readonly struct ArchitectureReviewCaptureMetadata
    {
        public readonly string CanonicalReviewKey;
        public readonly string ReferenceKey;
        public readonly string PoseKey;
        public readonly string StyleProfileKey;
        public readonly string ProviderKey;
        public readonly string ArchetypeKey;
        public readonly uint Seed;
        public readonly ArchitectureGenerationParameters Parameters;
        public readonly ArchitectureGenerationIdentity Identity;
        public readonly string RuntimeCaptureName;

        public ArchitectureReviewCaptureMetadata(
            string canonicalReviewKey,
            string referenceKey,
            string poseKey,
            string styleProfileKey,
            string providerKey,
            string archetypeKey,
            uint seed,
            ArchitectureGenerationParameters parameters,
            ArchitectureGenerationIdentity identity,
            string runtimeCaptureName)
        {
            CanonicalReviewKey = canonicalReviewKey ?? string.Empty;
            ReferenceKey = referenceKey ?? string.Empty;
            PoseKey = poseKey ?? string.Empty;
            StyleProfileKey = styleProfileKey ?? string.Empty;
            ProviderKey = providerKey ?? string.Empty;
            ArchetypeKey = archetypeKey ?? string.Empty;
            Seed = seed;
            Parameters = parameters;
            Identity = identity;
            RuntimeCaptureName = runtimeCaptureName ?? string.Empty;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(CanonicalReviewKey) &&
            !string.IsNullOrWhiteSpace(ReferenceKey) &&
            !string.IsNullOrWhiteSpace(PoseKey) &&
            !string.IsNullOrWhiteSpace(StyleProfileKey) &&
            !string.IsNullOrWhiteSpace(ProviderKey) &&
            !string.IsNullOrWhiteSpace(ArchetypeKey) &&
            Parameters.IsWellFormed &&
            Identity.IsWellFormed &&
            !string.IsNullOrWhiteSpace(RuntimeCaptureName);

        public string ToDiagnosticString()
        {
            return string.Concat(
                "review=", CanonicalReviewKey,
                " reference=", ReferenceKey,
                " pose=", PoseKey,
                " profile=", StyleProfileKey,
                " provider=", ProviderKey,
                " archetype=", ArchetypeKey,
                " seed=", Seed.ToString(),
                " requestHash=", Identity.RequestHash.ToString("X16"),
                " realizationHash=", Identity.RealizationHash.ToString("X16"),
                " capture=", RuntimeCaptureName);
        }
    }

    public readonly struct ArchitectureReviewComparisonDescriptor
    {
        public readonly ArchitectureReferenceImageDescriptor Reference;
        public readonly ArchitectureReviewCaptureMetadata RuntimeCapture;

        public ArchitectureReviewComparisonDescriptor(
            ArchitectureReferenceImageDescriptor reference,
            ArchitectureReviewCaptureMetadata runtimeCapture)
        {
            Reference = reference;
            RuntimeCapture = runtimeCapture;
        }

        public bool IsWellFormed => Reference.IsWellFormed && RuntimeCapture.IsWellFormed;
    }
}
