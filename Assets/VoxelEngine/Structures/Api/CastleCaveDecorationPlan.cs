using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleCaveDecorationKind : byte
    {
        EntryPool,
        DryCauseway,
        CrystalSpire,
        MossSpire,
        Stalagmite,
        Stalactite,
        LightMarker,
    }

    public struct CastleCaveDecorationSpec
    {
        public int Id;
        public int ChamberId;
        public CastleCaveDecorationKind Kind;
        public int3 Position;
        public int3 Size;
        public int Radius;
        public int Height;
    }

    /// <summary>Pure castle-specific dressing plan for a validated CavePlan.</summary>
    public sealed class CastleCaveDecorationPlan
    {
        public uint CaveSeed { get; }
        public CastleCaveDecorationSpec[] Elements { get; }

        internal CastleCaveDecorationPlan(uint caveSeed, CastleCaveDecorationSpec[] elements)
        {
            CaveSeed = caveSeed;
            Elements = elements;
        }

        public CastleCaveDecorationPlan Snapshot() => new CastleCaveDecorationPlan(
            CaveSeed,
            Elements != null ? (CastleCaveDecorationSpec[])Elements.Clone() : null);
    }
}
