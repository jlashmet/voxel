using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Reusable provenance sidecar for checked-in startup payloads. Callers own the semantic source
    /// signature; this helper only binds that signature and a format version to exact payload bytes.
    /// </summary>
    public static class StartupBakeProvenance
    {
        public static string CreateManifest(int version, uint contentSignature, byte[] payload)
        {
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            return "version=" + version.ToString(CultureInfo.InvariantCulture) + "\n"
                 + "contentSignature=" + contentSignature.ToString("X8", CultureInfo.InvariantCulture) + "\n"
                 + "payloadSha256=" + ComputePayloadSha256(payload) + "\n";
        }

        public static void Validate(
            int expectedVersion,
            uint expectedContentSignature,
            byte[] payload,
            string manifestText,
            string artifactName = "startup bake")
        {
            if (expectedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(manifestText))
                throw new InvalidDataException(artifactName + " provenance manifest is missing or empty. Re-bake the world.");

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
                    throw new InvalidDataException(artifactName + " provenance manifest contains a malformed line.");

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                switch (key)
                {
                    case "version":
                        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version))
                            throw new InvalidDataException(artifactName + " provenance manifest has an invalid version.");
                        break;
                    case "contentSignature":
                        if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out contentSignature))
                            throw new InvalidDataException(artifactName + " provenance manifest has an invalid content signature.");
                        break;
                    case "payloadSha256":
                        payloadSha256 = value;
                        break;
                    default:
                        throw new InvalidDataException(
                            artifactName + " provenance manifest contains unknown field '" + key + "'.");
                }
            }

            if (version != expectedVersion)
                throw new InvalidDataException(
                    artifactName + " provenance manifest version " + version
                    + " is not supported. Re-bake the world.");
            if (contentSignature != expectedContentSignature)
                throw new InvalidDataException(
                    artifactName + " provenance content signature is stale. Re-bake the world.");

            string actualSha256 = ComputePayloadSha256(payload);
            if (!string.Equals(payloadSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    artifactName + " bytes do not match their provenance manifest. Re-bake the world.");
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
    }
}
