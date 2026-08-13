using System;
using MountingForce.WorldGen.Architecture;

namespace MountingForce.WorldGen.Content.Kentridge
{
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
    /// Architectural detail for one anonymous piece of block frontage. The Kentridge content layer
    /// supplies the frontage run and its city-scale constraints; this lower layer generates local form.
    /// </summary>
    public readonly struct KentridgeUrbanFabricForm
    {
        public readonly int WidthDm;
        public readonly int DepthDm;
        public readonly int Storeys;
        public readonly int UpperOverhangDm;
        public readonly int RoofHeightDm;
        public readonly KentridgeRoofForm Roof;
        public readonly KentridgeFrontageRhythm FrontageRhythm;
        public readonly KentridgeWindowStyle WindowStyle;
        public readonly bool HasAwning;
        public readonly bool ChimneyOnRight;
        public readonly bool AnnexOnRight;

        public KentridgeUrbanFabricForm(
            int widthDm,
            int depthDm,
            int storeys,
            int upperOverhangDm,
            int roofHeightDm,
            KentridgeRoofForm roof,
            KentridgeFrontageRhythm frontageRhythm,
            KentridgeWindowStyle windowStyle,
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
            WindowStyle = windowStyle;
            HasAwning = hasAwning;
            ChimneyOnRight = chimneyOnRight;
            AnnexOnRight = annexOnRight;
        }
    }

    /// <summary>
    /// Lower-level grammar for anonymous urban fabric. The block plan supplies density, storey limits,
    /// frontage, court gaps, district and elevation; this layer supplies local architectural rhythm.
    /// </summary>
    public static class KentridgeUrbanFabricGrammar
    {
        public const int EnvelopeDm = 72;
        public const int RoofOverhangDm = 3;

        public static KentridgeUrbanFabricForm Resolve(
            KentridgeFrontageRun run,
            uint seed,
            int runIndex,
            int siteIndex)
        {
            uint h = Hash(seed, runIndex, siteIndex, (int)run.District, (int)run.Band);

            int storeys = run.MinStoreys;
            if (run.MaxStoreys > run.MinStoreys)
                storeys += (int)(h % (uint)(run.MaxStoreys - run.MinStoreys + 1));

            int width = 56 + (int)((h >> 3) % 7u);
            int depth = 50 + (int)((h >> 7) % 9u);
            int overhang = ((h >> 12) & 1u) != 0 ? 2 : 0;
            KentridgeRoofForm roof = (KentridgeRoofForm)((h >> 13) % 4u);
            KentridgeFrontageRhythm rhythm =
                (KentridgeFrontageRhythm)((h >> 16) % 3u);

            KentridgeWindowStyle windows = KentridgeWindowStyle.Glass;
            if (run.District == DistrictKind.Civic || run.District == DistrictKind.Noble
                || (run.District == DistrictKind.Market && ((h >> 19) & 1u) != 0))
                windows = KentridgeWindowStyle.Warm;

            int roofHeight = roof == KentridgeRoofForm.SteepGable
                ? 29 + (int)((h >> 20) % 4u)
                : 20 + (int)((h >> 20) % 7u);
            bool awning = run.District == DistrictKind.Market && ((h >> 24) & 1u) != 0;

            var form = new KentridgeUrbanFabricForm(
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
            Validate(run, form);
            return form;
        }

        public static void Validate(
            KentridgeFrontageRun run,
            KentridgeUrbanFabricForm form)
        {
            if (form.Storeys < run.MinStoreys || form.Storeys > run.MaxStoreys)
                throw new InvalidOperationException(
                    "Urban fabric storeys escaped frontage constraints: " + run.Id);
            if (form.WidthDm <= 0 || form.DepthDm <= 0 || form.RoofHeightDm <= 0)
                throw new InvalidOperationException(
                    "Urban fabric has invalid dimensions: " + run.Id);

            int lateral = form.WidthDm
                        + 2 * form.UpperOverhangDm
                        + 2 * RoofOverhangDm;
            if (lateral > EnvelopeDm)
                throw new InvalidOperationException(
                    "Urban fabric exceeds its orientation-independent envelope: " + run.Id);
            if (form.DepthDm + form.UpperOverhangDm + 2 * RoofOverhangDm > EnvelopeDm)
                throw new InvalidOperationException(
                    "Urban fabric depth exceeds its envelope: " + run.Id);
        }

        private static uint Hash(
            uint seed,
            int runIndex,
            int siteIndex,
            int district,
            int band)
        {
            uint h = seed
                   ^ ((uint)(runIndex + 1) * 0x9E3779B9u)
                   ^ ((uint)(siteIndex + 1) * 0x85EBCA6Bu)
                   ^ ((uint)(district + 7) * 0xC2B2AE35u)
                   ^ ((uint)(band + 13) * 0x27D4EB2Fu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
