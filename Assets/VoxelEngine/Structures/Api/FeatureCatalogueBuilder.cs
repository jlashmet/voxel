using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Why a catalogue was refused. Loading never half-succeeds.</summary>
    public enum CatalogueLoadResult
    {
        Ok = 0,
        UnsupportedVersion = 1,
        TooManyDefinitions = 2,
        FootprintExceedsBudget = 3,
        SpacingNotEnforceable = 4,
        EmptyCatalogue = 5,
        PrimitiveBudgetExceeded = 6,
        InvalidStructuralComposition = 7,
    }

    /// <summary>
    /// Builds a catalogue blob and computes its hash.
    ///
    /// The checks here are the ones that must hold before *anything* generates, because a
    /// catalogue that violates them produces a world that is wrong rather than a world that fails.
    /// </summary>
    public static class FeatureCatalogueBuilder
    {
        public const uint SupportedVersion = 1;

        public static CatalogueLoadResult Finalise(ref FeatureCatalogue catalogue)
        {
            if (catalogue.Version != SupportedVersion)
                return CatalogueLoadResult.UnsupportedVersion;

            if (!catalogue.IsCreated || catalogue.DefinitionCount == 0)
                return CatalogueLoadResult.EmptyCatalogue;

            if (catalogue.DefinitionCount > FeatureBudget.MaxDefinitions)
                return CatalogueLoadResult.TooManyDefinitions;

            for (var i = 0; i < catalogue.DefinitionCount; i++)
            {
                var definition = catalogue.Definitions[i];
                if (!definition.FootprintWithinBudget)
                    return CatalogueLoadResult.FootprintExceedsBudget;
                if (definition.MaxPrimitives > FeatureBudget.MaxPrimitivesPerInstance)
                    return CatalogueLoadResult.PrimitiveBudgetExceeded;
            }

            for (var i = 0; i < catalogue.Rules.Length; i++)
            {
                if (!catalogue.Rules[i].SpacingEnforceable)
                    return CatalogueLoadResult.SpacingNotEnforceable;
            }

            if (!StructuralCatalogueValidation.IsValid(in catalogue))
                return CatalogueLoadResult.InvalidStructuralComposition;

            catalogue.Hash = ComputeHash(in catalogue);
            return CatalogueLoadResult.Ok;
        }

        /// <summary>Hashes every generation-affecting pool, including structural composition.</summary>
        public static ulong ComputeHash(in FeatureCatalogue catalogue)
        {
            ulong h = FeatureHash.Mix(catalogue.Version);

            for (var i = 0; i < catalogue.DefinitionCount; i++)
            {
                var d = catalogue.Definitions[i];

                h = FeatureHash.Mix(h ^ (ulong)(uint)d.Kind);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.BasePlane);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.Footprint.x);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.Footprint.y);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.Footprint.z);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.MaxSlope);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.FixedAltitude);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.Precedence);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.MaxPrimitives);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.SlotOffset);
                h = FeatureHash.Mix(h ^ (ulong)(uint)d.SlotCount);

                var piece = d.StructuralPiece;
                h = FeatureHash.Mix(h ^ piece.PieceId);
                h = FeatureHash.Mix(h ^ (ulong)piece.Role);
                h = FeatureHash.Mix(h ^ piece.Offers);
                h = FeatureHash.Mix(h ^ piece.Accepts);
                h = FeatureHash.Mix(h ^ (ulong)(uint)piece.LocalPosition.x);
                h = FeatureHash.Mix(h ^ (ulong)(uint)piece.LocalPosition.y);
                h = FeatureHash.Mix(h ^ (ulong)(uint)piece.LocalPosition.z);
                h = FeatureHash.Mix(h ^ (ulong)piece.Facing);
                h = HashInt3(h, piece.ClearanceMin);
                h = HashInt3(h, piece.ClearanceMax);
            }

            for (var i = 0; i < catalogue.Program.Length; i++)
                h = FeatureHash.Mix(h ^ (ulong)(uint)catalogue.Program[i]);

            for (var i = 0; i < catalogue.Parameters.Length; i++)
            {
                var p = catalogue.Parameters[i];
                h = FeatureHash.Mix(h ^ (ulong)(uint)p.Min);
                h = FeatureHash.Mix(h ^ (ulong)(uint)p.Max);
                h = FeatureHash.Mix(h ^ (ulong)(uint)p.Quantum);
            }

            for (var i = 0; i < catalogue.Materials.Length; i++)
                h = FeatureHash.Mix(h ^ catalogue.Materials[i]);

            for (var i = 0; i < catalogue.Slots.Length; i++)
            {
                var s = catalogue.Slots[i];
                h = FeatureHash.Mix(h ^ s.SocketId);
                h = FeatureHash.Mix(h ^ (ulong)s.Role);
                h = FeatureHash.Mix(h ^ s.Offers);
                h = FeatureHash.Mix(h ^ s.Accepts);
                h = HashInt3(h, s.LocalPosition);
                h = FeatureHash.Mix(h ^ (ulong)s.Facing);
                h = FeatureHash.Mix(h ^ (ulong)(uint)s.DefinitionId);
                h = HashInt3(h, s.LocalMin);
                h = HashInt3(h, s.LocalMax);
                h = HashInt3(h, s.ClearanceMin);
                h = HashInt3(h, s.ClearanceMax);
                h = FeatureHash.Mix(h ^ (ulong)(uint)s.CountMin);
                h = FeatureHash.Mix(h ^ (ulong)(uint)s.CountMax);
                h = FeatureHash.Mix(h ^ s.Capacity);
                h = FeatureHash.Mix(h ^ (ulong)(uint)s.Spacing);
                h = FeatureHash.Mix(h ^ (ulong)s.Flags);
                h = HashInt3(h, s.SupportProbeMin);
                h = HashInt3(h, s.SupportProbeMax);
                h = FeatureHash.Mix(h ^ s.MinimumSupportContacts);
                h = FeatureHash.Mix(h ^ (ulong)s.DecorationHandoff);
            }

            for (var i = 0; i < catalogue.Rules.Length; i++)
            {
                var r = catalogue.Rules[i];
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.DefinitionId);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.CellEdge);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.AttemptsPerCell);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.AcceptProbability);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.MinAltitude);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.MaxAltitude);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.MaxSlope);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.MinSpacing);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.ClusterMin);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.ClusterMax);
                h = FeatureHash.Mix(h ^ (ulong)(uint)r.ExclusionMask);
            }

            for (var i = 0; i < catalogue.ExplicitPlacements.Length; i++)
            {
                var e = catalogue.ExplicitPlacements[i];
                h = HashInt3(h, e.Position);
                h = FeatureHash.Mix(h ^ e.Orientation);
            }

            return h;
        }

        private static ulong HashInt3(ulong h, Unity.Mathematics.int3 value)
        {
            h = FeatureHash.Mix(h ^ (ulong)(uint)value.x);
            h = FeatureHash.Mix(h ^ (ulong)(uint)value.y);
            return FeatureHash.Mix(h ^ (ulong)(uint)value.z);
        }

        public static FeatureCatalogue Allocate(
            int definitions, int rules, int parameters, int anchors, int slots,
            int programLength, int materials, int explicitPlacements, int overrides,
            Allocator allocator)
        {
            return new FeatureCatalogue
            {
                Version = SupportedVersion,
                Definitions = new NativeArray<FeatureDefinition>(definitions, allocator),
                Rules = new NativeArray<PlacementRule>(rules, allocator),
                Parameters = new NativeArray<ParameterSpec>(parameters, allocator),
                Anchors = new NativeArray<AnchorSpec>(anchors, allocator),
                Slots = new NativeArray<SlotSpec>(slots, allocator),
                Program = new NativeArray<int>(programLength, allocator),
                Materials = new NativeArray<byte>(materials, allocator),
                ExplicitPlacements = new NativeArray<ExplicitPlacement>(explicitPlacements, allocator),
                ParameterOverrides = new NativeArray<int>(overrides, allocator),
            };
        }
    }
}
