using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Semantic purpose of one authored keep floor.</summary>
    public enum CastleKeepFloorPurpose : byte
    {
        GreatHall,
        Bedchamber,
        LibraryAndStores,
    }

    /// <summary>
    /// Pure planning data for one keep floor. Geometry/furnishing code consumes this contract;
    /// it does not decide the floor's purpose or reroll its variable accent placements.
    /// </summary>
    public readonly struct CastleKeepFloorPlan
    {
        public readonly int FloorIndex;
        public readonly CastleKeepFloorPurpose Purpose;
        public readonly bool HasPartition;
        public readonly uint SemanticSeed;
        public readonly CastleRoomAccentPlan Accents;

        public CastleKeepFloorPlan(
            int floorIndex,
            CastleKeepFloorPurpose purpose,
            bool hasPartition,
            uint semanticSeed,
            CastleRoomAccentPlan accents = null)
        {
            FloorIndex = floorIndex;
            Purpose = purpose;
            HasPartition = hasPartition;
            SemanticSeed = semanticSeed;
            Accents = accents;
        }
    }

    /// <summary>
    /// Immutable semantic stack for a keep interior. Anchor floors remain stable while intermediate
    /// upper floors may vary between supported room purposes without changing Runtime realization.
    /// </summary>
    public sealed class CastleKeepInteriorPlan
    {
        private readonly CastleKeepFloorPlan[] _floors;

        internal CastleKeepInteriorPlan(CastleKeepFloorPlan[] floors)
        {
            _floors = floors ?? Array.Empty<CastleKeepFloorPlan>();
        }

        public int FloorCount => _floors.Length;

        public CastleKeepFloorPlan Floor(int index)
        {
            if ((uint)index >= (uint)_floors.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _floors[index];
        }

        public CastleKeepFloorPlan[] SnapshotFloors() =>
            (CastleKeepFloorPlan[])_floors.Clone();
    }

    /// <summary>
    /// Pure keep-interior semantic planner. Per-floor seeds come from the named Rooms stream so
    /// adding unrelated wall, dungeon, or decoration choices cannot perturb room identity. Room
    /// purpose uses a separate element seed from accent placement, so changing the purpose policy
    /// later does not silently reroll the accents attached to unchanged floors.
    /// </summary>
    public static class CastleKeepInteriorPlanner
    {
        private const uint UpperFloorPurposeElementBase = 0x10000u;
        private const uint BedchamberChancePercent = 35u;

        public static CastleKeepInteriorPlan Create(in CastlePlan plan)
        {
            if (plan.Floors <= 0)
                throw new ArgumentOutOfRangeException(nameof(plan), "Castle must have at least one keep floor.");

            var floors = new CastleKeepFloorPlan[plan.Floors];
            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPurpose purpose = ChoosePurpose(in plan, floor);
                bool partitioned = purpose == CastleKeepFloorPurpose.LibraryAndStores;

                uint semanticSeed = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Rooms, (uint)floor);
                var semanticFloor = new CastleKeepFloorPlan(
                    floor,
                    purpose,
                    partitioned,
                    semanticSeed);
                CastleRoomAccentPlan accents = CastleRoomAccentPlanner.Create(
                    in plan, in semanticFloor);
                floors[floor] = new CastleKeepFloorPlan(
                    floor,
                    purpose,
                    partitioned,
                    semanticSeed,
                    accents);
            }

            return new CastleKeepInteriorPlan(floors);
        }

        private static CastleKeepFloorPurpose ChoosePurpose(in CastlePlan plan, int floor)
        {
            if (floor == 0)
                return CastleKeepFloorPurpose.GreatHall;

            // Keep the principal chamber stable even in the two-floor edge case.
            if (floor == 1)
                return CastleKeepFloorPurpose.Bedchamber;

            // Once a keep has a third floor, preserve at least one dedicated library/storey at the top.
            if (floor == plan.Floors - 1)
                return CastleKeepFloorPurpose.LibraryAndStores;

            uint choice = CastleSeedPartition.Derive(
                plan.Seed,
                CastleSeedDomain.Rooms,
                UpperFloorPurposeElementBase + (uint)floor);
            return choice % 100u < BedchamberChancePercent
                ? CastleKeepFloorPurpose.Bedchamber
                : CastleKeepFloorPurpose.LibraryAndStores;
        }
    }
}
