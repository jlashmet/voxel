using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Reusable session-side realization for the shared architectural component configs.
    /// These helpers contain only bounded integer geometry policy and operate through
    /// <see cref="IStructureAuthoringSession"/> so archetypes do not need a second runtime path.
    /// </summary>
    public static class StructureComponentAuthoring
    {
        public static void AuthorWallRun(
            IStructureAuthoringSession authoring,
            int3 start,
            int3 direction,
            bool alongX,
            in StructureWallRunConfig config,
            in StructureMaterialPalette palette)
        {
            Require(authoring);
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Wall run config is not well formed.", nameof(config));
            ValidateHorizontalDirection(direction, alongX);

            int3 usableStart = start + direction * config.StartInset;
            int usableLength = config.UsableLength;
            int3 size = alongX
                ? new int3(usableLength, config.Height, config.Thickness)
                : new int3(config.Thickness, config.Height, usableLength);

            authoring.FillBulk(usableStart, size, palette.Resolve(config.PrimaryMaterial));

            for (int i = 0; i < config.MaterialBands.Length; i++)
            {
                StructureWallMaterialBand band = config.MaterialBands[i];
                int3 bandSize = alongX
                    ? new int3(usableLength, band.Height, config.Thickness)
                    : new int3(config.Thickness, band.Height, usableLength);
                authoring.FillBulk(
                    usableStart + new int3(0, band.StartY, 0),
                    bandSize,
                    palette.Resolve(band.Material));
            }
        }

        public static void AuthorRepeatedOpenings(
            IStructureAuthoringSession authoring,
            int3 wallStart,
            int3 direction,
            bool alongX,
            int wallSpan,
            int depth,
            in OpeningConfig config,
            in StructureMaterialPalette palette)
        {
            Require(authoring);
            if (StructureComponentValidation.Opening(in config, wallSpan) !=
                StructureComponentValidationIssue.None)
                throw new System.ArgumentException("Opening config is invalid for the wall span.", nameof(config));
            ValidateHorizontalDirection(direction, alongX);
            if (depth <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(depth));

            int count = config.MaxCountForSpan(wallSpan);
            int step = config.Spacing == 0 ? 0 : config.Spacing;
            byte material = palette.Resolve(config.FillMaterialRole);

            for (int i = 0; i < count; i++)
            {
                int offset = config.StartMargin + i * step;
                int3 min = wallStart + direction * offset + new int3(0, config.BottomOffset, 0);
                int3 size = alongX
                    ? new int3(config.Width, config.Height, depth)
                    : new int3(depth, config.Height, config.Width);

                if (config.Kind == StructureOpeningKind.Arch)
                {
                    authoring.Arch(
                        min,
                        config.Width,
                        config.Height,
                        depth,
                        alongX ? 2 : 0,
                        material);
                }
                else
                {
                    authoring.FillBulk(min, size, material);
                }
            }
        }

        public static void AuthorBattlements(
            IStructureAuthoringSession authoring,
            int3 start,
            int3 direction,
            bool alongX,
            int length,
            in BattlementConfig config,
            in StructureMaterialPalette palette)
        {
            Require(authoring);
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Battlement config is not well formed.", nameof(config));
            ValidateHorizontalDirection(direction, alongX);
            if (length <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(length));

            byte material = palette.Resolve(config.MaterialRole);
            if (config.ParapetHeight > 0)
            {
                int3 parapetSize = alongX
                    ? new int3(length, config.ParapetHeight, config.ParapetThickness)
                    : new int3(config.ParapetThickness, config.ParapetHeight, length);
                authoring.FillBulk(start, parapetSize, material);
                start.y += config.ParapetHeight;
            }

            int cadence = config.MerlonWidth + config.GapWidth;
            for (int offset = 0; offset < length; offset += cadence)
            {
                int blockLength = math.min(config.MerlonWidth, length - offset);
                int3 blockSize = alongX
                    ? new int3(blockLength, config.MerlonHeight, config.ParapetThickness)
                    : new int3(config.ParapetThickness, config.MerlonHeight, blockLength);
                authoring.FillBulk(start + direction * offset, blockSize, material);
            }
        }

        public static void AuthorTowerShell(
            IStructureAuthoringSession authoring,
            int3 centre,
            in TowerConfig config,
            in StructureMaterialPalette palette,
            int innerRadius = 0,
            int wallThickness = 0)
        {
            Require(authoring);
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Tower config is not well formed.", nameof(config));

            byte material = palette.Resolve(config.WallMaterialRole);
            if (config.Shape == StructureTowerShape.Round)
            {
                if (innerRadius < 0 || innerRadius >= config.Radius)
                    throw new System.ArgumentOutOfRangeException(nameof(innerRadius));
                authoring.Cylinder(
                    centre.x,
                    centre.y,
                    centre.z,
                    config.Radius,
                    config.Height,
                    material,
                    innerRadius);
                return;
            }

            if (wallThickness > 0)
            {
                if (wallThickness * 2 >= config.Width || wallThickness * 2 >= config.Depth)
                    throw new System.ArgumentOutOfRangeException(nameof(wallThickness));
                authoring.HollowBox(
                    new int3(centre.x - config.Width / 2, centre.y, centre.z - config.Depth / 2),
                    new int3(config.Width, config.Height, config.Depth),
                    wallThickness,
                    material,
                    false,
                    false);
            }
            else
            {
                authoring.FillBulk(
                    new int3(centre.x - config.Width / 2, centre.y, centre.z - config.Depth / 2),
                    new int3(config.Width, config.Height, config.Depth),
                    material);
            }
        }

        /// <summary>
        /// Authors the fixed slab form of a shared foundation. TerrainFill and Terraced require
        /// terrain-aware composition and intentionally remain with the owning bounded terrain pass.
        /// </summary>
        public static void AuthorSlabFoundation(
            IStructureAuthoringSession authoring,
            int3 localOriginAtTop,
            in StructureFootprintConfig config,
            in StructureMaterialPalette palette)
        {
            Require(authoring);
            if (!config.IsWellFormed || config.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException("A well-formed slab foundation config is required.", nameof(config));

            byte material = palette.Resolve(config.FoundationMaterial);
            for (int i = 0; i < config.PartCount; i++)
            {
                StructureFootprintRect part = config.PartAt(i);
                authoring.FillBulk(
                    new int3(
                        localOriginAtTop.x + part.Min.x,
                        localOriginAtTop.y - config.FoundationDepth,
                        localOriginAtTop.z + part.Min.y),
                    new int3(part.Size.x, config.FoundationDepth, part.Size.y),
                    material);
            }
        }

        private static void ValidateHorizontalDirection(int3 direction, bool alongX)
        {
            bool valid = alongX
                ? direction.y == 0 && direction.z == 0 && math.abs(direction.x) == 1
                : direction.y == 0 && direction.x == 0 && math.abs(direction.z) == 1;
            if (!valid)
                throw new System.ArgumentException("Direction must be a cardinal unit vector matching the run axis.", nameof(direction));
        }

        private static void Require(IStructureAuthoringSession authoring)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
        }
    }
}
