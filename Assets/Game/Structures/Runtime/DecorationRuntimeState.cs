using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Interaction/persistence metadata kept independently from render geometry.</summary>
    public struct DecorationRuntimeMetadata
    {
        public GeneratedPropId Id;
        public DecorationPropFamily Family;
        public DecorationInteractionFlags Interaction;
        public DecorationBounds BaselineBounds;
        public uint Variant;

        public bool IsInteractable =>
            (Interaction & (DecorationInteractionFlags.Destructible |
                            DecorationInteractionFlags.Container |
                            DecorationInteractionFlags.Lootable |
                            DecorationInteractionFlags.Movable)) != 0;
    }

    /// <summary>
    /// One static combination group. Placements are referenced by index so geometry builders can
    /// retain stable prop ranges without creating one runtime object per generated prop.
    /// </summary>
    public sealed class DecorationStaticBatch
    {
        public DecorationRenderBackend Backend;
        public int[] PlacementIndices = Array.Empty<int>();
    }

    public struct DecorationDynamicProp
    {
        public int PlacementIndex;
        public GeneratedPropId Id;
    }

    public sealed class DecorationRuntimePlan
    {
        public DecorationRuntimeMetadata[] Metadata = Array.Empty<DecorationRuntimeMetadata>();
        public DecorationStaticBatch[] StaticBatches = Array.Empty<DecorationStaticBatch>();
        public DecorationDynamicProp[] DynamicProps = Array.Empty<DecorationDynamicProp>();

        public int PlacementCount => Metadata?.Length ?? 0;
    }

    public static class DecorationRuntimePlanner
    {
        public static bool TryBuild(
            DecorationPlacement[] placements,
            out DecorationRuntimePlan plan)
        {
            plan = new DecorationRuntimePlan();
            if (placements == null)
                return false;

            var metadata = new DecorationRuntimeMetadata[placements.Length];
            var dynamic = new List<DecorationDynamicProp>();
            var backendIndices = new List<int>[4]
            {
                new List<int>(), new List<int>(), new List<int>(), new List<int>(),
            };

            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!placement.IsWellFormed)
                    return false;

                metadata[i] = new DecorationRuntimeMetadata
                {
                    Id = placement.Id,
                    Family = placement.Family,
                    Interaction = placement.Interaction,
                    BaselineBounds = placement.Bounds,
                    Variant = placement.Variant,
                };

                if ((placement.Interaction & DecorationInteractionFlags.Movable) != 0)
                {
                    dynamic.Add(new DecorationDynamicProp
                    {
                        PlacementIndex = i,
                        Id = placement.Id,
                    });
                    continue;
                }

                int backend = (int)placement.Backend;
                if (backend < 0 || backend >= backendIndices.Length)
                    return false;
                backendIndices[backend].Add(i);
            }

            int batchCount = 0;
            for (int i = 0; i < backendIndices.Length; i++)
                if (backendIndices[i].Count > 0)
                    batchCount++;

            var batches = new DecorationStaticBatch[batchCount];
            int output = 0;
            for (int i = 0; i < backendIndices.Length; i++)
            {
                if (backendIndices[i].Count == 0)
                    continue;
                batches[output++] = new DecorationStaticBatch
                {
                    Backend = (DecorationRenderBackend)i,
                    PlacementIndices = backendIndices[i].ToArray(),
                };
            }

            plan.Metadata = metadata;
            plan.StaticBatches = batches;
            plan.DynamicProps = dynamic.ToArray();
            return true;
        }
    }

    public enum DecorationDetailClass : byte
    {
        Essential = 0,
        Standard = 1,
        Clutter = 2,
    }

    public static class DecorationDetailPolicy
    {
        public const float EssentialDistanceVoxels = 1200f;
        public const float StandardDistanceVoxels = 600f;
        public const float ClutterDistanceVoxels = 240f;

        public static DecorationDetailClass Classify(DecorationPropFamily family)
        {
            switch (family)
            {
                case DecorationPropFamily.Bed:
                case DecorationPropFamily.Dresser:
                case DecorationPropFamily.Table:
                case DecorationPropFamily.Bench:
                case DecorationPropFamily.Bookcase:
                case DecorationPropFamily.Fireplace:
                case DecorationPropFamily.Altar:
                case DecorationPropFamily.Campfire:
                    return DecorationDetailClass.Essential;

                case DecorationPropFamily.Candle:
                case DecorationPropFamily.Painting:
                case DecorationPropFamily.WallTorch:
                case DecorationPropFamily.Lantern:
                case DecorationPropFamily.Banner:
                case DecorationPropFamily.Curtain:
                    return DecorationDetailClass.Standard;

                default:
                    return DecorationDetailClass.Clutter;
            }
        }

        public static float MaxDistanceVoxels(DecorationPropFamily family)
        {
            switch (Classify(family))
            {
                case DecorationDetailClass.Essential:
                    return EssentialDistanceVoxels;
                case DecorationDetailClass.Standard:
                    return StandardDistanceVoxels;
                default:
                    return ClutterDistanceVoxels;
            }
        }

        public static bool ShouldInclude(DecorationPropFamily family, float distanceVoxels) =>
            distanceVoxels <= MaxDistanceVoxels(family);

        public static DecorationPlacement[] Filter(
            DecorationPlacement[] placements,
            float distanceVoxels)
        {
            if (placements == null || distanceVoxels < 0f)
                return Array.Empty<DecorationPlacement>();

            int count = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].IsWellFormed && ShouldInclude(placements[i].Family, distanceVoxels))
                    count++;
            }

            var filtered = new DecorationPlacement[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].IsWellFormed && ShouldInclude(placements[i].Family, distanceVoxels))
                    filtered[output++] = placements[i];
            }
            return filtered;
        }
    }

    [Flags]
    public enum DecorationPersistenceFlags : byte
    {
        None = 0,
        Destroyed = 1 << 0,
        Looted = 1 << 1,
        Moved = 1 << 2,
    }

    /// <summary>Saved override for one deterministic generated baseline prop.</summary>
    public struct DecorationPersistenceDelta
    {
        public GeneratedPropId Id;
        public DecorationPersistenceFlags Flags;
        public DecorationBounds MovedBounds;
        public int3 MovedFacing;

        public bool IsWellFormed
        {
            get
            {
                if (Id.Value == 0 || Flags == DecorationPersistenceFlags.None)
                    return false;
                if ((Flags & DecorationPersistenceFlags.Moved) == 0)
                    return true;
                return MovedBounds.IsWellFormed &&
                       math.csum(math.abs(MovedFacing)) == 1;
            }
        }
    }

    public struct DecorationResolvedState
    {
        public DecorationPlacement Placement;
        public DecorationPersistenceFlags Persistence;

        public bool IsVisible =>
            (Persistence & DecorationPersistenceFlags.Destroyed) == 0;
        public bool IsLooted =>
            (Persistence & DecorationPersistenceFlags.Looted) != 0;
    }

    public static class DecorationPersistenceResolver
    {
        public static bool TryApply(
            DecorationPlacement[] deterministicBaseline,
            DecorationPersistenceDelta[] deltas,
            out DecorationResolvedState[] resolved)
        {
            resolved = Array.Empty<DecorationResolvedState>();
            if (deterministicBaseline == null)
                return false;

            if (!ValidateDeltas(deltas))
                return false;

            var states = new DecorationResolvedState[deterministicBaseline.Length];
            for (int i = 0; i < deterministicBaseline.Length; i++)
            {
                DecorationPlacement placement = deterministicBaseline[i];
                if (!placement.IsWellFormed)
                    return false;

                DecorationPersistenceFlags persistence = DecorationPersistenceFlags.None;
                int deltaIndex = FindDelta(deltas, placement.Id);
                if (deltaIndex >= 0)
                {
                    DecorationPersistenceDelta delta = deltas[deltaIndex];
                    persistence = delta.Flags;
                    if ((delta.Flags & DecorationPersistenceFlags.Moved) != 0)
                    {
                        placement.Bounds = delta.MovedBounds;
                        placement.Facing = delta.MovedFacing;
                    }
                }

                states[i] = new DecorationResolvedState
                {
                    Placement = placement,
                    Persistence = persistence,
                };
            }

            resolved = states;
            return true;
        }

        private static bool ValidateDeltas(DecorationPersistenceDelta[] deltas)
        {
            if (deltas == null)
                return true;

            for (int i = 0; i < deltas.Length; i++)
            {
                if (!deltas[i].IsWellFormed)
                    return false;
                for (int j = i + 1; j < deltas.Length; j++)
                    if (deltas[i].Id == deltas[j].Id)
                        return false;
            }
            return true;
        }

        private static int FindDelta(
            DecorationPersistenceDelta[] deltas,
            GeneratedPropId id)
        {
            if (deltas == null)
                return -1;
            for (int i = 0; i < deltas.Length; i++)
                if (deltas[i].Id == id)
                    return i;
            return -1;
        }
    }
}
