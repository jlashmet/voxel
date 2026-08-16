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
    /// Immutable semantic stack for a keep interior. The initial planner deliberately preserves
    /// the existing authored recipe (hall, bedchamber, upper library/stores) while moving the
    /// decision out of Runtime. Later topology work can vary this data without changing the
    /// furnishing boundary.
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
    /// adding unrelated wall, dungeon, or decoration choices cannot perturb room identity. Any
    /// variable furnishing accents are resolved here and carried as immutable plan data.
    /// </summary>
    public static class CastleKeepInteriorPlanner
    {
        public static CastleKeepInteriorPlan Create(in CastlePlan plan)
        {
            if (plan.Floors <= 0)
                throw new ArgumentOutOfRangeException(nameof(plan), "Castle must have at least one keep floor.");

            var floors = new CastleKeepFloorPlan[plan.Floors];
            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPurpose purpose;
                bool partitioned;
                if (floor == 0)
                {
                    purpose = CastleKeepFloorPurpose.GreatHall;
                    partitioned = false;
                }
                else if (floor == 1)
                {
                    purpose = CastleKeepFloorPurpose.Bedchamber;
                    partitioned = false;
                }
                else
                {
                    purpose = CastleKeepFloorPurpose.LibraryAndStores;
                    partitioned = true;
                }

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
    }
}
