using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Provenance contract for the checked-in VoxelShowcase startup image.
    ///
    /// The binary bake codec deliberately remains backwards-compatible because it is also used by
    /// other showcase worlds. VoxelShowcase itself has a stricter requirement: the image it ships
    /// must have been produced from the currently-authored landmark contract. A tiny sidecar binds
    /// that source signature to the exact serialized bytes, so an old but structurally valid bake
    /// can no longer suppress newer WorldBuilder content silently.
    /// </summary>
    public static class ShowcaseStartupBakeContract
    {
        public const string ManifestResourcePath = "VoxelShowcase/ShowcaseWorld.manifest";
        public const string ManifestAssetPath =
            "Assets/Resources/VoxelShowcase/ShowcaseWorld.manifest.txt";

        private const int ManifestVersion = 1;
        // Revision 7 replaces revision 6's constant-run, freestanding switchback topology with
        // shared core-aware tier geometry: the path cuts into the near mountain shell, upper runs
        // narrow with the tapered core, turn landings remain coincident, and residual supports bias
        // inward to merge with the coherent mountain mass.
        private const uint LandmarkContractRevision = 7;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// Changes automatically with the authored mountain dimensions/placement contract. Bump
        /// <see cref="LandmarkContractRevision"/> when the reusable realization algorithm changes
        /// without changing one of these parameters.
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

        public static string CreateManifest(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            return "version=" + ManifestVersion.ToString(CultureInfo.InvariantCulture) + "\n"
                 + "contentSignature=" + RequiredContentSignature.ToString("X8", CultureInfo.InvariantCulture) + "\n"
                 + "payloadSha256=" + ComputePayloadSha256(payload) + "\n";
        }

        public static void Validate(byte[] payload, string manifestText)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(manifestText))
                throw new InvalidDataException(
                    "VoxelShowcase startup bake provenance manifest is missing or empty. Re-bake the world.");

            int version = -1;
            uint contentSignature = 0;
            string payloadSha256 = null;

            string[] lines = manifestText.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                int separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                    throw new InvalidDataException(
                        "VoxelShowcase startup bake manifest contains a malformed line.");

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                switch (key)
                {
                    case "version":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version))
                            throw new InvalidDataException("VoxelShowcase startup bake manifest has an invalid version.");
                        break;
                    case "contentSignature":
                        if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out contentSignature))
                            throw new InvalidDataException(
                                "VoxelShowcase startup bake manifest has an invalid content signature.");
                        break;
                    case "payloadSha256":
                        payloadSha256 = value;
                        break;
                    default:
                        throw new InvalidDataException(
                            "VoxelShowcase startup bake manifest contains unknown field '" + key + "'.");
                }
            }

            if (version != ManifestVersion)
                throw new InvalidDataException(
                    "VoxelShowcase startup bake manifest version " + version
                    + " is not supported. Re-bake the world.");
            if (contentSignature != RequiredContentSignature)
                throw new InvalidDataException(
                    "VoxelShowcase startup bake content signature is stale. Re-bake the world.");

            string actualSha256 = ComputePayloadSha256(payload);
            if (!string.Equals(payloadSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "VoxelShowcase startup bake bytes do not match their provenance manifest. Re-bake the world.");
        }

        public static string ComputePayloadSha256(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
                digest = sha.ComputeHash(payload);

            var text = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++)
                text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }

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
