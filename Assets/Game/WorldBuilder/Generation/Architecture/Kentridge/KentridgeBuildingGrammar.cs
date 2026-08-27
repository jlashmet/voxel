using System;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    public enum StructureGenerationMode : byte { Generated, Bespoke }
    public enum FootprintForm : byte { Rectangle, RearWing, SideWing, SteppedUpper }
    public enum RoofForm : byte { Gable, SteepGable, TwinGable, GableWithLeanTo }
    public enum FrontageRhythm : byte { TwoBay, ThreeBay, Asymmetric }
    public enum WindowTreatment : byte { Glass, Warm, Open }

    /// <summary>
    /// Detailed, renderer-independent result produced by the architectural generation layer.
    /// Settlement planning never authors this type directly.
    /// </summary>
    public readonly struct StructureForm
    {
        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly StructureGenerationMode Mode;
        public readonly FootprintForm Footprint;
        public readonly RoofForm Roof;
        public readonly FrontageRhythm FrontageRhythm;
        public readonly WindowTreatment WindowTreatment;
        public readonly int WidthDm;
        public readonly int DepthDm;
        public readonly int Storeys;
        public readonly int DoorOffsetDm;
        public readonly int UpperOverhangDm;
        public readonly int RoofHeightDm;
        public readonly int WingWidthDm;
        public readonly int WingDepthDm;
        public readonly bool WingOnRight;
        public readonly bool ChimneyOnRight;

        public StructureForm(
            int roleId, StructureArchetype archetype, DistrictKind district,
            StructureGenerationMode mode, FootprintForm footprint, RoofForm roof,
            FrontageRhythm frontageRhythm, WindowTreatment windowTreatment,
            int widthDm, int depthDm, int storeys, int doorOffsetDm,
            int upperOverhangDm, int roofHeightDm, int wingWidthDm, int wingDepthDm,
            bool wingOnRight, bool chimneyOnRight)
        {
            RoleId = roleId;
            Archetype = archetype;
            District = district;
            Mode = mode;
            Footprint = footprint;
            Roof = roof;
            FrontageRhythm = frontageRhythm;
            WindowTreatment = windowTreatment;
            WidthDm = widthDm;
            DepthDm = depthDm;
            Storeys = storeys;
            DoorOffsetDm = doorOffsetDm;
            UpperOverhangDm = upperOverhangDm;
            RoofHeightDm = roofHeightDm;
            WingWidthDm = wingWidthDm;
            WingDepthDm = wingDepthDm;
            WingOnRight = wingOnRight;
            ChimneyOnRight = chimneyOnRight;
        }

        public bool IsGenerated => Mode == StructureGenerationMode.Generated;
        public bool IsShop => Archetype == StructureArchetype.Shop;
        public bool IsHospitality => Archetype == StructureArchetype.Inn;
    }

    /// <summary>
    /// Public handoff from high-level settlement intent to lower-level architectural detail.
    /// The settlement chooses a style; a registry supplies the style-specific compiler.
    /// </summary>
    public static class ArchitectureCompiler
    {
        public static StructureForm Resolve(
            StructureIntent intent,
            ArchitectureTheme theme,
            uint seed) =>
            Resolve(intent, theme, seed, BuiltInArchitectureStyles.Registry);

        public static StructureForm Resolve(
            StructureIntent intent,
            ArchitectureTheme theme,
            uint seed,
            ArchitectureStyleRegistry styles)
        {
            if (styles == null) throw new ArgumentNullException(nameof(styles));
            IArchitectureStyleCompiler compiler = styles.Require(intent.StyleId);
            StructureForm form = compiler.ResolveStructure(intent, theme, seed);
            ValidateGenerated(intent, theme, form, styles);
            return form;
        }

        public static void ValidateGenerated(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form) =>
            ValidateGenerated(intent, theme, form, BuiltInArchitectureStyles.Registry);

        public static void ValidateGenerated(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form,
            ArchitectureStyleRegistry styles)
        {
            if (styles == null) throw new ArgumentNullException(nameof(styles));
            IArchitectureStyleCompiler style = styles.Require(intent.StyleId);
            int envelopeClearanceDm = style is IStructureEnvelopeClearancePolicy clearancePolicy
                ? clearancePolicy.GeneratedStructureClearanceDm
                : 12;
            if (envelopeClearanceDm < 0)
                throw new InvalidOperationException(
                    "Architecture style requested negative structure-envelope clearance.");

            if (form.RoleId != intent.RoleId
                || form.Archetype != intent.Archetype
                || form.District != intent.District)
                throw new InvalidOperationException(
                    "Architecture compiler changed high-level structure identity.");

            if (form.IsGenerated)
            {
                if (form.Storeys < 1)
                    throw new InvalidOperationException(
                        "Generated architecture must contain at least one storey.");
                if (form.WidthDm <= 0 || form.DepthDm <= 0 || form.RoofHeightDm <= 0)
                    throw new InvalidOperationException(
                        "Generated architecture contains non-positive dimensions.");

                int lateralExtent = form.WidthDm
                                  + 2 * form.UpperOverhangDm
                                  + 2 * theme.RoofOverhangDm;
                int depthExtent = form.DepthDm
                                + form.UpperOverhangDm
                                + 2 * theme.RoofOverhangDm;
                if (lateralExtent > intent.EnvelopeDm.X - envelopeClearanceDm
                    || depthExtent > intent.EnvelopeDm.Z - envelopeClearanceDm)
                    throw new InvalidOperationException(
                        "Generated architecture escaped its high-level structure envelope: " +
                        "lateral " + lateralExtent + " vs " +
                        (intent.EnvelopeDm.X - envelopeClearanceDm) +
                        ", depth " + depthExtent + " vs " +
                        (intent.EnvelopeDm.Z - envelopeClearanceDm) +
                        " (width " + form.WidthDm + ", depth " + form.DepthDm +
                        ", upperOverhang " + form.UpperOverhangDm +
                        ", roofOverhang " + theme.RoofOverhangDm +
                        ", clearance " + envelopeClearanceDm + ").");

                if (form.Footprint == FootprintForm.RearWing && form.WingDepthDm <= 0)
                    throw new InvalidOperationException("Rear-wing form is missing its wing.");
                if (form.Footprint == FootprintForm.SideWing && form.WingWidthDm <= 0)
                    throw new InvalidOperationException("Side-wing form is missing its wing.");
            }

            style.ValidateStructure(intent, theme, form);
        }
    }

    /// <summary>Kentridge style implementation hidden behind the registered style compiler.</summary>
    internal static class KentridgeStructureCompiler
    {
        private const int RoomPlanExpansionDm = 8;

        public static StructureForm Resolve(
            StructureIntent intent, ArchitectureTheme theme, uint seed)
        {
            KentridgeRole role = (KentridgeRole)intent.RoleId;
            switch (role)
            {
                case KentridgeRole.Inn:
                    return Generated(intent, FootprintForm.RearWing,
                        RoofForm.TwinGable, FrontageRhythm.ThreeBay,
                        WindowTreatment.Warm, 132, 104, 3, 0, 4, 30,
                        40, 36, true, true);
                case KentridgeRole.Pub:
                    return Generated(intent, FootprintForm.SideWing,
                        RoofForm.GableWithLeanTo, FrontageRhythm.Asymmetric,
                        WindowTreatment.Warm, 118, 96, 2, -12, 2, 24,
                        28, 42, false, false);
                case KentridgeRole.WeaponShop:
                    return Generated(intent, FootprintForm.RearWing,
                        RoofForm.GableWithLeanTo, FrontageRhythm.ThreeBay,
                        WindowTreatment.Glass, 94, 70, 2, -10, 0, 20,
                        30, 26, false, true);
                case KentridgeRole.ArmorShop:
                    return Generated(intent, FootprintForm.SideWing,
                        RoofForm.TwinGable, FrontageRhythm.TwoBay,
                        WindowTreatment.Glass, 84, 72, 2, 10, 2, 24,
                        22, 32, true, false);
                case KentridgeRole.MagicShop:
                    return Generated(intent, FootprintForm.SteppedUpper,
                        RoofForm.SteepGable, FrontageRhythm.Asymmetric,
                        WindowTreatment.Warm, 72, 68, 3, -8, 5, 32,
                        0, 0, false, true);
                case KentridgeRole.MayorHouse:
                    return Generated(intent, FootprintForm.RearWing,
                        RoofForm.TwinGable, FrontageRhythm.ThreeBay,
                        WindowTreatment.Warm, 90, 78, 3, 0, 4, 30,
                        28, 28, true, false);
                case KentridgeRole.AbandonedHouse:
                    return Generated(intent, FootprintForm.SideWing,
                        RoofForm.GableWithLeanTo, FrontageRhythm.Asymmetric,
                        WindowTreatment.Open, 66, 66, 2, -10, 0, 20,
                        18, 28, false, false);
            }

            if (!UsesGeneratedHouseGrammar(intent.Archetype))
                return Bespoke(intent);

            uint h = Hash(seed, intent.RoleId, (int)intent.Archetype, (int)intent.District);
            bool wide = intent.Archetype == StructureArchetype.WideHouse;
            int width = wide ? 84 + (int)(h % 13u) : 66 + (int)(h % 11u);
            int depth = wide ? 74 + (int)((h >> 5) % 13u) : 64 + (int)((h >> 5) % 11u);
            FootprintForm footprint = (FootprintForm)((h >> 9) % 4u);
            RoofForm roof = (RoofForm)((h >> 12) % 4u);
            FrontageRhythm rhythm = (FrontageRhythm)((h >> 15) % 3u);

            int storeys = 2;
            if ((intent.District == DistrictKind.Civic || intent.District == DistrictKind.Market)
                && ((h >> 18) & 1u) != 0)
                storeys = 3;

            int overhang = storeys > 1 ? (int)((h >> 20) % 3u) * 2 : 0;
            if (footprint == FootprintForm.SteppedUpper)
                overhang = Math.Max(overhang, 4);

            int wingWidth = 0;
            int wingDepth = 0;
            if (footprint == FootprintForm.RearWing)
            {
                wingWidth = wide ? 26 : 22;
                wingDepth = wide ? 28 : 24;
            }
            else if (footprint == FootprintForm.SideWing)
            {
                wingWidth = wide ? 22 : 18;
                wingDepth = wide ? 34 : 28;
            }

            int roofHeight = roof == RoofForm.SteepGable
                ? 30
                : 21 + (int)((h >> 23) % 6u);
            int doorOffset = ((int)((h >> 26) % 3u) - 1) * 8;

            return Generated(intent, footprint, roof, rhythm, WindowTreatment.Glass,
                width, depth, storeys, doorOffset, overhang, roofHeight,
                wingWidth, wingDepth, ((h >> 28) & 1u) != 0, ((h >> 29) & 1u) != 0);
        }

        private static bool UsesGeneratedHouseGrammar(StructureArchetype archetype)
        {
            return archetype == StructureArchetype.Townhouse
                || archetype == StructureArchetype.WideHouse
                || archetype == StructureArchetype.Shop
                || archetype == StructureArchetype.Inn;
        }

        private static StructureForm Generated(
            StructureIntent intent,
            FootprintForm footprint, RoofForm roof,
            FrontageRhythm rhythm, WindowTreatment windows,
            int width, int depth, int storeys, int doorOffset, int overhang,
            int roofHeight, int wingWidth, int wingDepth, bool wingRight, bool chimneyRight)
        {
            return new StructureForm(
                intent.RoleId, intent.Archetype, intent.District, StructureGenerationMode.Generated,
                footprint, roof, rhythm, windows,
                width + RoomPlanExpansionDm, depth + RoomPlanExpansionDm,
                storeys, doorOffset, overhang, roofHeight,
                wingWidth, wingDepth, wingRight, chimneyRight);
        }

        private static StructureForm Bespoke(StructureIntent intent)
        {
            return new StructureForm(
                intent.RoleId, intent.Archetype, intent.District, StructureGenerationMode.Bespoke,
                FootprintForm.Rectangle, RoofForm.Gable,
                FrontageRhythm.TwoBay, WindowTreatment.Glass,
                0, 0, 0, 0, 0, 0, 0, 0, false, false);
        }

        private static uint Hash(uint seed, int roleId, int archetype, int district)
        {
            uint h = seed
                   ^ ((uint)(roleId + 1) * 0x9E3779B9u)
                   ^ ((uint)(archetype + 7) * 0x85EBCA6Bu)
                   ^ ((uint)(district + 11) * 0xC2B2AE35u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
