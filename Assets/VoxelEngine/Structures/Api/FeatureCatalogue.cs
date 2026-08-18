using System;
using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// The set of definitions available to a world, with their placement rules.
    ///
    /// A catalogue is **world identity**, not content. The world's shape is derived from
    /// `(seed, catalogue)`, so two clients holding different catalogues generate different worlds
    /// while both believing they are in the same one. <see cref="Hash"/> exists so that join can
    /// refuse rather than reconcile.
    ///
    /// Everything is a flat pool with definitions holding offsets into it, so the whole structure
    /// is blittable and readable from a Burst job with no managed indirection.
    ///
    /// Immutable once loaded. There is no API to change a definition, because changing one
    /// mid-session would silently rewrite the world under players standing in it.
    /// </summary>
    public struct FeatureCatalogue : IDisposable
    {
        /// <summary>Format version. A world refuses a catalogue its evaluator does not implement.</summary>
        public uint Version;

        /// <summary>
        /// Covers every generation-affecting pool, including material assignments. Part of world
        /// identity alongside the seed: compared at join, and a mismatch is refused.
        /// </summary>
        public ulong Hash;

        public NativeArray<FeatureDefinition> Definitions;
        public NativeArray<PlacementRule> Rules;

        // Shared pools. Definitions and rules address these by offset and count.
        public NativeArray<ParameterSpec> Parameters;
        public NativeArray<AnchorSpec> Anchors;
        public NativeArray<SlotSpec> Slots;
        public NativeArray<int> Program;
        public NativeArray<byte> Materials;
        public NativeArray<ExplicitPlacement> ExplicitPlacements;
        public NativeArray<int> ParameterOverrides;

        public bool IsCreated => Definitions.IsCreated;

        public int DefinitionCount => Definitions.IsCreated ? Definitions.Length : 0;

        /// <summary>
        /// Largest footprint any definition declares, on any axis.
        ///
        /// This is the radius of the neighbourhood every region scans, so it is the number that
        /// decides what placement costs across the whole world. One oversized definition taxes
        /// every region, including regions that contain nothing.
        /// </summary>
        public int MaxFootprintExtent()
        {
            int max = 0;

            for (var i = 0; i < DefinitionCount; i++)
            {
                var f = Definitions[i].Footprint;
                if (f.x > max) max = f.x;
                if (f.y > max) max = f.y;
                if (f.z > max) max = f.z;
            }

            return max;
        }

        public void Dispose()
        {
            if (Definitions.IsCreated) Definitions.Dispose();
            if (Rules.IsCreated) Rules.Dispose();
            if (Parameters.IsCreated) Parameters.Dispose();
            if (Anchors.IsCreated) Anchors.Dispose();
            if (Slots.IsCreated) Slots.Dispose();
            if (Program.IsCreated) Program.Dispose();
            if (Materials.IsCreated) Materials.Dispose();
            if (ExplicitPlacements.IsCreated) ExplicitPlacements.Dispose();
            if (ParameterOverrides.IsCreated) ParameterOverrides.Dispose();
        }
    }
}
