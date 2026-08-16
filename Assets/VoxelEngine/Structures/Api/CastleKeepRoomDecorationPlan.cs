using System;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>One planner-owned small furnishing accent inside a keep floor.</summary>
    public readonly struct CastleKeepRoomDecorationSpec
    {
        public readonly int Id;
        public readonly int2 LocalCentre;
        public readonly int Radius;
        public readonly int Height;

        public CastleKeepRoomDecorationSpec(int id, int2 localCentre, int radius, int height)
        {
            Id = id;
            LocalCentre = localCentre;
            Radius = radius;
            Height = height;
        }
    }

    /// <summary>
    /// Fixed-capacity value plan for the only randomized furnishing accents in the legacy keep
    /// recipe. Keeping this as a value type avoids nested caller-owned arrays inside floor plans.
    /// </summary>
    public readonly struct CastleKeepRoomDecorationPlan
    {
        public readonly int FloorIndex;
        public readonly int Count;
        private readonly CastleKeepRoomDecorationSpec _a;
        private readonly CastleKeepRoomDecorationSpec _b;
        private readonly CastleKeepRoomDecorationSpec _c;
        private readonly CastleKeepRoomDecorationSpec _d;

        internal CastleKeepRoomDecorationPlan(
            int floorIndex,
            int count,
            in CastleKeepRoomDecorationSpec a,
            in CastleKeepRoomDecorationSpec b,
            in CastleKeepRoomDecorationSpec c,
            in CastleKeepRoomDecorationSpec d)
        {
            FloorIndex = floorIndex;
            Count = count;
            _a = a;
            _b = b;
            _c = c;
            _d = d;
        }

        public CastleKeepRoomDecorationSpec Decoration(int index)
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return index switch
            {
                0 => _a,
                1 => _b,
                2 => _c,
                3 => _d,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
        }
    }

    /// <summary>
    /// Freezes the legacy keep room's small side-table accents from the floor's independent Rooms
    /// seed stream. Runtime receives coordinates and dimensions and performs no random draws.
    /// </summary>
    public static class CastleKeepRoomDecorationPlanner
    {
        public static CastleKeepRoomDecorationPlan Create(
            in CastlePlan plan,
            in CastleKeepFloorPlan floor)
        {
            if (floor.FloorIndex < 0 || floor.FloorIndex >= plan.Floors)
                throw new ArgumentOutOfRangeException(nameof(floor));
            if (plan.KeepHalfX <= 38 || plan.KeepHalfZ <= 20)
                throw new ArgumentOutOfRangeException(nameof(plan));

            var rng = new Random(floor.SemanticSeed);
            CastleKeepRoomDecorationSpec a = default;
            CastleKeepRoomDecorationSpec b = default;
            CastleKeepRoomDecorationSpec c = default;
            CastleKeepRoomDecorationSpec d = default;
            int count = 0;

            // Preserve the historical draw order exactly. The loop condition itself drew a fresh
            // NextInt(2,5) on every iteration, so reproducing that unusual shape is seed-contract
            // behavior rather than simplifying it to one upfront count draw.
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int localX = leftWall
                    ? -plan.KeepHalfX + 30
                    : plan.KeepHalfX - 38;
                int localZ = rng.NextInt(-plan.KeepHalfZ + 16, plan.KeepHalfZ - 20);
                int radius = rng.NextInt(4, 7);
                int height = rng.NextInt(8, 14);
                var decoration = new CastleKeepRoomDecorationSpec(
                    count,
                    new int2(localX, localZ),
                    radius,
                    height);

                switch (count)
                {
                    case 0: a = decoration; break;
                    case 1: b = decoration; break;
                    case 2: c = decoration; break;
                    case 3: d = decoration; break;
                    default:
                        throw new InvalidOperationException("Keep room decoration planner exceeded fixed capacity.");
                }
                count++;
            }

            var result = new CastleKeepRoomDecorationPlan(
                floor.FloorIndex, count, in a, in b, in c, in d);
            if (!TryValidate(in plan, in floor, in result, out string error))
                throw new InvalidOperationException($"Planned keep room decorations are invalid: {error}");
            return result;
        }

        public static bool TryValidate(
            in CastlePlan plan,
            in CastleKeepFloorPlan floor,
            in CastleKeepRoomDecorationPlan decorations,
            out string error)
        {
            if (decorations.FloorIndex != floor.FloorIndex)
            {
                error = "decoration floor does not match keep floor";
                return false;
            }
            if (decorations.Count < 2 || decorations.Count > 4)
            {
                error = $"expected 2-4 room accents but found {decorations.Count}";
                return false;
            }

            int leftX = -plan.KeepHalfX + 30;
            int rightX = plan.KeepHalfX - 38;
            for (int i = 0; i < decorations.Count; i++)
            {
                CastleKeepRoomDecorationSpec decoration = decorations.Decoration(i);
                if (decoration.Id != i)
                {
                    error = $"decoration id {decoration.Id} is out of order at {i}";
                    return false;
                }
                if (decoration.LocalCentre.x != leftX && decoration.LocalCentre.x != rightX)
                {
                    error = $"decoration {i} is not attached to an authored side wall";
                    return false;
                }
                if (decoration.LocalCentre.y < -plan.KeepHalfZ + 16 ||
                    decoration.LocalCentre.y >= plan.KeepHalfZ - 20)
                {
                    error = $"decoration {i} leaves its authored Z range";
                    return false;
                }
                if (decoration.Radius < 4 || decoration.Radius > 6 ||
                    decoration.Height < 8 || decoration.Height > 13)
                {
                    error = $"decoration {i} has invalid dimensions";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
