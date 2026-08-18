using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>Game-owned courtyard paving, well, and compatibility outbuilding authoring.</summary>
    public static class CastleCourtyardAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            Author(authoring, in plan, in components.Courtyard, in palette);
        }

        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCourtyardConfig courtyard,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!courtyard.IsWellFormed)
                throw new System.ArgumentException("Castle courtyard configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(plan.Seed ^ 0xC0DEu);

            if (courtyard.OpenSpace.SurfaceMode != OpenSpaceSurfaceMode.None)
            {
                StructureFootprintRect area = courtyard.OpenSpace.Area;
                int maxX = area.Min.x + area.Size.x;
                int maxZ = area.Min.y + area.Size.y;
                int topY = baseY + courtyard.OpenSpace.SurfaceThickness - 1;
                byte primary = palette.Resolve(courtyard.OpenSpace.SurfaceMaterialRole);

                for (int z = area.Min.y; z < maxZ; z++)
                for (int x = area.Min.x; x < maxX; x++)
                {
                    byte material = rng.NextInt(0, 100) < courtyard.PrimarySurfacePercent
                        ? primary
                        : GameMaterialIds.Dirt;
                    authoring.FillColumnBulk(
                        plan.Centre.x + x,
                        baseY,
                        topY,
                        plan.Centre.z + z,
                        material);
                }
            }

            if (courtyard.Well.Enabled)
            {
                int wellX = plan.Centre.x + courtyard.Well.LocalCentre.x;
                int wellZ = plan.Centre.z + courtyard.Well.LocalCentre.y;
                authoring.Cylinder(
                    wellX,
                    baseY + 1,
                    wellZ,
                    courtyard.Well.OuterRadius,
                    courtyard.Well.WallHeight,
                    palette.Resolve(StructureMaterialRole.Underground),
                    courtyard.Well.InnerRadius);
                authoring.Cylinder(
                    wellX,
                    baseY - courtyard.Well.ShaftDepth,
                    wellZ,
                    courtyard.Well.InnerRadius,
                    courtyard.Well.ShaftDepth,
                    palette.Resolve(StructureMaterialRole.Opening));
                authoring.Cylinder(
                    wellX,
                    baseY - courtyard.Well.ShaftDepth,
                    wellZ,
                    courtyard.Well.WaterRadius,
                    courtyard.Well.WaterDepth,
                    GameMaterialIds.Water);
            }

            if (!courtyard.AuthorCompatibilityBuildings)
                return;

            for (int i = 0; i < courtyard.SecondaryBuildingSlots.Length; i++)
            {
                CastleCourtyardBuildingSlotConfig slot = courtyard.SecondaryBuildingSlots[i];
                int bx = plan.Centre.x + slot.LocalOrigin.x;
                int bz = plan.Centre.z + slot.LocalOrigin.y;
                int width = rng.NextInt(70, 100);
                int depth = rng.NextInt(60, 84);
                int height = rng.NextInt(56, 76);

                authoring.HollowBox(
                    new int3(bx, baseY, bz),
                    new int3(width, height, depth),
                    5,
                    GameMaterialIds.Stone,
                    false,
                    false);
                authoring.Box(
                    new int3(bx + width / 2 - 9, baseY, bz),
                    new int3(18, 30, 5),
                    GameMaterialIds.Empty);
                authoring.Gable(
                    new int3(bx - 4, baseY + height, bz - 4),
                    new int3(width + 8, 30, depth + 8),
                    true,
                    GameMaterialIds.Tile);
            }
        }
    }
}
