using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Runtime.Client
{
    public static class EventApplication
    {
        public static bool Apply(
            IAlterationApplier applier,
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            return applier.TryApply(storage, in evt, out affectedBricks);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplyWithArbitration(
            IAlterationApplier applier,
            IRegionMutationStore storage,
            in NativeArray<AlterationEvent> events)
        {
            bool anyChanged = false;
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                bool changed = applier.TryApply(
                    storage, in evt, out NativeList<int3> affectedBricks);
                if (affectedBricks.IsCreated) affectedBricks.Dispose();
                anyChanged |= changed;
            }
            return anyChanged;
        }
    }

    public struct S_AlterationEvent
    {
        public uint Tick;
        public int RegionCoord;
        public byte EventKind;
        public int OriginX, OriginY, OriginZ;
        public byte ShapeRadius;
        public ushort ShapeDataYz;
        public byte Material;
        public uint Seed;
        public ushort PlayerId;
        public ushort Sequence;
    }
}
