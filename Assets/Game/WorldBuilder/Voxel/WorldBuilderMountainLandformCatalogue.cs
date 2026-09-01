using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Voxel realization for <see cref="MountainLandformSurface"/>. It emits the surface authority's
    /// exact analytic masses rather than rebuilding or approximating the mountain independently.
    /// Material identity remains caller-owned presentation data.
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
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (mountainMaterial == 0) throw new ArgumentOutOfRangeException(nameof(mountainMaterial));
            if (surface.MassCount < 1 || surface.MassCount > FeatureBudget.MaxPrimitivesPerInstance)
                throw new InvalidOperationException(
                    $"Mountain landform emits {surface.MassCount} primitives; budget is {FeatureBudget.MaxPrimitivesPerInstance}.");

            CalculateBounds(surface, out int3 min, out int3 max);
            int3 footprint = max - min + 1;
            if (footprint.x > FeatureBudget.MaxFootprintVoxels
                || footprint.y > FeatureBudget.MaxFootprintVoxels
                || footprint.z > FeatureBudget.MaxFootprintVoxels)
            {
                throw new InvalidOperationException(
                    $"Mountain landform footprint {footprint} exceeds {FeatureBudget.MaxFootprintVoxels} voxels on one or more axes.");
            }

            int[] program = BuildProgram(surface, min, mountainMaterial);
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
                MaxPrimitives = surface.MassCount,
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
                    material);
            }

            program.Add((int)ShapeOp.End);
            program.Add(0);
            return program.ToArray();
        }

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

        private static void EmitFrustum(
            List<int> program,
            int centreX,
            int baseY,
            int centreZ,
            int height,
            int baseRadius,
            int topRadius,
            byte material)
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
            program.Add((int)PrimitiveMode.FillIfEmpty);
        }
    }
}
