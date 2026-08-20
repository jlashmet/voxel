using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Semantic extension points emitted by cave generation. The engine does not interpret these as
    /// loot, harvestables, VFX, or gameplay state; downstream composition decides what each hook means.
    /// </summary>
    public enum CaveHookKind : byte
    {
        Decoration = 0,
        Resource = 1,
        Water = 2,
    }

    public struct CaveResolvedHook
    {
        public CaveHookKind Kind;
        public int3 Position;
        public ulong Seed;
    }

    /// <summary>Small allocation-free set of cave extension hooks for one authored network.</summary>
    public struct CaveHookSet
    {
        public FixedList128Bytes<CaveResolvedHook> Items;
        public int Count => Items.Length;
    }

    /// <summary>
    /// Derives stable semantic hooks from already-authored cave traversal. The compatibility helper
    /// keeps main-path-end behaviour available, while constrained selection lets downstream systems
    /// request progression-aware placement without reimplementing cave traversal ranking.
    /// Independent semantic salts keep decoration/resource/water consumers from perturbing each other.
    /// </summary>
    public static class CaveHookPlanner
    {
        private const ulong DecorationSalt = 0x4445434F52415445ul; // DECORATE
        private const ulong ResourceSalt = 0x5245534F55524345ul;   // RESOURCE
        private const ulong WaterSalt = 0x5741544552484F4Ful;      // WATERHOO

        public static CaveHookSet AtMainPathEnd(
            in CaveGenerationRequest request,
            int3 mainPathEnd) =>
            AtPosition(in request, mainPathEnd);

        /// <summary>
        /// Resolves hooks at the deepest terminal satisfying the supplied traversal requirements.
        /// Returns false instead of silently falling back when hard requirements cannot be met.
        /// </summary>
        public static bool TryAtDeepestCandidate(
            in CaveGenerationRequest request,
            in CaveTraversalCandidateSet candidates,
            in CavePlacementRequirements requirements,
            out CaveHookSet hooks)
        {
            CavePlacementPreferences preferences = CavePlacementPreferences.None;
            return TryAtBestCandidate(
                in request, in candidates, in requirements, in preferences, out hooks);
        }

        /// <summary>
        /// Resolves hooks at the best hard-valid traversal candidate after applying soft preferences.
        /// Preferences only rank candidates that already satisfy requirements.
        /// </summary>
        public static bool TryAtBestCandidate(
            in CaveGenerationRequest request,
            in CaveTraversalCandidateSet candidates,
            in CavePlacementRequirements requirements,
            in CavePlacementPreferences preferences,
            out CaveHookSet hooks)
        {
            hooks = default;
            if (!CavePlacementResolver.TrySelectBest(
                    in candidates, in requirements, in preferences, out CaveTraversalCandidate selected))
                return false;

            hooks = AtPosition(in request, selected.Position);
            return true;
        }

        private static CaveHookSet AtPosition(
            in CaveGenerationRequest request,
            int3 position)
        {
            var hooks = new CaveHookSet();
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Decoration,
                Position = position,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ DecorationSalt)),
            });
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Resource,
                Position = position,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ ResourceSalt)),
            });
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Water,
                Position = position,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ WaterSalt)),
            });
            return hooks;
        }

        private static ulong NonZero(ulong seed) => seed == 0 ? 1ul : seed;
    }
}
