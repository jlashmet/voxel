using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Client
{
    public static class EventApplication
    {
        public static bool Apply(ref RegionTable table, ref BrickPool pool, in AlterationEvent evt, out NativeList<int3> affectedBricks)
        {
            return DeterministicAlterationApplier.TryApply(ref table, ref pool, in evt, out affectedBricks);
        }

        public static void TriggerInfrastructureUpdates(ref RegionTable table, in BrickPool pool, in NativeArray<int3> affectedRegions, int mipLevelCount, NativeArray<ulong>[][] mipStorage)
        {
            _ = table;
            _ = pool;
            _ = affectedRegions;
            _ = mipLevelCount;
            _ = mipStorage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ApplyWithArbitration(ref RegionTable table, ref BrickPool pool, in NativeArray<AlterationEvent> events)
        {
            bool anyChanged = false;
            for (int i = 0; i < events.Length; i++)
            {
                AlterationEvent evt = events[i];
                bool changed = DeterministicAlterationApplier.TryApply(ref table, ref pool, in evt, out NativeList<int3> affectedBricks);
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
