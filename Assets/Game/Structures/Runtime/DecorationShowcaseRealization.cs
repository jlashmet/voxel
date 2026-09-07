using System;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DecorationShowcaseRealizationKind : byte
    {
        Decoration = 0,
        MineCave = 1,
        NaturalCave = 2,
        WorldObject = 3,
    }

    /// <summary>
    /// One production-realization payload selected from the canonical catalogue. The payload is a
    /// union over existing production types; it does not define geometry, materials, or content ids.
    /// Both validation and integration browsers consume this adapter so source-specific construction
    /// policy does not leak into scenes.
    /// </summary>
    public readonly struct DecorationShowcaseRealization
    {
        public readonly DecorationShowcaseEntry Entry;
        public readonly DecorationShowcaseRealizationKind Kind;
        public readonly DecorationPlacement Decoration;
        public readonly MineCaveDecorationInstance MineCave;
        public readonly NaturalCaveDecorationInstance NaturalCave;
        public readonly WorldObjectResolvedState WorldObject;
        public readonly DecorationBounds Bounds;

        public DecorationRenderBackend DecorationBackend => Kind == DecorationShowcaseRealizationKind.Decoration
            ? Decoration.Backend
            : Kind == DecorationShowcaseRealizationKind.MineCave
                ? MineCave.Backend
                : Kind == DecorationShowcaseRealizationKind.NaturalCave
                    ? NaturalCave.Backend
                    : DecorationRenderBackend.VoxelStamp;

        public bool IsWellFormed
        {
            get
            {
                if (!Entry.IsWellFormed || !Bounds.IsWellFormed) return false;
                switch (Kind)
                {
                    case DecorationShowcaseRealizationKind.Decoration:
                        return Decoration.IsWellFormed;
                    case DecorationShowcaseRealizationKind.MineCave:
                        return MineCave.IsWellFormed;
                    case DecorationShowcaseRealizationKind.NaturalCave:
                        return NaturalCave.IsWellFormed;
                    case DecorationShowcaseRealizationKind.WorldObject:
                        return WorldObject.Descriptor.IsWellFormed;
                    default:
                        return false;
                }
            }
        }

        internal DecorationShowcaseRealization(
            in DecorationShowcaseEntry entry,
            in DecorationPlacement placement)
        {
            Entry = entry;
            Kind = DecorationShowcaseRealizationKind.Decoration;
            Decoration = placement;
            MineCave = default;
            NaturalCave = default;
            WorldObject = default;
            Bounds = placement.Bounds;
        }

        internal DecorationShowcaseRealization(
            in DecorationShowcaseEntry entry,
            in MineCaveDecorationInstance instance)
        {
            Entry = entry;
            Kind = DecorationShowcaseRealizationKind.MineCave;
            Decoration = default;
            MineCave = instance;
            NaturalCave = default;
            WorldObject = default;
            Bounds = instance.Bounds;
        }

        internal DecorationShowcaseRealization(
            in DecorationShowcaseEntry entry,
            in NaturalCaveDecorationInstance instance)
        {
            Entry = entry;
            Kind = DecorationShowcaseRealizationKind.NaturalCave;
            Decoration = default;
            MineCave = default;
            NaturalCave = instance;
            WorldObject = default;
            Bounds = instance.Bounds;
        }

        internal DecorationShowcaseRealization(
            in DecorationShowcaseEntry entry,
            in WorldObjectResolvedState state)
        {
            Entry = entry;
            Kind = DecorationShowcaseRealizationKind.WorldObject;
            Decoration = default;
            MineCave = default;
            NaturalCave = default;
            WorldObject = state;
            Bounds = state.Descriptor.Bounds;
        }
    }

    public static class DecorationShowcaseRealizer
    {
        private const uint PresetSlotBase = 0x10000u;
        private const uint WorldObjectParentSalt = 0x574F5052u; // WOPR

        public static bool TryCreate(
            in DecorationShowcaseEntry entry,
            in DecorationContext context,
            out DecorationShowcaseRealization realization)
        {
            realization = default;
            if (!entry.IsWellFormed || !context.IsWellFormed)
                return false;

            switch (entry.Source)
            {
                case DecorationShowcaseEntrySource.RegisteredDecoration:
                    if (!DecorationShowcaseCatalog.TryDescribeDecoration(
                            in context, entry.SourceId, out DecorationPropDescriptor decoration))
                        return false;
                    return TryCreateDecoration(in entry, in context, entry.SourceId, in decoration, out realization);

                case DecorationShowcaseEntrySource.Preset:
                    var presetKind = (DecorationShowcasePresetKind)entry.SourceId;
                    if (!DecorationShowcaseCatalog.TryDescribePreset(
                            in context, presetKind, out DecorationPropDescriptor preset))
                        return false;
                    return TryCreateDecoration(
                        in entry, in context, PresetSlotBase + entry.SourceId, in preset, out realization);

                case DecorationShowcaseEntrySource.MineCave:
                    if (!DecorationShowcaseCatalog.TryDescribeMineCave(
                            in context, entry.SourceId, out MineCaveDecorationDescriptor mine))
                        return false;
                    var mineBounds = BoundsAtOrigin(mine.Size);
                    var mineInstance = new MineCaveDecorationInstance
                    {
                        Id = GeneratedPropIds.Create(in context, MineCaveDecorationCatalog.SceneId, entry.SourceId),
                        Kind = mine.Kind,
                        Backend = mine.Backend,
                        Interaction = mine.Interaction,
                        Bounds = mineBounds,
                        Facing = mine.Mount == MineCaveMountKind.Floor ? new int3(0, 0, 1) : new int3(0, 0, 1),
                        Variant = mine.Variant,
                    };
                    realization = new DecorationShowcaseRealization(in entry, in mineInstance);
                    return realization.IsWellFormed;

                case DecorationShowcaseEntrySource.NaturalCave:
                    if (!DecorationShowcaseCatalog.TryDescribeNaturalCave(
                            in context, entry.SourceId, out NaturalCaveDecorationDescriptor natural))
                        return false;
                    var naturalBounds = BoundsAtOrigin(natural.Size);
                    var naturalInstance = new NaturalCaveDecorationInstance
                    {
                        Id = GeneratedPropIds.Create(in context, NaturalCaveDecorationCatalog.SceneId, entry.SourceId),
                        Kind = natural.Kind,
                        Backend = natural.Backend,
                        Interaction = natural.Interaction,
                        Bounds = naturalBounds,
                        Facing = natural.CeilingMounted ? new int3(0, -1, 0) : new int3(0, 1, 0),
                        Variant = natural.Variant,
                    };
                    realization = new DecorationShowcaseRealization(in entry, in naturalInstance);
                    return realization.IsWellFormed;

                case DecorationShowcaseEntrySource.WorldObject:
                    if (!DecorationShowcaseCatalog.TryGetWorldObjectPreset(entry.SourceId, out WorldObjectPreset objectPreset))
                        return false;
                    int3 size = WorldObjectCatalogQuery.BaselineSize(objectPreset.Kind);
                    if (math.any(size <= 0))
                        return false;
                    uint parentId = DecorationSeed.Derive(context.StructureId, WorldObjectParentSalt);
                    var objects = new WorldObjectAuthoringSession(context.WorldSeed, parentId);
                    objects.Place(
                        entry.SourceId,
                        objectPreset.Kind,
                        BoundsAtOrigin(size),
                        FacingForWorldObject(objectPreset.Kind));
                    WorldObjectDescriptor[] descriptors = objects.BuildObjects();
                    if (descriptors.Length != 1)
                        return false;
                    WorldObjectDescriptor descriptor = descriptors[0];
                    var store = new WorldObjectStateStore();
                    if ((descriptor.DefaultState & WorldObjectStateFlags.Hidden) != 0)
                    {
                        store.Set(new WorldObjectStateDelta
                        {
                            Id = descriptor.Id,
                            State = descriptor.DefaultState & ~WorldObjectStateFlags.Hidden,
                        });
                    }
                    WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in descriptor, store);
                    realization = new DecorationShowcaseRealization(in entry, in state);
                    return realization.IsWellFormed;

                default:
                    return false;
            }
        }

        private static bool TryCreateDecoration(
            in DecorationShowcaseEntry entry,
            in DecorationContext context,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            out DecorationShowcaseRealization realization)
        {
            realization = default;
            if (!descriptor.IsWellFormed || slotId == 0)
                return false;

            var placement = new DecorationPlacement
            {
                Id = GeneratedPropIds.Create(in context, DecorationShowcaseCatalog.PreviewSceneId, slotId),
                SceneId = DecorationShowcaseCatalog.PreviewSceneId,
                SlotId = slotId,
                Family = descriptor.Family,
                Backend = descriptor.Backend,
                Interaction = descriptor.Interaction,
                Bounds = BoundsAtOrigin(descriptor.Size),
                Facing = FacingForMount(descriptor.MountMode),
                Variant = descriptor.Variant,
            };
            realization = new DecorationShowcaseRealization(in entry, in placement);
            return realization.IsWellFormed;
        }

        private static DecorationBounds BoundsAtOrigin(int3 size) => new DecorationBounds
        {
            Min = int3.zero,
            MaxExclusive = math.max(new int3(1), size),
        };

        private static int3 FacingForMount(DecorationMountMode mount)
        {
            switch (mount)
            {
                case DecorationMountMode.Ceiling:
                    return new int3(0, -1, 0);
                case DecorationMountMode.Wall:
                case DecorationMountMode.FloorAgainstWall:
                case DecorationMountMode.AnchorRelative:
                    return new int3(0, 0, 1);
                default:
                    return new int3(0, 1, 0);
            }
        }

        private static int3 FacingForWorldObject(WorldObjectKind kind)
        {
            switch (kind)
            {
                case WorldObjectKind.Trapdoor:
                case WorldObjectKind.PressurePlate:
                case WorldObjectKind.Trap:
                case WorldObjectKind.SpikeTrap:
                case WorldObjectKind.Teleporter:
                case WorldObjectKind.Checkpoint:
                case WorldObjectKind.SpawnPoint:
                    return new int3(0, 1, 0);
                case WorldObjectKind.FallingBlockTrap:
                    return new int3(0, -1, 0);
                default:
                    return new int3(0, 0, 1);
            }
        }
    }
}
