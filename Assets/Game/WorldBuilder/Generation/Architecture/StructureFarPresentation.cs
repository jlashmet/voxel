using System;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Semantic visibility significance for a planned structure. This is world meaning, not a
    /// distance/tier decision: rendering composition remains responsible for deciding which proxy
    /// tier a class receives for a particular camera and budget.
    /// </summary>
    public enum StructureVisibilityClass : byte
    {
        OrdinaryStructure = 0,
        SettlementAnchor = 1,
        Landmark = 2,
        HorizonLandmark = 3,
    }

    /// <summary>
    /// Compact renderer-neutral facts needed to reconstruct or select a distant exterior proxy.
    /// No render object, material, camera, voxel residency, collision, or interior state is owned
    /// here. Bounds remain in deterministic world decimetres, matching WorldBuilder planning.
    /// </summary>
    public readonly struct StructureFarPresentation : IEquatable<StructureFarPresentation>
    {
        public readonly ulong StructureKey;
        public readonly ulong SettlementKey;
        public readonly Int2 FootprintMinDm;
        public readonly Int2 FootprintMaxDm;
        public readonly int HeightDm;
        public readonly FrontageDirection Facing;
        public readonly StructureArchetype Archetype;
        public readonly ulong ArchitectureKey;
        public readonly ulong MaterialFamilyKey;
        public readonly StructureVisibilityClass VisibilityClass;
        public readonly ulong Revision;

        public StructureFarPresentation(
            ulong structureKey,
            ulong settlementKey,
            Int2 footprintMinDm,
            Int2 footprintMaxDm,
            int heightDm,
            FrontageDirection facing,
            StructureArchetype archetype,
            ulong architectureKey,
            ulong materialFamilyKey,
            StructureVisibilityClass visibilityClass,
            ulong revision)
        {
            if (footprintMaxDm.X <= footprintMinDm.X)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));
            if (footprintMaxDm.Y <= footprintMinDm.Y)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));
            if (heightDm <= 0) throw new ArgumentOutOfRangeException(nameof(heightDm));

            StructureKey = structureKey;
            SettlementKey = settlementKey;
            FootprintMinDm = footprintMinDm;
            FootprintMaxDm = footprintMaxDm;
            HeightDm = heightDm;
            Facing = facing;
            Archetype = archetype;
            ArchitectureKey = architectureKey;
            MaterialFamilyKey = materialFamilyKey;
            VisibilityClass = visibilityClass;
            Revision = revision;
        }

        public bool Equals(StructureFarPresentation other) =>
            StructureKey == other.StructureKey
            && SettlementKey == other.SettlementKey
            && FootprintMinDm.X == other.FootprintMinDm.X
            && FootprintMinDm.Y == other.FootprintMinDm.Y
            && FootprintMaxDm.X == other.FootprintMaxDm.X
            && FootprintMaxDm.Y == other.FootprintMaxDm.Y
            && HeightDm == other.HeightDm
            && Facing == other.Facing
            && Archetype == other.Archetype
            && ArchitectureKey == other.ArchitectureKey
            && MaterialFamilyKey == other.MaterialFamilyKey
            && VisibilityClass == other.VisibilityClass
            && Revision == other.Revision;

        public override bool Equals(object obj) =>
            obj is StructureFarPresentation other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)(StructureKey ^ (StructureKey >> 32));
                hash = hash * 397 ^ (int)(Revision ^ (Revision >> 32));
                return hash;
            }
        }

        public static bool operator ==(StructureFarPresentation left, StructureFarPresentation right) =>
            left.Equals(right);

        public static bool operator !=(StructureFarPresentation left, StructureFarPresentation right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Resolves far-presentation facts from existing semantic planning/architecture outputs.
    /// Settlement identity and an optional visibility override are supplied as semantic policy;
    /// neither is inferred from scene coordinates or camera state.
    /// </summary>
    public static class StructureFarPresentationResolver
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static StructureFarPresentation Resolve(
            string settlementId,
            StructureIntent intent,
            StructureForm form,
            StructureSiteGeometry site,
            StructureGeometryProfile geometryProfile,
            ArchitectureTheme theme,
            StructureVisibilityClass? visibilityOverride = null)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
                throw new ArgumentException("Settlement id is required for stable far identity.", nameof(settlementId));
            if (intent.RoleId != form.RoleId
                || intent.Archetype != form.Archetype
                || intent.District != form.District)
                throw new InvalidOperationException(
                    "Far presentation cannot combine mismatched structure intent and form identity.");

            ulong settlementKey = HashString(FnvOffset, settlementId);
            ulong structureKey = settlementKey;
            structureKey = HashInt(structureKey, intent.RoleId);
            structureKey = HashByte(structureKey, (byte)intent.Archetype);

            ulong architectureKey = HashString(FnvOffset, intent.StyleId ?? string.Empty);
            architectureKey = HashByte(architectureKey, (byte)form.Footprint);
            architectureKey = HashByte(architectureKey, (byte)form.Roof);

            ulong materialFamilyKey = HashString(FnvOffset, theme.Id ?? string.Empty);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.Foundation);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.Wall);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.Frame);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.Window);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.Roof);
            materialFamilyKey = HashByte(materialFamilyKey, (byte)theme.AccentStone);

            int heightDm = ResolveHeightDm(intent, form, theme);
            StructureVisibilityClass visibility = visibilityOverride
                ?? DefaultVisibility(intent.Archetype);

            ulong revision = structureKey;
            revision = HashInt(revision, site.FootprintMinDm.X);
            revision = HashInt(revision, site.FootprintMinDm.Y);
            revision = HashInt(revision, site.FootprintMaxDm.X);
            revision = HashInt(revision, site.FootprintMaxDm.Y);
            revision = HashInt(revision, heightDm);
            revision = HashByte(revision, (byte)site.PublicEntranceFacing);
            revision = HashByte(revision, (byte)form.Mode);
            revision = HashByte(revision, (byte)form.FrontageRhythm);
            revision = HashByte(revision, (byte)form.WindowTreatment);
            revision = HashInt(revision, form.WidthDm);
            revision = HashInt(revision, form.DepthDm);
            revision = HashInt(revision, form.Storeys);
            revision = HashInt(revision, form.DoorOffsetDm);
            revision = HashInt(revision, form.UpperOverhangDm);
            revision = HashInt(revision, form.RoofHeightDm);
            revision = HashInt(revision, form.WingWidthDm);
            revision = HashInt(revision, form.WingDepthDm);
            revision = HashByte(revision, form.WingOnRight ? (byte)1 : (byte)0);
            revision = HashByte(revision, form.ChimneyOnRight ? (byte)1 : (byte)0);
            revision = HashInt(revision, geometryProfile.FoundationCornerRadiusDm);
            revision = HashInt(revision, geometryProfile.ShellCornerRadiusDm);
            revision = HashInt(revision, geometryProfile.OpeningCornerRadiusDm);
            revision = HashInt(revision, geometryProfile.DetailCornerRadiusDm);
            revision = HashByte(revision, (byte)geometryProfile.FoundationSurface);
            revision = HashByte(revision, (byte)geometryProfile.ShellSurface);
            revision = HashByte(revision, (byte)geometryProfile.OpeningSurface);
            revision = HashByte(revision, (byte)geometryProfile.DetailSurface);
            revision = HashByte(revision, (byte)geometryProfile.RoofSurface);
            revision = HashUlong(revision, architectureKey);
            revision = HashUlong(revision, materialFamilyKey);
            revision = HashByte(revision, (byte)visibility);

            return new StructureFarPresentation(
                structureKey,
                settlementKey,
                site.FootprintMinDm,
                site.FootprintMaxDm,
                heightDm,
                site.PublicEntranceFacing,
                intent.Archetype,
                architectureKey,
                materialFamilyKey,
                visibility,
                revision);
        }

        public static StructureVisibilityClass DefaultVisibility(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Church:
                    return StructureVisibilityClass.Landmark;
                case StructureArchetype.Mansion:
                    return StructureVisibilityClass.SettlementAnchor;
                default:
                    return StructureVisibilityClass.OrdinaryStructure;
            }
        }

        private static int ResolveHeightDm(
            StructureIntent intent,
            StructureForm form,
            ArchitectureTheme theme)
        {
            int envelopeHeight = Math.Max(1, intent.EnvelopeDm.Y);
            if (!form.IsGenerated) return envelopeHeight;

            long resolved = (long)theme.FoundationHeightDm
                          + (long)Math.Max(1, form.Storeys) * theme.FloorHeightDm
                          + Math.Max(0, form.RoofHeightDm);
            if (resolved <= 0) return envelopeHeight;
            return (int)Math.Min(envelopeHeight, Math.Min(int.MaxValue, resolved));
        }

        private static ulong HashString(ulong hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                ushort c = value[i];
                hash = HashByte(hash, (byte)c);
                hash = HashByte(hash, (byte)(c >> 8));
            }
            return HashByte(hash, 0xFF);
        }

        private static ulong HashInt(ulong hash, int value) =>
            HashUlong(hash, unchecked((uint)value));

        private static ulong HashUlong(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
                hash = HashByte(hash, (byte)(value >> shift));
            return hash;
        }

        private static ulong HashByte(ulong hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }
    }
}
