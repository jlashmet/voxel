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
    /// Derives stable semantic hooks from an already-authored cave result. Hooks deliberately live at
    /// the guaranteed reachable main-path end rather than inventing gameplay-aware placement rules.
    /// Independent semantic salts keep decoration/resource/water consumers from perturbing each other.
    /// </summary>
    public static class CaveHookPlanner
    {
        private const ulong DecorationSalt = 0x4445434F52415445ul; // DECORATE
        private const ulong ResourceSalt = 0x5245534F55524345ul;   // RESOURCE
        private const ulong WaterSalt = 0x5741544552484F4Ful;      // WATERHOO

        public static CaveHookSet AtMainPathEnd(
            in CaveGenerationRequest request,
            int3 mainPathEnd)
        {
            var hooks = new CaveHookSet();
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Decoration,
                Position = mainPathEnd,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ DecorationSalt)),
            });
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Resource,
                Position = mainPathEnd,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ ResourceSalt)),
            });
            hooks.Items.Add(new CaveResolvedHook
            {
                Kind = CaveHookKind.Water,
                Position = mainPathEnd,
                Seed = NonZero(FeatureHash.Mix(request.Seed ^ WaterSalt)),
            });
            return hooks;
        }

        private static ulong NonZero(ulong seed) => seed == 0 ? 1ul : seed;
    }
}
