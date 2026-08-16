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
    }

    /// <summary>
    /// Builds a catalogue blob and computes its hash.
    ///
    /// The checks here are the ones that must hold before *anything* generates, because a
    /// catalogue that violates them produces a world that is wrong rather than a world that fails.
    /// Deeper validation — footprint proofs over the parameter space, degenerate combinations,
    /// slot cycles — belongs in <c>CatalogueValidation</c> and runs at authoring time.
    /// </summary>
    public static class FeatureCatalogueBuilder
    {
        /// <summary>Format version this build implements.</summary>
        public const uint SupportedVersion = 1;

        /// <summary>
        /// Finalises a catalogue: checks the load-bearing invariants and computes the identity
        /// hash.
        ///
        /// Refuses rather than repairs. A catalogue quietly corrected on one machine and not
        /// another is two different worlds, which is the failure mode this whole design is built
        /// to avoid.
        /// </summary>
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
                if (!catalogue.Definitions[i].FootprintWithinBudget)
                    return CatalogueLoadResult.FootprintExceedsBudget;
            }

            for (var i = 0; i < catalogue.Rules.Length; i++)
            {
                if (!catalogue.Rules[i].SpacingEnforceable)
                    return CatalogueLoadResult.SpacingNotEnforceable;
            }

            catalogue.Hash = ComputeHash(in catalogue);
            return CatalogueLoadResult.Ok;
        }

        /// <summary>
        /// Hashes everything that affects the generated world.
        ///
        /// Deliberately covers the program body, material mapping and placement rules, not just
        /// the definition headers: a single changed opcode or material slot changes the world,
        /// and a hash that missed it would let two clients agree they share a world they do not.
        /// </summary>
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

            // Material pool entries are part of generation identity. Definitions address this pool
            // by offset/count, so changing a semantic material assignment must invalidate the hash
            // even when geometry, programs and placement remain byte-for-byte identical.
            for (var i = 0; i < catalogue.Materials.Length; i++)
                h = FeatureHash.Mix(h ^ catalogue.Materials[i]);

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
                h = FeatureHash.Mix(h ^ (ulong)(uint)e.Position.x);
                h = FeatureHash.Mix(h ^ (ulong)(uint)e.Position.y);
                h = FeatureHash.Mix(h ^ (ulong)(uint)e.Position.z);
                h = FeatureHash.Mix(h ^ e.Orientation);
            }

            return h;
        }

        /// <summary>Allocates the pools for a catalogue being built.</summary>
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
