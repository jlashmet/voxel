using System;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeBuildingMode : byte { Generated, Bespoke }
    public enum KentridgeFootprintForm : byte { Rectangle, RearWing, SideWing, SteppedUpper }
    public enum KentridgeRoofForm : byte { Gable, SteepGable, TwinGable, GableWithLeanTo }
    public enum KentridgeFrontageRhythm : byte { TwoBay, ThreeBay, Asymmetric }
    public enum KentridgeWindowStyle : byte { Glass, Warm, Open }

    /// <summary>
    /// Architectural detail compiled from Kentridge's high-level BuildingPlot intent. The settlement
    /// layer owns role, district, placement, frontage, and archetype; this lower layer owns the
    /// footprint variation, facade rhythm, roof, windows, wings, and other local architectural detail.
    /// </summary>
    public readonly struct KentridgeBuildingForm
    {
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

        public KentridgeBuildingForm(
            int roleId, StructureArchetype archetype, DistrictKind district,
            KentridgeBuildingMode mode, KentridgeFootprintForm footprint,
            KentridgeRoofForm roof, KentridgeFrontageRhythm frontageRhythm,
            KentridgeWindowStyle windowStyle, int widthDm, int depthDm, int storeys,
            int doorOffsetDm, int upperOverhangDm, int roofHeightDm,
            int wingWidthDm, int wingDepthDm, bool wingOnRight, bool chimneyOnRight)
        {
            RoleId = roleId;
            Archetype = archetype;
            District = district;
            Mode = mode;
            Footprint = footprint;
            Roof = roof;
            FrontageRhythm = frontageRhythm;
            WindowStyle = windowStyle;
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

        public bool IsGenerated => Mode == KentridgeBuildingMode.Generated;
        public bool IsShop => Archetype == StructureArchetype.Shop;
        public bool IsHospitality => Archetype == StructureArchetype.Inn;
    }

    /// <summary>
    /// Lower-level deterministic architecture compiler for Kentridge structure intent. This is kept
    /// in MountingForce.WorldGen.Architecture so the settlement/content assembly cannot depend on it.
    /// </summary>
    public static class KentridgeBuildingGrammar
    {
        public static KentridgeBuildingForm Resolve(BuildingPlot plot, uint seed)
        {
            KentridgeRole role = (KentridgeRole)plot.RoleId;
            switch (role)
            {
                case KentridgeRole.Inn:
                    return Generated(plot, KentridgeFootprintForm.RearWing,
                        KentridgeRoofForm.TwinGable, KentridgeFrontageRhythm.ThreeBay,
                        KentridgeWindowStyle.Warm, 132, 104, 3, 0, 4, 30,
                        40, 36, true, true);
                case KentridgeRole.Pub:
                    return Generated(plot, KentridgeFootprintForm.SideWing,
                        KentridgeRoofForm.GableWithLeanTo, KentridgeFrontageRhythm.Asymmetric,
                        KentridgeWindowStyle.Warm, 112, 92, 2, -12, 2, 24,
                        28, 42, false, false);
                case KentridgeRole.WeaponShop:
                    return Generated(plot, KentridgeFootprintForm.RearWing,
                        KentridgeRoofForm.GableWithLeanTo, KentridgeFrontageRhythm.ThreeBay,
                        KentridgeWindowStyle.Glass, 94, 70, 2, -10, 0, 20,
                        30, 26, false, true);
                case KentridgeRole.ArmorShop:
                    return Generated(plot, KentridgeFootprintForm.SideWing,
                        KentridgeRoofForm.TwinGable, KentridgeFrontageRhythm.TwoBay,
                        KentridgeWindowStyle.Glass, 84, 72, 2, 10, 2, 24,
                        22, 32, true, false);
                case KentridgeRole.MagicShop:
                    return Generated(plot, KentridgeFootprintForm.SteppedUpper,
                        KentridgeRoofForm.SteepGable, KentridgeFrontageRhythm.Asymmetric,
                        KentridgeWindowStyle.Warm, 72, 68, 3, -8, 5, 32,
                        0, 0, false, true);
                case KentridgeRole.MayorHouse:
                    return Generated(plot, KentridgeFootprintForm.RearWing,
                        KentridgeRoofForm.TwinGable, KentridgeFrontageRhythm.ThreeBay,
                        KentridgeWindowStyle.Warm, 90, 78, 3, 0, 4, 30,
                        28, 28, true, false);
                case KentridgeRole.AbandonedHouse:
                    return Generated(plot, KentridgeFootprintForm.SideWing,
                        KentridgeRoofForm.GableWithLeanTo, KentridgeFrontageRhythm.Asymmetric,
                        KentridgeWindowStyle.Open, 66, 66, 2, -10, 0, 20,
                        18, 28, false, false);
            }

            if (!UsesGeneratedHouseGrammar(plot.Archetype))
                return Bespoke(plot);

            uint h = Hash(seed, plot.RoleId, (int)plot.Archetype, (int)plot.District);
            bool wide = plot.Archetype == StructureArchetype.WideHouse;
            int width = wide ? 84 + (int)(h % 13u) : 66 + (int)(h % 11u);
            int depth = wide ? 74 + (int)((h >> 5) % 13u) : 64 + (int)((h >> 5) % 11u);
            KentridgeFootprintForm footprint = (KentridgeFootprintForm)((h >> 9) % 4u);
            KentridgeRoofForm roof = (KentridgeRoofForm)((h >> 12) % 4u);
            KentridgeFrontageRhythm rhythm = (KentridgeFrontageRhythm)((h >> 15) % 3u);

            int storeys = 2;
            if ((plot.District == DistrictKind.Civic || plot.District == DistrictKind.Market)
                && ((h >> 18) & 1u) != 0)
                storeys = 3;

            int overhang = storeys > 1 ? (int)((h >> 20) % 3u) * 2 : 0;
            if (footprint == KentridgeFootprintForm.SteppedUpper)
                overhang = Math.Max(overhang, 4);

            int wingWidth = 0;
            int wingDepth = 0;
            if (footprint == KentridgeFootprintForm.RearWing)
            {
                wingWidth = wide ? 26 : 22;
                wingDepth = wide ? 28 : 24;
            }
            else if (footprint == KentridgeFootprintForm.SideWing)
            {
                wingWidth = wide ? 22 : 18;
                wingDepth = wide ? 34 : 28;
            }

            int roofHeight = roof == KentridgeRoofForm.SteepGable
                ? 30
                : 21 + (int)((h >> 23) % 6u);
            int doorOffset = ((int)((h >> 26) % 3u) - 1) * 8;

            return Generated(plot, footprint, roof, rhythm, KentridgeWindowStyle.Glass,
                width, depth, storeys, doorOffset, overhang, roofHeight,
                wingWidth, wingDepth, ((h >> 28) & 1u) != 0, ((h >> 29) & 1u) != 0);
        }

        public static bool UsesGeneratedHouseGrammar(StructureArchetype archetype)
        {
            return archetype == StructureArchetype.Townhouse
                || archetype == StructureArchetype.WideHouse
                || archetype == StructureArchetype.Shop
                || archetype == StructureArchetype.Inn;
        }

        public static void ValidateGenerated(KentridgeBuildingForm form)
        {
            if (!form.IsGenerated) return;
            if (!UsesGeneratedHouseGrammar(form.Archetype))
                throw new InvalidOperationException(
                    "Generated Kentridge form uses unsupported archetype: " + form.Archetype);
            if (form.Storeys < 2 || form.Storeys > 3)
                throw new InvalidOperationException(
                    "Generated Kentridge form has invalid storey count for role " + form.RoleId);
            if (form.WidthDm <= 0 || form.DepthDm <= 0 || form.RoofHeightDm <= 0)
                throw new InvalidOperationException(
                    "Generated Kentridge form has invalid dimensions for role " + form.RoleId);

            Int3 envelope = KentridgeDefinition.FootprintDm(form.Archetype);
            ArchitectureTheme theme = KentridgeDefinition.Theme;
            int lateralExtent = form.WidthDm
                              + 2 * form.UpperOverhangDm
                              + 2 * theme.RoofOverhangDm;
            int depthExtent = form.DepthDm
                            + form.UpperOverhangDm
                            + 2 * theme.RoofOverhangDm;
            if (lateralExtent > envelope.X - 12 || depthExtent > envelope.Z - 12)
                throw new InvalidOperationException(
                    "Generated Kentridge form exceeds its stable plot envelope for role " + form.RoleId);
            if (form.Footprint == KentridgeFootprintForm.RearWing && form.WingDepthDm <= 0)
                throw new InvalidOperationException("Rear-wing form is missing its wing.");
            if (form.Footprint == KentridgeFootprintForm.SideWing && form.WingWidthDm <= 0)
                throw new InvalidOperationException("Side-wing form is missing its wing.");
        }

        private static KentridgeBuildingForm Generated(
            BuildingPlot plot, KentridgeFootprintForm footprint, KentridgeRoofForm roof,
            KentridgeFrontageRhythm rhythm, KentridgeWindowStyle windows,
            int width, int depth, int storeys, int doorOffset, int overhang,
            int roofHeight, int wingWidth, int wingDepth, bool wingRight, bool chimneyRight)
        {
            var form = new KentridgeBuildingForm(
                plot.RoleId, plot.Archetype, plot.District, KentridgeBuildingMode.Generated,
                footprint, roof, rhythm, windows, width, depth, storeys, doorOffset,
                overhang, roofHeight, wingWidth, wingDepth, wingRight, chimneyRight);
            ValidateGenerated(form);
            return form;
        }

        private static KentridgeBuildingForm Bespoke(BuildingPlot plot)
        {
            return new KentridgeBuildingForm(
                plot.RoleId, plot.Archetype, plot.District, KentridgeBuildingMode.Bespoke,
                KentridgeFootprintForm.Rectangle, KentridgeRoofForm.Gable,
                KentridgeFrontageRhythm.TwoBay, KentridgeWindowStyle.Glass,
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
