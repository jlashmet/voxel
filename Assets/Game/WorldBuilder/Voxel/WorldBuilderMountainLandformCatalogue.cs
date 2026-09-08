using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Caller-owned mapping from reusable mountain surface roles to concrete voxel materials.
    /// Keeping this mapping outside Game.WorldBuilder.Api prevents presentation ids from leaking
    /// into the semantic landform/climate contract.
    /// </summary>
    public readonly struct MountainLandformPalette
    {
        public byte GroundCoverMaterial { get; }
        public byte RockMaterial { get; }
        public byte SnowMaterial { get; }

        public MountainLandformPalette(
            byte groundCoverMaterial,
            byte rockMaterial,
            byte snowMaterial)
        {
            if (groundCoverMaterial == 0) throw new ArgumentOutOfRangeException(nameof(groundCoverMaterial));
            if (rockMaterial == 0) throw new ArgumentOutOfRangeException(nameof(rockMaterial));
            if (snowMaterial == 0) throw new ArgumentOutOfRangeException(nameof(snowMaterial));

            GroundCoverMaterial = groundCoverMaterial;
            RockMaterial = rockMaterial;
            SnowMaterial = snowMaterial;
        }

        public byte MaterialFor(MountainSurfaceRole role)
        {
            switch (role)
            {
                case MountainSurfaceRole.GroundCover: return GroundCoverMaterial;
                case MountainSurfaceRole.Rock: return RockMaterial;
                case MountainSurfaceRole.Snow: return SnowMaterial;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
    }

    /// <summary>
    /// Voxel realization for <see cref="MountainLandformSurface"/>. It emits the surface authority's
    /// exact analytic masses rather than rebuilding or approximating the mountain independently.
    /// Climate is an ordered material-only surface pass over those masses; it never changes the
    /// authoritative occupancy. Concrete material identity remains caller-owned presentation data.
    /// </summary>
    public static class WorldBuilderMountainLandformCatalogue
    {
        public const string LandformDefinitionName = "worldbuilder-mountain-landform";

        public static FeatureCatalogue Build(
            in MountainLandformSpec spec,
            byte mountainMaterial,
            Allocator allocator)
        {
            var surface = new MountainLandformSurface(in spec);
            return Build(surface, mountainMaterial, allocator);
        }

        public static FeatureCatalogue Build(
            MountainLandformSurface surface,
            byte mountainMaterial,
            Allocator allocator)
        {
            if (mountainMaterial == 0) throw new ArgumentOutOfRangeException(nameof(mountainMaterial));

            ValidateAndCalculateBounds(surface, surface?.MassCount ?? 0, out int3 min, out int3 max);
            int[] program = BuildProgram(surface, min, mountainMaterial);
            return BuildCatalogue(surface, min, max, program, surface.MassCount, allocator);
        }

        public static FeatureCatalogue Build(
            in MountainLandformSpec spec,
            MountainClimateProfile climate,
            in MountainLandformPalette palette,
            Allocator allocator)
        {
            var surface = new MountainLandformSurface(in spec);
            return Build(surface, climate, in palette, allocator);
        }

        public static FeatureCatalogue Build(
            MountainLandformSurface surface,
            MountainClimateProfile climate,
            in MountainLandformPalette palette,
            Allocator allocator)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (climate == null) throw new ArgumentNullException(nameof(climate));

            int steepMassCount = CountSteepMasses(surface, climate.SteepRockSlopePermille);
            int primitiveCount = surface.MassCount + 2 + steepMassCount;
            ValidateAndCalculateBounds(surface, primitiveCount, out int3 min, out int3 max);

            int[] program = BuildClimateProgram(surface, min, max, climate, in palette);
            return BuildCatalogue(surface, min, max, program, primitiveCount, allocator);
        }

        private static FeatureCatalogue BuildCatalogue(
            MountainLandformSurface surface,
            int3 min,
            int3 max,
            int[] program,
            int maxPrimitives,
            Allocator allocator)
        {
            int3 footprint = max - min + 1;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: program.Length,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            for (int i = 0; i < program.Length; i++) catalogue.Program[i] = program[i];

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = LandformDefinitionName,
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = min.y,
                Footprint = footprint,
                MaxSlope = 8,
                Precedence = 100,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaxPrimitives = maxPrimitives,
            };

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = min,
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 4096,
                MaxSlope = 8,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result == CatalogueLoadResult.Ok) return catalogue;

            catalogue.Dispose();
            throw new InvalidOperationException(
                "Mountain landform catalogue failed validation: " + result);
        }

        private static int[] BuildProgram(
            MountainLandformSurface surface,
            int3 placementMin,
            byte material)
        {
            var program = new List<int>(surface.MassCount * ShapeOps.InstructionLength(ShapeOp.EmitFrustum) + 2);
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                EmitFrustum(
                    program,
                    mass.CentreXdm - placementMin.x,
                    mass.BaseYdm - placementMin.y,
                    mass.CentreZdm - placementMin.z,
                    mass.HeightDm,
                    mass.BaseRadiusDm,
                    mass.TopRadiusDm,
                    material,
                    PrimitiveMode.FillIfEmpty);
            }

            program.Add((int)ShapeOp.End);
            program.Add(0);
            return program.ToArray();
        }

        private static int[] BuildClimateProgram(
            MountainLandformSurface surface,
            int3 placementMin,
            int3 placementMax,
            MountainClimateProfile climate,
            in MountainLandformPalette palette)
        {
            int steepMassCount = CountSteepMasses(surface, climate.SteepRockSlopePermille);
            int capacity = (surface.MassCount + steepMassCount) * ShapeOps.InstructionLength(ShapeOp.EmitFrustum)
                + 2 * ShapeOps.InstructionLength(ShapeOp.EmitBox) + 2;
            var program = new List<int>(capacity);

            // Shape authority remains exactly the same mass list; climate only changes which
            // concrete material is used for its mineral support and adds material-only passes.
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                EmitFrustum(
                    program,
                    mass.CentreXdm - placementMin.x,
                    mass.BaseYdm - placementMin.y,
                    mass.CentreZdm - placementMin.z,
                    mass.HeightDm,
                    mass.BaseRadiusDm,
                    mass.TopRadiusDm,
                    palette.RockMaterial,
                    PrimitiveMode.FillIfEmpty);
            }

            int relief = placementMax.y - placementMin.y;
            int sizeX = placementMax.x - placementMin.x + 1;
            int sizeZ = placementMax.z - placementMin.z + 1;
            int groundCoverTop = ScalePermille(relief, climate.GroundCoverCeilingPermille);
            int snowLine = ScalePermille(relief, climate.SnowLinePermille);

            // PaintSurface treats each box as an altitude selector over the already-generated
            // density surface. Lower selectors may touch hidden interior support beneath taller
            // columns, but never occupancy; later snow/steep passes determine the visible role.
            EmitBox(
                program,
                0, 0, 0,
                sizeX, groundCoverTop + 1, sizeZ,
                palette.GroundCoverMaterial,
                PrimitiveMode.PaintSurface);
            EmitBox(
                program,
                0, snowLine, 0,
                sizeX, relief - snowLine + 1, sizeZ,
                palette.SnowMaterial,
                PrimitiveMode.PaintSurface);

            // Slope is a property of each analytic mass. Repainting steep masses last implements
            // the semantic precedence rule (steep => exposed rock) without altering geometry.
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                if (MassSlopePermille(in mass) < climate.SteepRockSlopePermille) continue;

                EmitFrustum(
                    program,
                    mass.CentreXdm - placementMin.x,
                    mass.BaseYdm - placementMin.y,
                    mass.CentreZdm - placementMin.z,
                    mass.HeightDm,
                    mass.BaseRadiusDm,
                    mass.TopRadiusDm,
                    palette.RockMaterial,
                    PrimitiveMode.PaintSurface);
            }

            program.Add((int)ShapeOp.End);
            program.Add(0);
            return program.ToArray();
        }

        private static void ValidateAndCalculateBounds(
            MountainLandformSurface surface,
            int primitiveCount,
            out int3 min,
            out int3 max)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (surface.MassCount < 1 || primitiveCount > FeatureBudget.MaxPrimitivesPerInstance)
                throw new InvalidOperationException(
                    $"Mountain landform emits {primitiveCount} primitives; budget is {FeatureBudget.MaxPrimitivesPerInstance}.");

            CalculateBounds(surface, out min, out max);
            int3 footprint = max - min + 1;
            if (footprint.x > FeatureBudget.MaxFootprintVoxels
                || footprint.y > FeatureBudget.MaxFootprintVoxels
                || footprint.z > FeatureBudget.MaxFootprintVoxels)
            {
                throw new InvalidOperationException(
                    $"Mountain landform footprint {footprint} exceeds {FeatureBudget.MaxFootprintVoxels} voxels on one or more axes.");
            }
        }

        private static int CountSteepMasses(MountainLandformSurface surface, int steepRockSlopePermille)
        {
            int count = 0;
            for (int i = 0; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                if (MassSlopePermille(in mass) >= steepRockSlopePermille) count++;
            }
            return count;
        }

        private static int MassSlopePermille(in MountainLandformMass mass)
        {
            int horizontalRun = mass.BaseRadiusDm - mass.TopRadiusDm;
            if (horizontalRun <= 0) return 10000;
            long slope = ((long)mass.HeightDm * 1000L + horizontalRun / 2L) / horizontalRun;
            return (int)Math.Min(10000L, slope);
        }

        private static int ScalePermille(int span, int permille)
            => (int)(((long)span * permille + 500L) / 1000L);

        private static void CalculateBounds(
            MountainLandformSurface surface,
            out int3 min,
            out int3 max)
        {
            MountainLandformMass first = surface.GetMass(0);
            min = new int3(
                first.CentreXdm - first.BaseRadiusDm,
                first.BaseYdm,
                first.CentreZdm - first.BaseRadiusDm);
            max = new int3(
                first.CentreXdm + first.BaseRadiusDm,
                first.TopYdm,
                first.CentreZdm + first.BaseRadiusDm);

            for (int i = 1; i < surface.MassCount; i++)
            {
                MountainLandformMass mass = surface.GetMass(i);
                min = math.min(min, new int3(
                    mass.CentreXdm - mass.BaseRadiusDm,
                    mass.BaseYdm,
                    mass.CentreZdm - mass.BaseRadiusDm));
                max = math.max(max, new int3(
                    mass.CentreXdm + mass.BaseRadiusDm,
                    mass.TopYdm,
                    mass.CentreZdm + mass.BaseRadiusDm));
            }
        }

        private static void EmitBox(
            List<int> program,
            int minX,
            int minY,
            int minZ,
            int sizeX,
            int sizeY,
            int sizeZ,
            byte material,
            PrimitiveMode mode)
        {
            program.Add((int)ShapeOp.EmitBox);
            program.Add(0);
            program.Add(minX);
            program.Add(minY);
            program.Add(minZ);
            program.Add(sizeX);
            program.Add(sizeY);
            program.Add(sizeZ);
            program.Add(material);
            program.Add(0);
            program.Add(0);
            program.Add((int)mode);
        }

        private static void EmitFrustum(
            List<int> program,
            int centreX,
            int baseY,
            int centreZ,
            int height,
            int baseRadius,
            int topRadius,
            byte material,
            PrimitiveMode mode)
        {
            program.Add((int)ShapeOp.EmitFrustum);
            program.Add(0);
            program.Add(centreX);
            program.Add(baseY);
            program.Add(centreZ);
            program.Add(height);
            program.Add(baseRadius);
            program.Add(topRadius);
            program.Add(1);
            program.Add(material);
            program.Add(0);
            program.Add(0);
            program.Add((int)mode);
        }
    }
}
