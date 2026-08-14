using System;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Semantic opening kinds produced by the building grammar. These are architectural intents,
    /// not voxel geometry; a lower layer chooses the concrete reusable feature implementation.
    /// </summary>
    public enum BuildingOpeningKind : byte
    {
        Door,
        Window,
    }

    /// <summary>
    /// Optional reusable detail requested for an opening. ArchBay deliberately describes a socket
    /// rather than arch geometry so the existing arch generator can remain the single author of
    /// voussoirs, piers, imposts and masonry joints.
    /// </summary>
    public enum BuildingDetailSocketKind : byte
    {
        None,
        ArchBay,
    }

    public readonly struct BuildingOpening
    {
        public readonly BuildingOpeningKind Kind;
        public readonly BuildingDetailSocketKind DetailSocket;
        public readonly int Storey;
        public readonly int Bay;
        public readonly int CenterOffsetDm;
        public readonly int SillHeightDm;
        public readonly int WidthDm;
        public readonly int HeightDm;

        public BuildingOpening(
            BuildingOpeningKind kind,
            BuildingDetailSocketKind detailSocket,
            int storey,
            int bay,
            int centerOffsetDm,
            int sillHeightDm,
            int widthDm,
            int heightDm)
        {
            Kind = kind;
            DetailSocket = detailSocket;
            Storey = storey;
            Bay = bay;
            CenterOffsetDm = centerOffsetDm;
            SillHeightDm = sillHeightDm;
            WidthDm = widthDm;
            HeightDm = heightDm;
        }
    }

    /// <summary>
    /// Renderer-independent semantic composition for one generated building. StructureForm owns
    /// massing; this form adds storey and facade organization plus sockets for reusable details.
    /// </summary>
    public readonly struct BuildingCompositionForm
    {
        public readonly StructureForm Massing;
        public readonly int StoreyHeightDm;
        public readonly int BayCount;
        public readonly int BayWidthDm;
        public readonly BuildingOpening[] Openings;

        public BuildingCompositionForm(
            StructureForm massing,
            int storeyHeightDm,
            int bayCount,
            int bayWidthDm,
            BuildingOpening[] openings)
        {
            Massing = massing;
            StoreyHeightDm = storeyHeightDm;
            BayCount = bayCount;
            BayWidthDm = bayWidthDm;
            Openings = openings ?? Array.Empty<BuildingOpening>();
        }
    }

    /// <summary>
    /// Expands generated massing into a deterministic facade grammar. It never places world-space
    /// voxels or hardcodes a particular building. All coordinates are local to the generated
    /// frontage and are derived from the form's dimensions, rhythm and seed.
    /// </summary>
    public static class BuildingCompositionCompiler
    {
        public static BuildingCompositionForm Resolve(StructureForm form, uint seed)
        {
            if (!form.IsGenerated)
                return new BuildingCompositionForm(form, 0, 0, 0, Array.Empty<BuildingOpening>());

            int bayCount = ResolveBayCount(form.FrontageRhythm, form.WidthDm);
            int sideMarginDm = Math.Max(6, Math.Min(12, form.WidthDm / 10));
            int usableWidthDm = Math.Max(bayCount * 12, form.WidthDm - 2 * sideMarginDm);
            int bayWidthDm = usableWidthDm / bayCount;
            int storeyHeightDm = form.Storeys >= 3 ? 27 : 29;

            int doorBay = ResolveDoorBay(form, bayCount, bayWidthDm);
            bool archDoor = WantsArchPortal(form, seed, bayWidthDm, storeyHeightDm);

            // One opening per facade bay per storey. Ground-floor door replaces the window in its
            // bay; the remaining cells become windows. This makes the grammar easy to extend later
            // with paired windows, arcades, galleries and shopfront substitutions.
            var openings = new BuildingOpening[bayCount * form.Storeys];
            int cursor = 0;
            for (int storey = 0; storey < form.Storeys; storey++)
            {
                for (int bay = 0; bay < bayCount; bay++)
                {
                    int center = BayCenterOffsetDm(bay, bayCount, bayWidthDm);
                    if (storey == 0 && bay == doorBay)
                    {
                        int doorWidth = ClampEven(Math.Min(18, bayWidthDm - 8), 10, 18);
                        int doorHeight = Math.Min(24, storeyHeightDm - 3);
                        openings[cursor++] = new BuildingOpening(
                            BuildingOpeningKind.Door,
                            archDoor ? BuildingDetailSocketKind.ArchBay : BuildingDetailSocketKind.None,
                            storey, bay, center, 0, doorWidth, doorHeight);
                        continue;
                    }

                    int windowWidth = ClampEven(Math.Min(16, bayWidthDm - 8), 8, 16);
                    int sill = storey == 0 ? 8 : 7;
                    int windowHeight = Math.Max(10, Math.Min(16, storeyHeightDm - sill - 4));
                    openings[cursor++] = new BuildingOpening(
                        BuildingOpeningKind.Window,
                        BuildingDetailSocketKind.None,
                        storey, bay, center, sill, windowWidth, windowHeight);
                }
            }

            var result = new BuildingCompositionForm(
                form, storeyHeightDm, bayCount, bayWidthDm, openings);
            Validate(result);
            return result;
        }

        public static void Validate(BuildingCompositionForm form)
        {
            if (!form.Massing.IsGenerated) return;
            if (form.BayCount < 2 || form.BayWidthDm <= 0 || form.StoreyHeightDm <= 0)
                throw new InvalidOperationException("Generated building composition has invalid facade dimensions.");
            if (form.Openings == null || form.Openings.Length != form.BayCount * form.Massing.Storeys)
                throw new InvalidOperationException("Generated building composition has an incomplete facade grid.");

            int halfWidth = form.Massing.WidthDm / 2;
            for (int i = 0; i < form.Openings.Length; i++)
            {
                BuildingOpening opening = form.Openings[i];
                if (opening.Storey < 0 || opening.Storey >= form.Massing.Storeys
                    || opening.Bay < 0 || opening.Bay >= form.BayCount)
                    throw new InvalidOperationException("Building opening escaped its facade grid.");
                if (opening.WidthDm <= 0 || opening.HeightDm <= 0 || opening.SillHeightDm < 0)
                    throw new InvalidOperationException("Building opening has invalid dimensions.");
                if (Math.Abs(opening.CenterOffsetDm) + opening.WidthDm / 2 > halfWidth)
                    throw new InvalidOperationException("Building opening escaped the generated frontage.");
                if (opening.SillHeightDm + opening.HeightDm > form.StoreyHeightDm)
                    throw new InvalidOperationException("Building opening escaped its storey.");
                if (opening.DetailSocket == BuildingDetailSocketKind.ArchBay
                    && opening.Kind != BuildingOpeningKind.Door)
                    throw new InvalidOperationException("Arch detail socket is attached to an unsupported opening.");
            }
        }

        private static int ResolveBayCount(FrontageRhythm rhythm, int widthDm)
        {
            int requested = rhythm == FrontageRhythm.TwoBay ? 2 : 3;
            // Very wide fronts can carry an additional bay without changing the high-level rhythm.
            if (widthDm >= 108 && rhythm != FrontageRhythm.TwoBay)
                requested++;
            return Math.Max(2, requested);
        }

        private static int ResolveDoorBay(StructureForm form, int bayCount, int bayWidthDm)
        {
            int bestBay = 0;
            int bestDistance = int.MaxValue;
            for (int bay = 0; bay < bayCount; bay++)
            {
                int center = BayCenterOffsetDm(bay, bayCount, bayWidthDm);
                int distance = Math.Abs(center - form.DoorOffsetDm);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestBay = bay;
            }
            return bestBay;
        }

        private static int BayCenterOffsetDm(int bay, int bayCount, int bayWidthDm)
        {
            // Twice-the-coordinate arithmetic keeps symmetric even/odd bay layouts deterministic
            // without introducing floats into generation.
            return ((2 * bay + 1 - bayCount) * bayWidthDm) / 2;
        }

        private static bool WantsArchPortal(
            StructureForm form, uint seed, int bayWidthDm, int storeyHeightDm)
        {
            if (bayWidthDm < 22 || storeyHeightDm < 26)
                return false;

            // Inns and shops strongly prefer a reusable arch portal. Houses receive one only as a
            // seeded variation, keeping arches meaningful rather than stamping them everywhere.
            if (form.Archetype == StructureArchetype.Inn
                || form.Archetype == StructureArchetype.Shop)
                return true;

            uint h = seed
                   ^ ((uint)(form.RoleId + 17) * 0x9E3779B9u)
                   ^ ((uint)(form.WidthDm + 31) * 0x85EBCA6Bu)
                   ^ ((uint)(form.DepthDm + 47) * 0xC2B2AE35u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h % 5u == 0u;
        }

        private static int ClampEven(int value, int min, int max)
        {
            value = Math.Max(min, Math.Min(max, value));
            return value & ~1;
        }
    }
}
