using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace MountingForce.WorldGen.Voxel
{
    internal enum KentridgeHousePresetId : byte
    {
        Compact = 0,
        Farmhouse = 1,
        TallTownhouse = 2,
    }

    /// <summary>
    /// Adapter between Kentridge's renderer-neutral StructureForm and the shared house authoring
    /// contracts. Selection is stable per semantic role; geometry is compiled by HouseProgramCompiler.
    /// Kentridge retains settlement identity and envelope policy but no longer owns the rectangular
    /// house shell/opening/roof implementation.
    /// </summary>
    internal static class KentridgeSharedHouseProgram
    {
        internal readonly struct Program
        {
            public readonly int[] Code;
            public readonly int3 Door;
            public readonly int3 Hearth;
            public readonly KentridgeHousePresetId Preset;

            public Program(
                int[] code,
                int3 door,
                int3 hearth,
                KentridgeHousePresetId preset)
            {
                Code = code;
                Door = door;
                Hearth = hearth;
                Preset = preset;
            }
        }

        public static Program Build(
            BuildingPlot plot,
            StructureForm form,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            uint seed)
        {
            if (!form.IsGenerated)
                throw new ArgumentException("Shared house adapter requires a generated form.", nameof(form));

            int scale = settings.VoxelsPerDecimetre;
            KentridgeHousePresetId preset = SelectPreset(plot, seed);
            StructureMaterialPalette palette = ResolvePalette(theme, settings, form.RoleId);
            HouseConfig config = BaseConfig(preset, in palette);

            int width = form.WidthDm * scale;
            int depth = form.DepthDm * scale;
            int foundation = theme.FoundationHeightDm * scale;
            int floorHeight = theme.FloorHeightDm * scale;
            int wallHeight = form.Storeys * floorHeight;

            config.Footprint.Primary = new StructureFootprintRect(
                int2.zero, new int2(width, depth));
            config.Footprint.FoundationDepth = foundation;
            config.Walls.Length = width;
            config.Walls.Height = wallHeight;
            config.Walls.Thickness = math.max(1, theme.WallThicknessDm * scale);
            config.Floors.FloorCount = form.Storeys;
            config.Floors.LevelHeight = floorHeight;
            config.Floors.SlabThickness = math.max(1, 3 * scale);

            int doorWidth = (form.Archetype == StructureArchetype.Shop ? 17 : 13) * scale;
            config.MainDoor.Width = math.min(doorWidth, width - 4 * config.Walls.Thickness);
            config.MainDoor.Height = theme.DoorHeightDm * scale;
            config.MainDoor.BottomOffset = 0;
            config.FrontDoors.Facade = HouseFacade.Front;
            config.FrontDoors.Placement = HouseFacadePlacementMode.Centered;
            config.FrontDoors.Count = 1;
            config.FrontDoors.Opening = config.MainDoor;

            // Keep the shared compiler's supported bounded roof family. Steep/twin Kentridge forms
            // retain their requested height through the pitch ratio; annex/secondary roof hooks can
            // be layered later without restoring a private main-house geometry path.
            config.Roof.Style = RoofStyle.Gable;
            config.Roof.RidgeAxis = RoofAxis.Z;
            config.Roof.PitchRise = math.max(1, form.RoofHeightDm * scale);
            config.Roof.PitchRun = math.max(1, width / 2);
            config.Roof.EaveOverhang = theme.RoofOverhangDm * scale;
            config.Roof.Thickness = math.max(1, scale);
            config.Palette = palette;

            // The shared compiler deliberately emits two semantic anchors: public entrance and
            // hearth. Kentridge preserves both rather than mutating compiled bytecode to hide one.
            int[] compiled = HouseProgramCompiler.BuildProgram(in config, 0, 1);
            // settings carries the settlement being realized; the envelope belongs to it.
            Int3 envelopeDm = SettlementFootprints.For(settings.Settlement, form.Archetype);
            int envelopeWidth = envelopeDm.X * scale;
            int x0 = (envelopeWidth - width) / 2;
            int z0 = 10 * scale;
            int3 localOffset = new int3(x0, 0, z0);
            int[] translated = ShapeProgramComposition.Translate(compiled, localOffset);

            int3 door = new int3(x0 + width / 2, foundation, z0);
            int3 hearth = new int3(x0 + width / 2, foundation, z0 + depth / 2);
            return new Program(translated, door, hearth, preset);
        }

        public static KentridgeHousePresetId SelectPreset(BuildingPlot plot, uint seed)
        {
            string presetId = KentridgeTownPlanner.CompositionPolicy.Palette.SelectPreset(
                seed,
                plot.RoleId,
                plot.Archetype,
                plot.District);

            switch (presetId)
            {
                case KentridgeTownPlanner.CompactHousePresetId:
                    return KentridgeHousePresetId.Compact;
                case KentridgeTownPlanner.FarmhousePresetId:
                    return KentridgeHousePresetId.Farmhouse;
                case KentridgeTownPlanner.TallTownhousePresetId:
                    return KentridgeHousePresetId.TallTownhouse;
                default:
                    throw new InvalidOperationException(
                        "Kentridge settlement palette selected an unsupported house preset: " + presetId);
            }
        }

        private static HouseConfig BaseConfig(
            KentridgeHousePresetId preset,
            in StructureMaterialPalette palette)
        {
            HouseConfig config;
            switch (preset)
            {
                case KentridgeHousePresetId.Compact:
                    config = HouseStylePresets.CompactCabin(palette.PrimaryWall, palette.Roof);
                    break;
                case KentridgeHousePresetId.Farmhouse:
                    config = HouseStylePresets.Farmhouse(palette.PrimaryWall, palette.Roof);
                    break;
                default:
                    config = HousePresets.TallTownhouse(palette.PrimaryWall, palette.Roof);
                    break;
            }
            config.Palette = palette;
            return config;
        }

        private static StructureMaterialPalette ResolvePalette(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            int roleId)
        {
            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte frame = settings.Materials.Resolve(theme.Frame);
            byte window = settings.Materials.Resolve(theme.Window);
            byte roof = roleId == (int)KentridgeRole.MagicShop
                     || roleId == (int)KentridgeRole.MayorHouse
                ? settings.Materials.Resolve(MaterialRole.Slate)
                : settings.Materials.Resolve(theme.Roof);

            return new StructureMaterialPalette
            {
                Foundation = foundation,
                PrimaryWall = wall,
                SecondaryWall = wall,
                Trim = frame,
                Roof = roof,
                Floor = frame,
                Column = frame,
                Accent = settings.Materials.Resolve(theme.AccentStone),
                Underground = foundation,
                Opening = 0,
                Glass = window,
                Detail = frame,
            };
        }
    }
}
