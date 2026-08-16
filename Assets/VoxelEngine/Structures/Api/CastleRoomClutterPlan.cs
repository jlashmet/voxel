using System;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// One planned pedestal/clutter element inside a keep floor. Coordinates are local X/Z offsets
    /// from the semantic keep centre; Runtime only converts them to world voxels and authors them.
    /// </summary>
    public readonly struct CastleRoomClutterSpec
    {
        public readonly int Id;
        public readonly int FloorIndex;
        public readonly int2 LocalCentre;
        public readonly int Radius;
        public readonly int Height;

        public CastleRoomClutterSpec(
            int id,
            int floorIndex,
            int2 localCentre,
            int radius,
            int height)
        {
            Id = id;
            FloorIndex = floorIndex;
            LocalCentre = localCentre;
            Radius = radius;
            Height = height;
        }
    }

    /// <summary>Immutable room-clutter choices for the complete keep floor stack.</summary>
    public sealed class CastleRoomClutterPlan
    {
        private readonly CastleRoomClutterSpec[] _items;

        internal CastleRoomClutterPlan(CastleRoomClutterSpec[] items)
        {
            _items = items ?? Array.Empty<CastleRoomClutterSpec>();
        }

        public int Count => _items.Length;

        public CastleRoomClutterSpec Item(int index)
        {
            if ((uint)index >= (uint)_items.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }

        public CastleRoomClutterSpec[] SnapshotItems() =>
            (CastleRoomClutterSpec[])_items.Clone();
    }

    public enum CastleRoomClutterPlanIssue : byte
    {
        None = 0,
        MissingFloorPlan,
        FloorCountMismatch,
        ItemIdMismatch,
        InvalidFloorIndex,
        InvalidDimensions,
        ItemOutsideKeepInterior,
    }

    /// <summary>
    /// Freezes the legacy room-furnisher random pedestal pass into deterministic planning data.
    /// The intentionally unusual loop-condition draw order matches the historical Runtime code so
    /// this migration changes ownership, not generated output.
    /// </summary>
    public static class CastleRoomClutterPlanner
    {
        private const int InnerWallInset = 8;
        private const int LeftWallOffset = 22;
        private const int RightWallOffset = 30;
        private const int NearEndOffset = 8;
        private const int FarEndOffset = 12;

        public static CastleRoomClutterPlan Create(
            in CastlePlan plan,
            CastleKeepFloorPlan[] floors)
        {
            if (floors == null || floors.Length != plan.Floors)
            {
                throw new ArgumentException(
                    "Room clutter planning requires one semantic keep-floor plan per floor.",
                    nameof(floors));
            }

            var items = new CastleRoomClutterSpec[plan.Floors * 4];
            int cursor = 0;
            for (int floor = 0; floor < floors.Length; floor++)
            {
                CastleKeepFloorPlan floorPlan = floors[floor];
                if (floorPlan.FloorIndex != floor)
                {
                    throw new ArgumentException(
                        "Keep-floor plans must be ordered before room clutter is planned.",
                        nameof(floors));
                }

                var rng = new Random(floorPlan.SemanticSeed);

                // Preserve the historical loop exactly: NextInt(2, 5) is re-evaluated on every
                // condition check rather than drawn once before the loop.
                for (int item = 0; item < rng.NextInt(2, 5); item++)
                {
                    bool leftWall = rng.NextBool();
                    int localX = leftWall
                        ? -plan.KeepHalfX + InnerWallInset + LeftWallOffset
                        : plan.KeepHalfX - InnerWallInset - RightWallOffset;
                    int localZ = rng.NextInt(
                        -plan.KeepHalfZ + InnerWallInset + NearEndOffset,
                        plan.KeepHalfZ - InnerWallInset - FarEndOffset);
                    int radius = rng.NextInt(4, 7);
                    int height = rng.NextInt(8, 14);

                    items[cursor] = new CastleRoomClutterSpec(
                        cursor,
                        floor,
                        new int2(localX, localZ),
                        radius,
                        height);
                    cursor++;
                }
            }

            if (cursor != items.Length)
                Array.Resize(ref items, cursor);

            var result = new CastleRoomClutterPlan(items);
            if (!TryValidate(in plan, floors, result, out CastleRoomClutterPlanIssue issue))
                throw new InvalidOperationException($"Planned keep room clutter is invalid: {issue}.");
            return result;
        }

        public static bool TryValidate(
            in CastlePlan plan,
            CastleKeepFloorPlan[] floors,
            CastleRoomClutterPlan clutter,
            out CastleRoomClutterPlanIssue issue)
        {
            if (floors == null || clutter == null)
            {
                issue = CastleRoomClutterPlanIssue.MissingFloorPlan;
                return false;
            }

            if (floors.Length != plan.Floors)
            {
                issue = CastleRoomClutterPlanIssue.FloorCountMismatch;
                return false;
            }

            int minX = -plan.KeepHalfX + InnerWallInset;
            int maxX = plan.KeepHalfX - InnerWallInset;
            int minZ = -plan.KeepHalfZ + InnerWallInset;
            int maxZ = plan.KeepHalfZ - InnerWallInset;

            for (int i = 0; i < clutter.Count; i++)
            {
                CastleRoomClutterSpec item = clutter.Item(i);
                if (item.Id != i)
                {
                    issue = CastleRoomClutterPlanIssue.ItemIdMismatch;
                    return false;
                }

                if (item.FloorIndex < 0 || item.FloorIndex >= floors.Length ||
                    floors[item.FloorIndex].FloorIndex != item.FloorIndex)
                {
                    issue = CastleRoomClutterPlanIssue.InvalidFloorIndex;
                    return false;
                }

                if (item.Radius <= 0 || item.Height <= 0)
                {
                    issue = CastleRoomClutterPlanIssue.InvalidDimensions;
                    return false;
                }

                if (item.LocalCentre.x - item.Radius < minX ||
                    item.LocalCentre.x + item.Radius > maxX ||
                    item.LocalCentre.y - item.Radius < minZ ||
                    item.LocalCentre.y + item.Radius > maxZ)
                {
                    issue = CastleRoomClutterPlanIssue.ItemOutsideKeepInterior;
                    return false;
                }
            }

            issue = CastleRoomClutterPlanIssue.None;
            return true;
        }
    }
}
