using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Derives deterministic semantic sockets from a rectangular usable interior volume.</summary>
    public static class RectangularDecorationSpaceAnalyzer
    {
        public static DecorationSocket[] ExtractSockets(in DecorationSpace space)
        {
            if (!space.IsWellFormed)
                return new DecorationSocket[0];

            int3 min = space.Bounds.Min;
            int3 max = space.Bounds.MaxExclusive;
            return new[]
            {
                Socket(1, DecorationSocketKind.Floor,
                    new DecorationBounds { Min = min, MaxExclusive = new int3(max.x, min.y + 1, max.z) }, new int3(0, 1, 0)),
                Socket(2, DecorationSocketKind.Wall,
                    new DecorationBounds { Min = min, MaxExclusive = new int3(min.x + 1, max.y, max.z) }, new int3(1, 0, 0)),
                Socket(3, DecorationSocketKind.Wall,
                    new DecorationBounds { Min = new int3(max.x - 1, min.y, min.z), MaxExclusive = max }, new int3(-1, 0, 0)),
                Socket(4, DecorationSocketKind.Wall,
                    new DecorationBounds { Min = min, MaxExclusive = new int3(max.x, max.y, min.z + 1) }, new int3(0, 0, 1)),
                Socket(5, DecorationSocketKind.Wall,
                    new DecorationBounds { Min = new int3(min.x, min.y, max.z - 1), MaxExclusive = max }, new int3(0, 0, -1)),
                Socket(6, DecorationSocketKind.Corner,
                    new DecorationBounds { Min = min, MaxExclusive = new int3(min.x + 1, max.y, min.z + 1) }, new int3(1, 0, 0)),
                Socket(7, DecorationSocketKind.Corner,
                    new DecorationBounds { Min = new int3(max.x - 1, min.y, min.z), MaxExclusive = new int3(max.x, max.y, min.z + 1) }, new int3(-1, 0, 0)),
                Socket(8, DecorationSocketKind.Corner,
                    new DecorationBounds { Min = new int3(min.x, min.y, max.z - 1), MaxExclusive = new int3(min.x + 1, max.y, max.z) }, new int3(1, 0, 0)),
                Socket(9, DecorationSocketKind.Corner,
                    new DecorationBounds { Min = new int3(max.x - 1, min.y, max.z - 1), MaxExclusive = max }, new int3(-1, 0, 0)),
                Socket(10, DecorationSocketKind.Ceiling,
                    new DecorationBounds { Min = new int3(min.x, max.y - 1, min.z), MaxExclusive = max }, new int3(0, -1, 0)),
            };
        }

        private static DecorationSocket Socket(uint id, DecorationSocketKind kind, DecorationBounds bounds, int3 facing) =>
            new DecorationSocket
            {
                SocketId = id,
                Kind = kind,
                Bounds = bounds,
                Facing = facing,
            };
    }

    public static class DecorationPlacementResolver
    {
        public static bool TryPlace(
            in DecorationSpace space,
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            DecorationSocket[] sockets,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            placement = default;
            if (!space.IsWellFormed || !context.IsWellFormed || !descriptor.IsWellFormed || sockets == null)
                return false;

            uint seed = DecorationSeed.ForSlot(in context, sceneId, slotId);
            int start = sockets.Length == 0 ? 0 : (int)(seed % (uint)sockets.Length);

            for (int pass = 0; pass < sockets.Length; pass++)
            {
                DecorationSocket socket = sockets[(start + pass) % sockets.Length];
                if (!socket.IsWellFormed || !descriptor.Accepts(socket.Kind))
                    continue;

                for (int attempt = 0; attempt < 4; attempt++)
                {
                    uint attemptSeed = DecorationSeed.Derive(seed, (uint)(pass * 17 + attempt + 1));
                    if (!TryBuildBounds(in space, in socket, in descriptor, attemptSeed, out DecorationBounds bounds, out int3 facing))
                        continue;
                    if (!IsCandidateValid(in space, in descriptor, in bounds, exclusions, occupied, occupiedCount))
                        continue;

                    placement = BuildPlacement(in context, sceneId, slotId, 0, socket.SocketId,
                        in descriptor, in bounds, facing);
                    return true;
                }
            }

            return false;
        }

        public static bool TryPlaceRugRelativeToAnchor(
            in DecorationSpace space,
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            in DecorationPlacement anchor,
            DecorationExclusion[] exclusions,
            out DecorationPlacement placement)
        {
            placement = default;
            if (descriptor.Family != DecorationPropFamily.Rug || !anchor.IsWellFormed)
                return false;

            int width = descriptor.Size.x;
            int depth = descriptor.Size.z;
            int centerX = (anchor.Bounds.Min.x + anchor.Bounds.MaxExclusive.x) / 2;
            int centerZ = (anchor.Bounds.Min.z + anchor.Bounds.MaxExclusive.z) / 2;
            int minX = centerX - width / 2;
            int minZ = centerZ - depth / 2;

            minX = math.clamp(minX, space.Bounds.Min.x, space.Bounds.MaxExclusive.x - width);
            minZ = math.clamp(minZ, space.Bounds.Min.z, space.Bounds.MaxExclusive.z - depth);
            var bounds = new DecorationBounds
            {
                Min = new int3(minX, space.Bounds.Min.y, minZ),
                MaxExclusive = new int3(minX + width, space.Bounds.Min.y + 1, minZ + depth),
            };

            if (!space.Bounds.Contains(in bounds) || IntersectsExclusions(in bounds, descriptor.Clearance, exclusions))
                return false;

            placement = BuildPlacement(in context, sceneId, slotId, anchor.SlotId, 0,
                in descriptor, in bounds, anchor.Facing);
            return true;
        }

        public static bool TryPlaceAboveAnchor(
            in DecorationSpace space,
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            in DecorationPlacement anchor,
            DecorationExclusion[] exclusions,
            out DecorationPlacement placement)
        {
            placement = default;
            if (!anchor.IsWellFormed || descriptor.MountMode != DecorationMountMode.Wall)
                return false;

            int3 facing = anchor.Facing;
            int width = descriptor.Size.x;
            int height = descriptor.Size.y;
            int depth = descriptor.Size.z;
            int centerX = (anchor.Bounds.Min.x + anchor.Bounds.MaxExclusive.x) / 2;
            int centerZ = (anchor.Bounds.Min.z + anchor.Bounds.MaxExclusive.z) / 2;
            int y = anchor.Bounds.MaxExclusive.y + 4;
            DecorationBounds bounds;

            if (math.abs(facing.x) == 1)
            {
                int minX = facing.x > 0 ? space.Bounds.Min.x : space.Bounds.MaxExclusive.x - depth;
                bounds = new DecorationBounds
                {
                    Min = new int3(minX, y, centerZ - width / 2),
                    MaxExclusive = new int3(minX + depth, y + height, centerZ - width / 2 + width),
                };
            }
            else
            {
                int minZ = facing.z > 0 ? space.Bounds.Min.z : space.Bounds.MaxExclusive.z - depth;
                bounds = new DecorationBounds
                {
                    Min = new int3(centerX - width / 2, y, minZ),
                    MaxExclusive = new int3(centerX - width / 2 + width, y + height, minZ + depth),
                };
            }

            if (!space.Bounds.Contains(in bounds) || IntersectsExclusions(in bounds, descriptor.Clearance, exclusions))
                return false;

            placement = BuildPlacement(in context, sceneId, slotId, anchor.SlotId, anchor.SocketId,
                in descriptor, in bounds, facing);
            return true;
        }

        private static bool TryBuildBounds(
            in DecorationSpace space,
            in DecorationSocket socket,
            in DecorationPropDescriptor descriptor,
            uint seed,
            out DecorationBounds bounds,
            out int3 facing)
        {
            bounds = default;
            facing = socket.Facing;

            switch (descriptor.MountMode)
            {
                case DecorationMountMode.Floor:
                    return TryBuildFloorBounds(in space, in descriptor, seed, out bounds);
                case DecorationMountMode.FloorAgainstWall:
                    return TryBuildWallBounds(in space, in socket, in descriptor, seed, false, out bounds);
                case DecorationMountMode.Wall:
                    return TryBuildWallBounds(in space, in socket, in descriptor, seed, true, out bounds);
                case DecorationMountMode.Ceiling:
                    return TryBuildCeilingBounds(in space, in descriptor, seed, out bounds);
                default:
                    return false;
            }
        }

        private static bool TryBuildFloorBounds(
            in DecorationSpace space,
            in DecorationPropDescriptor descriptor,
            uint seed,
            out DecorationBounds bounds)
        {
            bounds = default;
            int widthRoom = space.Bounds.Size.x;
            int depthRoom = space.Bounds.Size.z;
            if (descriptor.Size.x > widthRoom || descriptor.Size.z > depthRoom || descriptor.Size.y > space.Bounds.Size.y)
                return false;

            int availableX = widthRoom - descriptor.Size.x;
            int availableZ = depthRoom - descriptor.Size.z;
            int offsetX = availableX == 0 ? 0 : (int)(seed % (uint)(availableX + 1));
            int offsetZ = availableZ == 0 ? 0 : (int)(DecorationSeed.Derive(seed, 31) % (uint)(availableZ + 1));
            int3 min = new int3(space.Bounds.Min.x + offsetX, space.Bounds.Min.y, space.Bounds.Min.z + offsetZ);
            bounds = new DecorationBounds { Min = min, MaxExclusive = min + descriptor.Size };
            return true;
        }

        private static bool TryBuildCeilingBounds(
            in DecorationSpace space,
            in DecorationPropDescriptor descriptor,
            uint seed,
            out DecorationBounds bounds)
        {
            if (!TryBuildFloorBounds(in space, in descriptor, seed, out bounds))
                return false;
            int height = descriptor.Size.y;
            bounds.Min.y = space.Bounds.MaxExclusive.y - height;
            bounds.MaxExclusive.y = space.Bounds.MaxExclusive.y;
            return true;
        }

        private static bool TryBuildWallBounds(
            in DecorationSpace space,
            in DecorationSocket socket,
            in DecorationPropDescriptor descriptor,
            uint seed,
            bool mounted,
            out DecorationBounds bounds)
        {
            bounds = default;
            int width = descriptor.Size.x;
            int height = descriptor.Size.y;
            int depth = descriptor.Size.z;
            int roomHeight = space.Bounds.Size.y;
            if (height > roomHeight)
                return false;

            int y = mounted
                ? math.clamp(space.Bounds.Min.y + (roomHeight * 2 / 3) - height / 2,
                    space.Bounds.Min.y, space.Bounds.MaxExclusive.y - height)
                : space.Bounds.Min.y;

            if (math.abs(socket.Facing.x) == 1)
            {
                int wallSpan = space.Bounds.Size.z;
                if (width > wallSpan || depth > space.Bounds.Size.x)
                    return false;
                int available = wallSpan - width;
                int offset = available == 0 ? 0 : (int)(seed % (uint)(available + 1));
                int minX = socket.Facing.x > 0 ? space.Bounds.Min.x : space.Bounds.MaxExclusive.x - depth;
                int minZ = space.Bounds.Min.z + offset;
                bounds = new DecorationBounds
                {
                    Min = new int3(minX, y, minZ),
                    MaxExclusive = new int3(minX + depth, y + height, minZ + width),
                };
                return true;
            }

            if (math.abs(socket.Facing.z) == 1)
            {
                int wallSpan = space.Bounds.Size.x;
                if (width > wallSpan || depth > space.Bounds.Size.z)
                    return false;
                int available = wallSpan - width;
                int offset = available == 0 ? 0 : (int)(seed % (uint)(available + 1));
                int minX = space.Bounds.Min.x + offset;
                int minZ = socket.Facing.z > 0 ? space.Bounds.Min.z : space.Bounds.MaxExclusive.z - depth;
                bounds = new DecorationBounds
                {
                    Min = new int3(minX, y, minZ),
                    MaxExclusive = new int3(minX + width, y + height, minZ + depth),
                };
                return true;
            }

            return false;
        }

        private static bool IsCandidateValid(
            in DecorationSpace space,
            in DecorationPropDescriptor descriptor,
            in DecorationBounds bounds,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] occupied,
            int occupiedCount)
        {
            if (!space.Bounds.Contains(in bounds))
                return false;
            if (IntersectsExclusions(in bounds, descriptor.Clearance, exclusions))
                return false;

            DecorationBounds expanded = bounds.Expanded(descriptor.Clearance);
            int count = occupied == null ? 0 : math.min(occupiedCount, occupied.Length);
            for (int i = 0; i < count; i++)
            {
                if (occupied[i].Bounds.IsWellFormed && expanded.Overlaps(in occupied[i].Bounds))
                    return false;
            }
            return true;
        }

        private static bool IntersectsExclusions(
            in DecorationBounds bounds,
            int3 clearance,
            DecorationExclusion[] exclusions)
        {
            if (exclusions == null)
                return false;
            DecorationBounds expanded = bounds.Expanded(clearance);
            for (int i = 0; i < exclusions.Length; i++)
            {
                if (exclusions[i].IsWellFormed && expanded.Overlaps(in exclusions[i].Bounds))
                    return true;
            }
            return false;
        }

        private static DecorationPlacement BuildPlacement(
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            uint anchorSlotId,
            uint socketId,
            in DecorationPropDescriptor descriptor,
            in DecorationBounds bounds,
            int3 facing) =>
            new DecorationPlacement
            {
                Id = GeneratedPropIds.Create(in context, sceneId, slotId),
                SceneId = sceneId,
                SlotId = slotId,
                AnchorSlotId = anchorSlotId,
                SocketId = socketId,
                Family = descriptor.Family,
                Backend = descriptor.Backend,
                Interaction = descriptor.Interaction,
                Bounds = bounds,
                Facing = facing,
                Variant = descriptor.Variant,
            };
    }

    /// <summary>First relational decoration scene: bed anchors rug, dresser anchors painting, torch fills remaining wall space.</summary>
    public static class BedroomSceneResolver
    {
        public const int PlacementCount = 5;

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.Kind != DecorationSpaceKind.Bedroom ||
                context.SpaceKind != DecorationSpaceKind.Bedroom)
                return false;

            DecorationSceneSlot[] slots = BedroomSceneDefinition.CreateSlots();
            if (!DecorationValidation.ValidateScene(slots, out _))
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[PlacementCount];

            DecorationPropDescriptor bed = DecorationPropPresets.Bed(in context);
            if (!DecorationPlacementResolver.TryPlace(in space, in context, BedroomSceneDefinition.SceneId,
                BedroomSceneDefinition.BedSlot, in bed, sockets, exclusions, resolved, 0, out resolved[0]))
                return false;

            DecorationPropDescriptor rug = DecorationPropPresets.Rug(in context);
            if (!DecorationPlacementResolver.TryPlaceRugRelativeToAnchor(in space, in context, BedroomSceneDefinition.SceneId,
                BedroomSceneDefinition.RugSlot, in rug, in resolved[0], exclusions, out resolved[1]))
                return false;

            DecorationPropDescriptor dresser = DecorationPropPresets.Dresser(in context);
            if (!DecorationPlacementResolver.TryPlace(in space, in context, BedroomSceneDefinition.SceneId,
                BedroomSceneDefinition.DresserSlot, in dresser, sockets, exclusions, resolved, 2, out resolved[2]))
                return false;

            DecorationPropDescriptor painting = DecorationPropPresets.Painting(in context);
            if (!DecorationPlacementResolver.TryPlaceAboveAnchor(in space, in context, BedroomSceneDefinition.SceneId,
                BedroomSceneDefinition.PaintingSlot, in painting, in resolved[2], exclusions, out resolved[3]))
            {
                if (!DecorationPlacementResolver.TryPlace(in space, in context, BedroomSceneDefinition.SceneId,
                    BedroomSceneDefinition.PaintingSlot, in painting, sockets, exclusions, resolved, 3, out resolved[3]))
                    return false;
            }

            DecorationPropDescriptor torch = DecorationPropPresets.WallTorch(in context);
            if (!DecorationPlacementResolver.TryPlace(in space, in context, BedroomSceneDefinition.SceneId,
                BedroomSceneDefinition.TorchSlot, in torch, sockets, exclusions, resolved, 4, out resolved[4]))
                return false;

            placements = resolved;
            return true;
        }
    }
}
