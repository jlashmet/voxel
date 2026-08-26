using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    // Compatibility names for the voxel catalogue while the architecture layer completes its
    // renderer-independent rename. Keep this adapter at the backend boundary instead of leaking the
    // old Kentridge-prefixed types back into settlement or architecture code.
    internal enum KentridgeFootprintForm : byte
    {
        Rectangle = (byte)FootprintForm.Rectangle,
        RearWing = (byte)FootprintForm.RearWing,
        SideWing = (byte)FootprintForm.SideWing,
        SteppedUpper = (byte)FootprintForm.SteppedUpper,
    }

    internal enum KentridgeRoofForm : byte
    {
        Gable = (byte)RoofForm.Gable,
        SteepGable = (byte)RoofForm.SteepGable,
        TwinGable = (byte)RoofForm.TwinGable,
        GableWithLeanTo = (byte)RoofForm.GableWithLeanTo,
    }

    internal enum KentridgeFrontageRhythm : byte
    {
        TwoBay = (byte)FrontageRhythm.TwoBay,
        ThreeBay = (byte)FrontageRhythm.ThreeBay,
        Asymmetric = (byte)FrontageRhythm.Asymmetric,
    }

    internal enum KentridgeWindowStyle : byte
    {
        Glass = (byte)WindowTreatment.Glass,
        Warm = (byte)WindowTreatment.Warm,
        Open = (byte)WindowTreatment.Open,
    }

    internal readonly struct KentridgeBuildingForm
    {
        internal readonly StructureIntent Intent;
        internal readonly ArchitectureTheme Theme;
        internal readonly StructureForm Inner;

        internal KentridgeBuildingForm(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm inner)
        {
            Intent = intent;
            Theme = theme;
            Inner = inner;
        }

        public int RoleId => Inner.RoleId;
        public StructureArchetype Archetype => Inner.Archetype;
        public DistrictKind District => Inner.District;
        public bool IsGenerated => Inner.IsGenerated;
        public bool IsShop => Inner.IsShop;
        public KentridgeFootprintForm Footprint => (KentridgeFootprintForm)Inner.Footprint;
        public KentridgeRoofForm Roof => (KentridgeRoofForm)Inner.Roof;
        public KentridgeFrontageRhythm FrontageRhythm =>
            (KentridgeFrontageRhythm)Inner.FrontageRhythm;
        public KentridgeWindowStyle WindowStyle => (KentridgeWindowStyle)Inner.WindowTreatment;
        public int WidthDm => Inner.WidthDm;
        public int DepthDm => Inner.DepthDm;
        public int Storeys => Inner.Storeys;
        public int DoorOffsetDm => Inner.DoorOffsetDm;
        public int UpperOverhangDm => Inner.UpperOverhangDm;
        public int RoofHeightDm => Inner.RoofHeightDm;
        public int WingWidthDm => Inner.WingWidthDm;
        public int WingDepthDm => Inner.WingDepthDm;
        public bool WingOnRight => Inner.WingOnRight;
        public bool ChimneyOnRight => Inner.ChimneyOnRight;
    }

    internal static class KentridgeBuildingGrammar
    {
        public static KentridgeBuildingForm Resolve(BuildingPlot plot, uint seed)
        {
            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            ArchitectureTheme theme = KentridgeDefinition.Theme;
            StructureForm form = ArchitectureCompiler.Resolve(intent, theme, seed);
            return new KentridgeBuildingForm(intent, theme, form);
        }

        public static void ValidateGenerated(KentridgeBuildingForm form)
        {
            ArchitectureCompiler.ValidateGenerated(form.Intent, form.Theme, form.Inner);
        }
    }
}
