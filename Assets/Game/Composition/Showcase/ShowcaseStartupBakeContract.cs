using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// VoxelShowcase composition for the reusable startup-bake provenance mechanism. This layer
    /// owns the Mountain Dragon source signature; byte hashing/manifest validation remain generic.
    /// </summary>
    public static class ShowcaseStartupBakeContract
    {
        public const string ManifestResourcePath = "VoxelShowcase/ShowcaseWorld.manifest";
        public const string ManifestAssetPath =
            "Assets/Resources/VoxelShowcase/ShowcaseWorld.manifest.txt";

        private const int ManifestVersion = 1;
        // Revision 8 makes each switchback follow the receding mountain shell throughout its rise.
        // The tier contract now carries both Z endpoints and subdivides the climb into deterministic
        // shallow segments, so no upper ramp can finish detached from the natural core.
        private const uint LandmarkContractRevision = 8;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// Showcase-owned source signature. The generic provenance helper intentionally knows
        /// nothing about Mountain Dragon layout, revision policy, or scene composition.
        /// </summary>
        public static uint RequiredContentSignature
        {
            get
            {
                uint hash = FnvOffsetBasis;
                Mix(ref hash, unchecked((int)LandmarkContractRevision));
                Mix(ref hash, ShowcaseMountainDragonLayout.OriginX);
                Mix(ref hash, ShowcaseMountainDragonLayout.OriginZ);
                Mix(ref hash, ShowcaseMountainDragonLayout.FootprintEdge);
                Mix(ref hash, ShowcaseMountainDragonLayout.MountainRadius);
                Mix(ref hash, ShowcaseMountainDragonLayout.MountainHeight);
                Mix(ref hash, ShowcaseMountainDragonLayout.SummitRadius);
                Mix(ref hash, ShowcaseMountainDragonLayout.PathWidth);
                Mix(ref hash, ShowcaseMountainDragonLayout.PathRun);
                Mix(ref hash, ShowcaseMountainDragonLayout.PathRise);
                Mix(ref hash, ShowcaseMountainDragonLayout.SwitchbackCount);
                Mix(ref hash, ShowcaseMountainDragonLayout.PlaceholderSize);
                return hash;
            }
        }

        public static string CreateManifest(byte[] payload) =>
            StartupBakeProvenance.CreateManifest(
                ManifestVersion,
                RequiredContentSignature,
                payload);

        public static void Validate(byte[] payload, string manifestText) =>
            StartupBakeProvenance.Validate(
                ManifestVersion,
                RequiredContentSignature,
                payload,
                manifestText,
                "VoxelShowcase startup bake");

        public static string ComputePayloadSha256(byte[] payload) =>
            StartupBakeProvenance.ComputePayloadSha256(payload);

        private static void Mix(ref uint hash, int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(bits >> shift);
                    hash *= FnvPrime;
                }
            }
        }
    }
}
