using System.Runtime.CompilerServices;
using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Shared policy for authored numeric values. Configuration code must opt into clamping;
    /// rejection is the default so invalid content cannot silently change shape.
    /// </summary>
    public enum StructureValidationPolicy : byte
    {
        Reject = 0,
        Clamp = 1,
    }

    /// <summary>Result of applying a shared authoring validation rule.</summary>
    public enum StructureValidationResult : byte
    {
        Valid = 0,
        Clamped = 1,
        Rejected = 2,
    }

    /// <summary>
    /// Common integer validation helpers for worldbuilding configuration.
    ///
    /// All limits are inclusive. Invalid rule definitions are always rejected, regardless of the
    /// requested policy. Callers may explicitly clamp user-facing authored values, while catalogue
    /// validation and compatibility presets should normally reject invalid values.
    /// </summary>
    public static class StructureConfigValidation
    {
        public static StructureValidationResult Dimension(
            int value,
            int minimum,
            int maximum,
            StructureValidationPolicy policy,
            out int resolved)
        {
            resolved = value;
            if (minimum <= 0 || maximum < minimum)
                return StructureValidationResult.Rejected;

            if (value >= minimum && value <= maximum)
                return StructureValidationResult.Valid;

            if (policy != StructureValidationPolicy.Clamp)
                return StructureValidationResult.Rejected;

            resolved = value < minimum ? minimum : maximum;
            return StructureValidationResult.Clamped;
        }

        public static StructureValidationResult OrderedRange(
            int authoredMinimum,
            int authoredMaximum,
            int allowedMinimum,
            int allowedMaximum,
            StructureValidationPolicy policy,
            out int resolvedMinimum,
            out int resolvedMaximum)
        {
            resolvedMinimum = authoredMinimum;
            resolvedMaximum = authoredMaximum;

            if (allowedMaximum < allowedMinimum || authoredMaximum < authoredMinimum)
                return StructureValidationResult.Rejected;

            bool inside = authoredMinimum >= allowedMinimum && authoredMaximum <= allowedMaximum;
            if (inside)
                return StructureValidationResult.Valid;

            if (policy != StructureValidationPolicy.Clamp)
                return StructureValidationResult.Rejected;

            resolvedMinimum = authoredMinimum < allowedMinimum ? allowedMinimum : authoredMinimum;
            resolvedMaximum = authoredMaximum > allowedMaximum ? allowedMaximum : authoredMaximum;

            if (resolvedMinimum > resolvedMaximum)
                return StructureValidationResult.Rejected;

            return StructureValidationResult.Clamped;
        }
    }

    /// <summary>
    /// Archetype-neutral material meanings used by reusable architectural components. Values are
    /// resolved to opaque voxel material ids by <see cref="StructureMaterialPalette"/>.
    /// </summary>
    public enum StructureMaterialRole : byte
    {
        Foundation = 0,
        PrimaryWall = 1,
        SecondaryWall = 2,
        Trim = 3,
        Roof = 4,
        Floor = 5,
        Column = 6,
        Accent = 7,
        Underground = 8,
        Opening = 9,
        Glass = 10,
        Detail = 11,
    }

    /// <summary>
    /// Blittable semantic palette shared by structure components. The engine continues to treat
    /// material ids as opaque bytes; game content decides which ids satisfy each semantic role.
    /// </summary>
    public struct StructureMaterialPalette
    {
        public byte Foundation;
        public byte PrimaryWall;
        public byte SecondaryWall;
        public byte Trim;
        public byte Roof;
        public byte Floor;
        public byte Column;
        public byte Accent;
        public byte Underground;
        public byte Opening;
        public byte Glass;
        public byte Detail;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Resolve(StructureMaterialRole role)
        {
            switch (role)
            {
                case StructureMaterialRole.Foundation: return Foundation;
                case StructureMaterialRole.PrimaryWall: return PrimaryWall;
                case StructureMaterialRole.SecondaryWall: return SecondaryWall;
                case StructureMaterialRole.Trim: return Trim;
                case StructureMaterialRole.Roof: return Roof;
                case StructureMaterialRole.Floor: return Floor;
                case StructureMaterialRole.Column: return Column;
                case StructureMaterialRole.Accent: return Accent;
                case StructureMaterialRole.Underground: return Underground;
                case StructureMaterialRole.Opening: return Opening;
                case StructureMaterialRole.Glass: return Glass;
                case StructureMaterialRole.Detail: return Detail;
                default: return PrimaryWall;
            }
        }
    }

    /// <summary>
    /// Stable semantic sub-seed derivation. Unlike consuming a mutable RNG stream, a child seed
    /// depends only on its parent seed, semantic key, and explicit ordinal, so adding an unrelated
    /// detail does not reshuffle existing children.
    /// </summary>
    public static class StructureSeed
    {
        private const ulong SemanticSalt = 0xD6E8FEB86659FD93ul;
        private const ulong ByteSalt = 0x9E3779B97F4A7C15ul;
        private const ulong OrdinalSalt = 0xC2B2AE3D27D4EB4Ful;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Child(ulong parentSeed, in FixedString64Bytes semanticKey, int ordinal = 0)
        {
            ulong hash = FeatureHash.Mix(parentSeed ^ SemanticSalt);
            for (var i = 0; i < semanticKey.Length; i++)
                hash = FeatureHash.Mix(hash ^ ((ulong)semanticKey[i] + ByteSalt + (ulong)(uint)i));

            hash = FeatureHash.Mix(hash ^ ((ulong)(uint)semanticKey.Length * ByteSalt));
            return FeatureHash.Mix(hash ^ ((ulong)(uint)ordinal * OrdinalSalt));
        }
    }
}
