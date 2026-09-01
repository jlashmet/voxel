using System;
using Game.WorldBuilder.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Bridges significant members of the authoritative natural-scatter population into the same
    /// generic sparse presentation bake used by other generated features. The bridge is population-
    /// generic: every scatter kind uses one conservative massing recipe and keeps the record's stable
    /// identity/revision. Ordinary members that fail policy never allocate a catalogue or sparse bake.
    /// </summary>
    public static class NaturalScatterPresentationPromotion
    {
        private const string DefinitionName = "natural-scatter-promoted";
        private const int DecimetresPerVoxel = 10;

        public static bool TryBake(
            in NaturalScatterRecord record,
            in NaturalScatterPromotionPolicy policy,
            byte material,
            IFeaturePresentationBaker baker,
            out FeaturePresentationBake bake)
        {
            if (baker == null) throw new ArgumentNullException(nameof(baker));
            bake = null;
            if (!policy.ShouldPromote(in record)) return false;

            int radius = Math.Max(1, CeilDiv(record.RadiusDm, DecimetresPerVoxel));
            int height = Math.Max(1, CeilDiv(record.HeightDm, DecimetresPerVoxel));
            int diameter = checked(radius * 2);
            int centreX = FloorDiv(record.PositionDm.X, DecimetresPerVoxel);
            int centreZ = FloorDiv(record.PositionDm.Y, DecimetresPerVoxel);
            int3 placementPosition = new int3(centreX - radius, 0, centreZ - radius);

            FeatureCatalogue catalogue = BuildCatalogue(diameter, height, material, placementPosition, Allocator.Temp);
            try
            {
                ExplicitPlacement placement = catalogue.ExplicitPlacements[0];
                if (!baker.TryBake(
                        in catalogue,
                        unchecked((uint)(record.StableId ^ (record.StableId >> 32))),
                        0,
                        in placement,
                        out FeaturePresentationBake derived))
                    return false;

                var primitives = new Primitive[derived.PrimitiveCount];
                for (int i = 0; i < primitives.Length; i++) primitives[i] = derived.GetPrimitive(i);

                ulong presentationRevision = record.Revision ^ ((ulong)material << 56);
                if (presentationRevision == 0UL) presentationRevision = 1UL;
                bake = new FeaturePresentationBake(
                    record.StableId,
                    presentationRevision,
                    FeatureKind.Scatter,
                    derived.Position,
                    derived.Orientation,
                    derived.BoundsMin,
                    derived.BoundsMax,
                    primitives);
                return true;
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static FeatureCatalogue BuildCatalogue(
            int diameter,
            int height,
            byte material,
            int3 placementPosition,
            Allocator allocator)
        {
            const int programLength = 14;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            int p = 0;
            catalogue.Program[p++] = (int)ShapeOp.EmitBox;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = diameter;
            catalogue.Program[p++] = height;
            catalogue.Program[p++] = diameter;
            catalogue.Program[p++] = material;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = 0;
            catalogue.Program[p++] = (int)PrimitiveMode.Fill;
            catalogue.Program[p++] = (int)ShapeOp.End;
            catalogue.Program[p] = 0;

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = DefinitionName,
                Kind = FeatureKind.Scatter,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(diameter, height, diameter),
                MaxSlope = 0,
                Precedence = 0,
                ProgramOffset = 0,
                ProgramLength = programLength,
                MaxPrimitives = 1,
            };
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = placementPosition,
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
                MaxSlope = 0,
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
            throw new InvalidOperationException("Promoted natural scatter catalogue failed validation: " + result);
        }

        private static int CeilDiv(int value, int divisor) => checked((value + divisor - 1) / divisor);

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && value < 0) quotient--;
            return quotient;
        }
    }
}
