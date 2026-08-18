using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Stable authoring identifier convention for reusable structure presets.
    ///
    /// IDs are metadata only: factories remain pure config constructors and generation must never
    /// look up hidden mutable state by ID. The convention is:
    ///     &lt;archetype&gt;.&lt;variant&gt;.v&lt;positive-version&gt;
    /// where archetype/variant use lowercase ASCII letters, digits and '-' only.
    ///
    /// Increment the version only when the named preset's default authored meaning changes in a way
    /// that callers may need to pin. Field overrides made by a caller do not create a new preset ID.
    /// </summary>
    public static class StructurePresetId
    {
        public const int CurrentConventionVersion = 1;

        public static bool IsWellFormed(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            int firstDot = id.IndexOf('.');
            if (firstDot <= 0 || firstDot == id.Length - 1) return false;
            int secondDot = id.IndexOf('.', firstDot + 1);
            if (secondDot <= firstDot + 1 || secondDot == id.Length - 1) return false;
            if (id.IndexOf('.', secondDot + 1) >= 0) return false;

            if (!ValidNameSegment(id, 0, firstDot) ||
                !ValidNameSegment(id, firstDot + 1, secondDot - firstDot - 1))
                return false;

            int versionStart = secondDot + 1;
            if (id[versionStart] != 'v' || versionStart + 1 >= id.Length) return false;

            int version = 0;
            for (int i = versionStart + 1; i < id.Length; i++)
            {
                char c = id[i];
                if (c < '0' || c > '9') return false;
                int digit = c - '0';
                if (version > (int.MaxValue - digit) / 10) return false;
                version = version * 10 + digit;
            }
            return version > 0;
        }

        public static int Version(string id)
        {
            if (!IsWellFormed(id))
                throw new ArgumentException("Preset ID must match <archetype>.<variant>.v<positive-version>.", nameof(id));
            int marker = id.LastIndexOf(".v", StringComparison.Ordinal);
            return int.Parse(id.Substring(marker + 2));
        }

        private static bool ValidNameSegment(string value, int start, int length)
        {
            if (length <= 0 || value[start] == '-' || value[start + length - 1] == '-') return false;
            bool previousDash = false;
            for (int i = start; i < start + length; i++)
            {
                char c = value[i];
                bool letter = c >= 'a' && c <= 'z';
                bool digit = c >= '0' && c <= '9';
                bool dash = c == '-';
                if (!letter && !digit && !dash) return false;
                if (dash && previousDash) return false;
                previousDash = dash;
            }
            return true;
        }
    }

    /// <summary>Engine-owned reusable preset IDs. These are metadata aliases for pure factories.</summary>
    public static class StructurePresetIds
    {
        public const string HouseCottageCompatibilityV1 = "house.cottage-compatibility.v1";
        public const string HouseCompactCabinV1 = "house.compact-cabin.v1";
        public const string HouseFarmhouseV1 = "house.farmhouse.v1";
        public const string HouseTallTownhouseV1 = "house.tall-townhouse.v1";
        public const string CaveDefaultV1 = "cave.default.v1";
    }
}
