using System;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Semantic visibility classes used by composition/presentation policy. These values describe
    /// world meaning only; metre thresholds, camera state and device budgets belong to presentation.
    /// </summary>
    public enum StructureVisibilityClass : byte
    {
        OrdinaryStructure = 0,
        SettlementAnchor = 1,
        Landmark = 2,
        HorizonLandmark = 3,
    }

    /// <summary>
    /// Compact renderer-neutral description of the distant exterior identity of one planned
    /// structure. It contains enough deterministic semantic/massing data for a later presentation
    /// layer to select or reconstruct a proxy without loading voxel regions.
    /// </summary>
    public readonly struct StructureFarPresentation : IEquatable<StructureFarPresentation>
    {
        public readonly string StructureKey;
        public readonly string ClusterKey;
        public readonly Int2 FootprintMinDm;
        public readonly Int2 FootprintMaxDm;
        public readonly int HeightDm;
        public readonly FrontageDirection Facing;
        public readonly StructureArchetype Archetype;
        public readonly FootprintForm Footprint;
        public readonly RoofForm Roof;
        public readonly int Storeys;
        public readonly string ArchitectureFamilyKey;
        public readonly StructureVisibilityClass VisibilityClass;
        public readonly uint Revision;

        internal StructureFarPresentation(
            string structureKey,
            string clusterKey,
            Int2 footprintMinDm,
            Int2 footprintMaxDm,
            int heightDm,
            FrontageDirection facing,
            StructureArchetype archetype,
            FootprintForm footprint,
            RoofForm roof,
            int storeys,
            string architectureFamilyKey,
            StructureVisibilityClass visibilityClass,
            uint revision)
        {
            StructureKey = structureKey;
            ClusterKey = clusterKey;
            FootprintMinDm = footprintMinDm;
            FootprintMaxDm = footprintMaxDm;
            HeightDm = heightDm;
            Facing = facing;
            Archetype = archetype;
            Footprint = footprint;
            Roof = roof;
            Storeys = storeys;
            ArchitectureFamilyKey = architectureFamilyKey;
            VisibilityClass = visibilityClass;
            Revision = revision;
        }

        public bool Equals(StructureFarPresentation other) =>
            string.Equals(StructureKey, other.StructureKey, StringComparison.Ordinal)
            && string.Equals(ClusterKey, other.ClusterKey, StringComparison.Ordinal)
            && FootprintMinDm.X == other.FootprintMinDm.X
            && FootprintMinDm.Y == other.FootprintMinDm.Y
            && FootprintMaxDm.X == other.FootprintMaxDm.X
            && FootprintMaxDm.Y == other.FootprintMaxDm.Y
            && HeightDm == other.HeightDm
            && Facing == other.Facing
            && Archetype == other.Archetype
            && Footprint == other.Footprint
            && Roof == other.Roof
            && Storeys == other.Storeys
            && string.Equals(ArchitectureFamilyKey, other.ArchitectureFamilyKey, StringComparison.Ordinal)
            && VisibilityClass == other.VisibilityClass
            && Revision == other.Revision;

        public override bool Equals(object obj) =>
            obj is StructureFarPresentation other && Equals(other);

        public override int GetHashCode() => unchecked((int)Revision);

        public static bool operator ==(StructureFarPresentation left, StructureFarPresentation right) =>
            left.Equals(right);

        public static bool operator !=(StructureFarPresentation left, StructureFarPresentation right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Resolves far-presentation metadata from the same planning and architecture facts used by
    /// physical realization. Stable identity/cluster ownership and visibility class are semantic
    /// composition inputs; this shared resolver never infers importance from scene coordinates.
    /// </summary>
    public static class StructureFarPresentationResolver
    {
        public static StructureFarPresentation Resolve(
            string clusterKey,
            StructureIntent intent,
            StructureForm form,
            StructureSiteGeometry site,
            StructureGeometryProfile geometry,
            ArchitectureTheme theme,
            StructureVisibilityClass visibilityClass)
        {
            if (string.IsNullOrWhiteSpace(clusterKey))
                throw new ArgumentException("A stable settlement/cluster key is required.", nameof(clusterKey));
            if (form.RoleId != intent.RoleId
                || form.Archetype != intent.Archetype
                || form.District != intent.District)
                throw new ArgumentException("Structure form identity must match structure intent.", nameof(form));
            if (site.FootprintMaxDm.X <= site.FootprintMinDm.X
                || site.FootprintMaxDm.Y <= site.FootprintMinDm.Y)
                throw new ArgumentException("Structure site must expose a positive footprint.", nameof(site));
            if ((byte)visibilityClass > (byte)StructureVisibilityClass.HorizonLandmark)
                throw new ArgumentOutOfRangeException(nameof(visibilityClass));
            if (theme.FoundationHeightDm < 0 || theme.FloorHeightDm <= 0)
                throw new ArgumentException("Architecture theme has invalid vertical dimensions.", nameof(theme));
            if (form.Storeys <= 0 || form.RoofHeightDm < 0)
                throw new ArgumentException("Structure form has invalid vertical dimensions.", nameof(form));

            string structureKey = clusterKey + "/role-" + intent.RoleId;
            string architectureFamilyKey = BuildArchitectureFamilyKey(intent, geometry, theme);
            int heightDm = checked(
                theme.FoundationHeightDm
                + form.Storeys * theme.FloorHeightDm
                + form.RoofHeightDm);

            uint revision = ComputeRevision(
                structureKey,
                clusterKey,
                intent,
                form,
                site,
                geometry,
                theme,
                visibilityClass,
                architectureFamilyKey,
                heightDm);

            return new StructureFarPresentation(
                structureKey,
                clusterKey,
                site.FootprintMinDm,
                site.FootprintMaxDm,
                heightDm,
                site.PublicEntranceFacing,
                form.Archetype,
                form.Footprint,
                form.Roof,
                form.Storeys,
                architectureFamilyKey,
                visibilityClass,
                revision);
        }

        private static string BuildArchitectureFamilyKey(
            StructureIntent intent,
            StructureGeometryProfile geometry,
            ArchitectureTheme theme) =>
            intent.StyleId + "/" + theme.Id
            + "/materials-" + (byte)theme.Foundation
            + "-" + (byte)theme.Wall
            + "-" + (byte)theme.Frame
            + "-" + (byte)theme.Window
            + "-" + (byte)theme.Roof
            + "-" + (byte)theme.AccentStone
            + "/surfaces-" + (byte)geometry.FoundationSurface
            + "-" + (byte)geometry.ShellSurface
            + "-" + (byte)geometry.OpeningSurface
            + "-" + (byte)geometry.DetailSurface
            + "-" + (byte)geometry.RoofSurface;

        private static uint ComputeRevision(
            string structureKey,
            string clusterKey,
            StructureIntent intent,
            StructureForm form,
            StructureSiteGeometry site,
            StructureGeometryProfile geometry,
            ArchitectureTheme theme,
            StructureVisibilityClass visibilityClass,
            string architectureFamilyKey,
            int heightDm)
        {
            uint hash = 2166136261u;
            Hash(ref hash, structureKey);
            Hash(ref hash, clusterKey);
            Hash(ref hash, intent.StyleId);
            Hash(ref hash, intent.RoleId);
            Hash(ref hash, (int)intent.Archetype);
            Hash(ref hash, (int)intent.District);
            Hash(ref hash, intent.PositionDm.X);
            Hash(ref hash, intent.PositionDm.Y);
            Hash(ref hash, (int)intent.Frontage);
            Hash(ref hash, intent.EnvelopeDm.X);
            Hash(ref hash, intent.EnvelopeDm.Y);
            Hash(ref hash, intent.EnvelopeDm.Z);

            Hash(ref hash, (int)form.Mode);
            Hash(ref hash, (int)form.Footprint);
            Hash(ref hash, (int)form.Roof);
            Hash(ref hash, (int)form.FrontageRhythm);
            Hash(ref hash, (int)form.WindowTreatment);
            Hash(ref hash, form.WidthDm);
            Hash(ref hash, form.DepthDm);
            Hash(ref hash, form.Storeys);
            Hash(ref hash, form.DoorOffsetDm);
            Hash(ref hash, form.UpperOverhangDm);
            Hash(ref hash, form.RoofHeightDm);
            Hash(ref hash, form.WingWidthDm);
            Hash(ref hash, form.WingDepthDm);
            Hash(ref hash, form.WingOnRight ? 1 : 0);
            Hash(ref hash, form.ChimneyOnRight ? 1 : 0);

            Hash(ref hash, site.FootprintMinDm.X);
            Hash(ref hash, site.FootprintMinDm.Y);
            Hash(ref hash, site.FootprintMaxDm.X);
            Hash(ref hash, site.FootprintMaxDm.Y);
            Hash(ref hash, site.PublicEntranceDm.X);
            Hash(ref hash, site.PublicEntranceDm.Y);
            Hash(ref hash, site.PublicEntranceHeightDm);
            Hash(ref hash, (int)site.PublicEntranceFacing);

            Hash(ref hash, geometry.FoundationCornerRadiusDm);
            Hash(ref hash, geometry.ShellCornerRadiusDm);
            Hash(ref hash, geometry.OpeningCornerRadiusDm);
            Hash(ref hash, geometry.DetailCornerRadiusDm);
            Hash(ref hash, (int)geometry.FoundationSurface);
            Hash(ref hash, (int)geometry.ShellSurface);
            Hash(ref hash, (int)geometry.OpeningSurface);
            Hash(ref hash, (int)geometry.DetailSurface);
            Hash(ref hash, (int)geometry.RoofSurface);

            Hash(ref hash, theme.Id);
            Hash(ref hash, (int)theme.Foundation);
            Hash(ref hash, (int)theme.Wall);
            Hash(ref hash, (int)theme.Frame);
            Hash(ref hash, (int)theme.Window);
            Hash(ref hash, (int)theme.Roof);
            Hash(ref hash, (int)theme.AccentStone);
            Hash(ref hash, theme.FoundationHeightDm);
            Hash(ref hash, theme.WallThicknessDm);
            Hash(ref hash, theme.FloorHeightDm);
            Hash(ref hash, theme.DoorHeightDm);
            Hash(ref hash, theme.WindowBaseDm);
            Hash(ref hash, theme.WindowHeightDm);
            Hash(ref hash, theme.BeamWidthDm);
            Hash(ref hash, theme.RoofOverhangDm);
            Hash(ref hash, theme.TypicalRoofHeightDm);
            Hash(ref hash, theme.GrandRoofHeightDm);
            Hash(ref hash, theme.UpperStoreyOverhangDm);
            Hash(ref hash, (int)visibilityClass);
            Hash(ref hash, architectureFamilyKey);
            Hash(ref hash, heightDm);
            return hash;
        }

        private static void Hash(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value;
                hash *= 16777619u;
                hash ^= (byte)(value >> 8);
                hash *= 16777619u;
                hash ^= (byte)(value >> 16);
                hash *= 16777619u;
                hash ^= (byte)(value >> 24);
                hash *= 16777619u;
            }
        }

        private static void Hash(ref uint hash, string value)
        {
            if (value == null)
            {
                Hash(ref hash, -1);
                return;
            }

            Hash(ref hash, value.Length);
            unchecked
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    hash ^= (byte)c;
                    hash *= 16777619u;
                    hash ^= (byte)(c >> 8);
                    hash *= 16777619u;
                }
            }
        }
    }
}
