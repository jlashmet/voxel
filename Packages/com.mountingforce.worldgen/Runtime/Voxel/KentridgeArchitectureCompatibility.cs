using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    // Temporary emitter-facing vocabulary while KentridgeGrammarVoxelCatalogue is migrated from the
    // former Content-owned grammar names. Resolution and validation are delegated to the current
    // Architecture public handoff, so this file contains no architectural generation decisions.
    internal enum KentridgeBuildingMode : byte { Generated, Bespoke }
    internal enum KentridgeFootprintForm : byte { Rectangle, RearWing, SideWing, SteppedUpper }
    internal enum KentridgeRoofForm : byte { Gable, SteepGable, TwinGable, GableWithLeanTo }
    internal enum KentridgeFrontageRhythm : byte { TwoBay, ThreeBay, Asymmetric }
    internal enum KentridgeWindowStyle : byte { Glass, Warm, Open }

    internal readonly struct KentridgeBuildingForm
    {
        private readonly StructureIntent _intent;
        private readonly StructureForm _form;

        public readonly int RoleId;
        public readonly StructureArchetype Archetype;
        public readonly DistrictKind District;
        public readonly KentridgeBuildingMode Mode;
        public readonly KentridgeFootprintForm Footprint;
        public readonly KentridgeRoofForm Roof;
        public readonly KentridgeFrontageRhythm FrontageRhythm;
        public readonly KentridgeWindowStyle WindowStyle;
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

        internal KentridgeBuildingForm(StructureIntent intent, StructureForm form)
        {
            _intent = intent;
            _form = form;
            RoleId = form.RoleId;
            Archetype = form.Archetype;
            District = form.District;
            Mode = (KentridgeBuildingMode)form.Mode;
            Footprint = (KentridgeFootprintForm)form.Footprint;
            Roof = (KentridgeRoofForm)form.Roof;
            FrontageRhythm = (KentridgeFrontageRhythm)form.FrontageRhythm;
            WindowStyle = (KentridgeWindowStyle)form.WindowTreatment;
            WidthDm = form.WidthDm;
            DepthDm = form.DepthDm;
            Storeys = form.Storeys;
            DoorOffsetDm = form.DoorOffsetDm;
            UpperOverhangDm = form.UpperOverhangDm;
            RoofHeightDm = form.RoofHeightDm;
            WingWidthDm = form.WingWidthDm;
            WingDepthDm = form.WingDepthDm;
            WingOnRight = form.WingOnRight;
            ChimneyOnRight = form.ChimneyOnRight;
        }

        internal StructureIntent Intent => _intent;
        internal StructureForm Form => _form;

        public bool IsGenerated => Mode == KentridgeBuildingMode.Generated;
        public bool IsShop => Archetype == StructureArchetype.Shop;
    }

    internal static class KentridgeBuildingGrammar
    {
        public static KentridgeBuildingForm Resolve(BuildingPlot plot, uint seed)
        {
            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            StructureForm form = ArchitectureCompiler.Resolve(
                intent,
                KentridgeDefinition.Theme,
                seed);
            return new KentridgeBuildingForm(intent, form);
        }

        public static void ValidateGenerated(KentridgeBuildingForm form)
        {
            ArchitectureCompiler.ValidateGenerated(
                form.Intent,
                KentridgeDefinition.Theme,
                form.Form);
        }
    }
}
