using System;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Detailed local result for one anonymous frontage site. This type belongs to architectural
    /// generation; the settlement layer supplies only <see cref="UrbanFabricIntent"/>.
    /// </summary>
    public readonly struct UrbanFabricForm
    {
        public readonly int WidthDm;
        public readonly int DepthDm;
        public readonly int Storeys;
        public readonly int UpperOverhangDm;
        public readonly int RoofHeightDm;
        public readonly RoofForm Roof;
        public readonly FrontageRhythm FrontageRhythm;
        public readonly WindowTreatment WindowTreatment;
        public readonly bool HasAwning;
        public readonly bool ChimneyOnRight;
        public readonly bool AnnexOnRight;

        public UrbanFabricForm(
            int widthDm,
            int depthDm,
            int storeys,
            int upperOverhangDm,
            int roofHeightDm,
            RoofForm roof,
            FrontageRhythm frontageRhythm,
            WindowTreatment windowTreatment,
            bool hasAwning,
            bool chimneyOnRight,
            bool annexOnRight)
        {
            WidthDm = widthDm;
            DepthDm = depthDm;
            Storeys = storeys;
            UpperOverhangDm = upperOverhangDm;
            RoofHeightDm = roofHeightDm;
            Roof = roof;
            FrontageRhythm = frontageRhythm;
            WindowTreatment = windowTreatment;
            HasAwning = hasAwning;
            ChimneyOnRight = chimneyOnRight;
            AnnexOnRight = annexOnRight;
        }
    }

    /// <summary>
    /// Generic anonymous-frontage handoff. Settlement code controls massing constraints; a style
    /// registry supplies local dimensions, roof, facade rhythm and small details.
    /// </summary>
    public static class UrbanFabricCompiler
    {
        public static UrbanFabricForm Resolve(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex) =>
            Resolve(intent, seed, runIndex, siteIndex, BuiltInArchitectureStyles.Registry);

        public static UrbanFabricForm Resolve(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex,
            ArchitectureStyleRegistry styles)
        {
            if (styles == null) throw new ArgumentNullException(nameof(styles));
            IArchitectureStyleCompiler compiler = styles.Require(intent.StyleId);
            UrbanFabricForm form = compiler.ResolveUrbanFabric(intent, seed, runIndex, siteIndex);
            Validate(intent, form, styles);
            return form;
        }

        public static void Validate(UrbanFabricIntent intent, UrbanFabricForm form) =>
            Validate(intent, form, BuiltInArchitectureStyles.Registry);

        public static void Validate(
            UrbanFabricIntent intent,
            UrbanFabricForm form,
            ArchitectureStyleRegistry styles)
        {
            if (styles == null) throw new ArgumentNullException(nameof(styles));

            if (form.Storeys < intent.MinStoreys || form.Storeys > intent.MaxStoreys)
                throw new InvalidOperationException(
                    "Urban fabric escaped the settlement storey envelope.");
            if (form.WidthDm <= 0 || form.DepthDm <= 0 || form.RoofHeightDm <= 0)
                throw new InvalidOperationException(
                    "Urban fabric contains non-positive local dimensions.");

            styles.Require(intent.StyleId).ValidateUrbanFabric(intent, form);
        }
    }

    internal static class KentridgeUrbanFabricCompiler
    {
        public static UrbanFabricForm Resolve(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex)
        {
            uint h = Hash(
                seed,
                runIndex,
                siteIndex,
                (int)intent.District,
                intent.VariationContext);

            int storeys = intent.MinStoreys;
            if (intent.MaxStoreys > intent.MinStoreys)
                storeys += (int)(h % (uint)(intent.MaxStoreys - intent.MinStoreys + 1));

            int width = 56 + (int)((h >> 3) % 7u);
            int depth = 50 + (int)((h >> 7) % 9u);
            int overhang = ((h >> 12) & 1u) != 0 ? 2 : 0;
            RoofForm roof = (RoofForm)((h >> 13) % 4u);
            FrontageRhythm rhythm = (FrontageRhythm)((h >> 16) % 3u);

            WindowTreatment windows = WindowTreatment.Glass;
            if (intent.District == DistrictKind.Civic || intent.District == DistrictKind.Noble
                || (intent.District == DistrictKind.Market && ((h >> 19) & 1u) != 0))
                windows = WindowTreatment.Warm;

            int roofHeight = roof == RoofForm.SteepGable
                ? 29 + (int)((h >> 20) % 4u)
                : 20 + (int)((h >> 20) % 7u);
            bool awning = intent.District == DistrictKind.Market && ((h >> 24) & 1u) != 0;

            return new UrbanFabricForm(
                width,
                depth,
                storeys,
                overhang,
                roofHeight,
                roof,
                rhythm,
                windows,
                awning,
                ((h >> 25) & 1u) != 0,
                ((h >> 26) & 1u) != 0);
        }

        public static void Validate(UrbanFabricIntent intent, UrbanFabricForm form)
        {
            const int roofOverhangDm = 3;
            int lateral = form.WidthDm
                        + 2 * form.UpperOverhangDm
                        + 2 * roofOverhangDm;
            int depth = form.DepthDm
                      + form.UpperOverhangDm
                      + 2 * roofOverhangDm;
            if (lateral > intent.EnvelopeDm || depth > intent.EnvelopeDm)
                throw new InvalidOperationException(
                    "Urban fabric escaped its high-level frontage envelope.");
        }

        private static uint Hash(
            uint seed,
            int runIndex,
            int siteIndex,
            int district,
            int variationContext)
        {
            uint h = seed
                   ^ ((uint)(runIndex + 1) * 0x9E3779B9u)
                   ^ ((uint)(siteIndex + 1) * 0x85EBCA6Bu)
                   ^ ((uint)(district + 7) * 0xC2B2AE35u)
                   ^ ((uint)(variationContext + 13) * 0x27D4EB2Fu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}

namespace MountingForce.WorldGen.Content.Kentridge
{
    using MountingForce.WorldGen.Architecture;

    // Transitional source-compatible names. Stable-role generation is delegated to the generic
    // ArchitectureCompiler; these types contain no architectural generation decisions themselves.
    public enum KentridgeBuildingMode : byte { Generated, Bespoke }
    public enum KentridgeFootprintForm : byte { Rectangle, RearWing, SideWing, SteppedUpper }
    public enum KentridgeRoofForm : byte { Gable, SteepGable, TwinGable, GableWithLeanTo }
    public enum KentridgeFrontageRhythm : byte { TwoBay, ThreeBay, Asymmetric }
    public enum KentridgeWindowStyle : byte { Glass, Warm, Open }

    public readonly struct KentridgeBuildingForm
    {
        private readonly StructureForm _form;

        internal KentridgeBuildingForm(StructureForm form) { _form = form; }

        public int RoleId => _form.RoleId;
        public StructureArchetype Archetype => _form.Archetype;
        public DistrictKind District => _form.District;
        public KentridgeBuildingMode Mode => (KentridgeBuildingMode)_form.Mode;
        public KentridgeFootprintForm Footprint => (KentridgeFootprintForm)_form.Footprint;
        public KentridgeRoofForm Roof => (KentridgeRoofForm)_form.Roof;
        public KentridgeFrontageRhythm FrontageRhythm => (KentridgeFrontageRhythm)_form.FrontageRhythm;
        public KentridgeWindowStyle WindowStyle => (KentridgeWindowStyle)_form.WindowTreatment;
        public int WidthDm => _form.WidthDm;
        public int DepthDm => _form.DepthDm;
        public int Storeys => _form.Storeys;
        public int DoorOffsetDm => _form.DoorOffsetDm;
        public int UpperOverhangDm => _form.UpperOverhangDm;
        public int RoofHeightDm => _form.RoofHeightDm;
        public int WingWidthDm => _form.WingWidthDm;
        public int WingDepthDm => _form.WingDepthDm;
        public bool WingOnRight => _form.WingOnRight;
        public bool ChimneyOnRight => _form.ChimneyOnRight;
        public bool IsGenerated => _form.IsGenerated;
        public bool IsShop => _form.IsShop;
        public bool IsHospitality => _form.IsHospitality;

        internal StructureForm Inner => _form;
    }

    public static class KentridgeBuildingGrammar
    {
        public static KentridgeBuildingForm Resolve(BuildingPlot plot, uint seed)
        {
            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, KentridgeDefinition.Theme, seed);
            return new KentridgeBuildingForm(form);
        }

        public static void ValidateGenerated(KentridgeBuildingForm form)
        {
            var intent = new StructureIntent(
                form.RoleId,
                KentridgeDefinition.Id,
                form.Archetype,
                form.District,
                new Int2(0, 0),
                FrontageDirection.South,
                KentridgeDefinition.FootprintDm(form.Archetype));
            ArchitectureCompiler.ValidateGenerated(intent, KentridgeDefinition.Theme, form.Inner);
        }
    }

    /// <summary>
    /// Transitional wrapper for callers still using Kentridge-specific detail names. The generated
    /// form itself is owned by the generic Architecture layer.
    /// </summary>
    public readonly struct KentridgeUrbanFabricForm
    {
        private readonly UrbanFabricForm _form;

        internal KentridgeUrbanFabricForm(UrbanFabricForm form) { _form = form; }

        public int WidthDm => _form.WidthDm;
        public int DepthDm => _form.DepthDm;
        public int Storeys => _form.Storeys;
        public int UpperOverhangDm => _form.UpperOverhangDm;
        public int RoofHeightDm => _form.RoofHeightDm;
        public KentridgeRoofForm Roof => (KentridgeRoofForm)_form.Roof;
        public KentridgeFrontageRhythm FrontageRhythm =>
            (KentridgeFrontageRhythm)_form.FrontageRhythm;
        public KentridgeWindowStyle WindowStyle =>
            (KentridgeWindowStyle)_form.WindowTreatment;
        public bool HasAwning => _form.HasAwning;
        public bool ChimneyOnRight => _form.ChimneyOnRight;
        public bool AnnexOnRight => _form.AnnexOnRight;

        /// <summary>
        /// Immutable generic architecture value behind this transitional compatibility wrapper.
        /// Backends can consume it without accessing Architecture internals or depending on a
        /// Kentridge-specific geometry policy.
        /// </summary>
        public UrbanFabricForm Inner => _form;
    }

    public static class KentridgeUrbanFabricGrammar
    {
        public const int EnvelopeDm = KentridgeDefinition.AnonymousFabricEnvelopeDm;
        public const int RoofOverhangDm = 3;

        public static KentridgeUrbanFabricForm Resolve(
            KentridgeFrontageRun run,
            uint seed,
            int runIndex,
            int siteIndex)
        {
            UrbanFabricIntent intent = KentridgeDefinition.UrbanFabricIntent(run);
            UrbanFabricForm form = UrbanFabricCompiler.Resolve(intent, seed, runIndex, siteIndex);
            return new KentridgeUrbanFabricForm(form);
        }

        public static void Validate(
            KentridgeFrontageRun run,
            KentridgeUrbanFabricForm form)
        {
            UrbanFabricCompiler.Validate(KentridgeDefinition.UrbanFabricIntent(run), form.Inner);
        }
    }
}
