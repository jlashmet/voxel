using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    /// <summary>
    /// Semantic capabilities exposed by a generated world-art socket. These tags are intentionally
    /// gameplay-facing: generation can solve attachment constraints without knowing the concrete
    /// prefab/component that will ultimately occupy the socket.
    /// </summary>
    [Flags]
    public enum WorldArtVoxelSocketTags
    {
        None = 0,
        Structural = 1 << 0,
        WallContinuation = 1 << 1,
        Traversal = 1 << 2,
        Bridge = 1 << 3,
        Decor = 1 << 4,
        Vine = 1 << 5,
        Npc = 1 << 6,
        Cutscene = 1 << 7,
        Rubble = 1 << 8
    }

    public enum WorldArtVoxelSocketRole
    {
        Passage,
        Support,
        Continuation,
        Attachment,
        Landmark,
        Debris
    }

    /// <summary>
    /// Engine-side socket metadata for destructible voxel components. Position/orientation are
    /// integer world-voxel coordinates so sockets are deterministic and network-friendly. The
    /// renderer never owns these values.
    ///
    /// ClearanceVoxels is the minimum empty/usable box an attachment solver should reserve.
    /// SupportProbeRadius tells runtime validation how far around Position to look for supporting
    /// solid voxels. If InvalidateWhenSupportLost is true, destruction can disable the socket once
    /// that local support disappears instead of leaving a floating gameplay anchor behind.
    /// </summary>
    public struct WorldArtVoxelSocket
    {
        public string Name;
        public WorldArtVoxelSocketRole Role;
        public WorldArtVoxelSocketTags Tags;
        public int3 Position;
        public int3 Forward;
        public int3 Up;
        public int3 ClearanceVoxels;
        public int SupportProbeRadius;
        public byte Capacity;
        public bool RequiredAttachment;
        public bool InvalidateWhenSupportLost;

        public bool Supports(WorldArtVoxelSocketTags requiredTags) =>
            (Tags & requiredTags) == requiredTags;
    }

    /// <summary>
    /// Typed semantic sockets emitted by the reusable voxel arch component. The existing named
    /// WorldArtVoxelArchSockets remain the compact geometry result; this layer gives procedural
    /// generation, vines, traversal, NPC placement and cutscenes constraints they can reason about.
    /// </summary>
    public static class WorldArtVoxelArchSocketLibrary
    {
        public static WorldArtVoxelSocket[] Build(in WorldArtVoxelArchSpec spec,
                                                   in WorldArtVoxelArchSockets sockets)
        {
            int halfOpening = math.max(4, spec.HalfOpening);
            int outerRadius = halfOpening + math.max(3, spec.RingThickness);
            int depth = math.max(4, spec.Depth);
            int frontZ = spec.BaseCentre.z - depth / 2 - 1;
            int springY = spec.BaseCentre.y + math.max(8, spec.PierHeight);
            int haunchX = math.max(3, outerRadius * 3 / 5);
            int haunchY = math.max(3, outerRadius * 4 / 5);

            return new[]
            {
                Socket("opening", WorldArtVoxelSocketRole.Passage,
                    WorldArtVoxelSocketTags.Traversal | WorldArtVoxelSocketTags.Npc |
                    WorldArtVoxelSocketTags.Cutscene,
                    sockets.Opening, new int3(0, 0, -1), new int3(0, 1, 0),
                    new int3(halfOpening * 2 - 2, math.max(8, halfOpening), depth),
                    2, 1, false, true),

                Socket("crown", WorldArtVoxelSocketRole.Landmark,
                    WorldArtVoxelSocketTags.Decor | WorldArtVoxelSocketTags.Vine |
                    WorldArtVoxelSocketTags.Cutscene,
                    sockets.Crown, new int3(0, 0, -1), new int3(0, 1, 0),
                    new int3(5, 5, 5), 2, 2, false, true),

                Socket("left-base", WorldArtVoxelSocketRole.Support,
                    WorldArtVoxelSocketTags.Structural,
                    sockets.LeftBase, new int3(0, -1, 0), new int3(0, 0, -1),
                    new int3(math.max(4, spec.PierWidth), 3, depth),
                    3, 1, false, true),

                Socket("right-base", WorldArtVoxelSocketRole.Support,
                    WorldArtVoxelSocketTags.Structural,
                    sockets.RightBase, new int3(0, -1, 0), new int3(0, 0, -1),
                    new int3(math.max(4, spec.PierWidth), 3, depth),
                    3, 1, false, true),

                Socket("wall-left", WorldArtVoxelSocketRole.Continuation,
                    WorldArtVoxelSocketTags.Structural | WorldArtVoxelSocketTags.WallContinuation,
                    sockets.WallLeft, new int3(-1, 0, 0), new int3(0, 1, 0),
                    new int3(math.max(4, spec.PierWidth), math.max(8, spec.PierHeight), depth),
                    3, 1, false, true),

                Socket("wall-right", WorldArtVoxelSocketRole.Continuation,
                    WorldArtVoxelSocketTags.Structural | WorldArtVoxelSocketTags.WallContinuation,
                    sockets.WallRight, new int3(1, 0, 0), new int3(0, 1, 0),
                    new int3(math.max(4, spec.PierWidth), math.max(8, spec.PierHeight), depth),
                    3, 1, false, true),

                Socket("ledge-top", WorldArtVoxelSocketRole.Attachment,
                    WorldArtVoxelSocketTags.Decor | WorldArtVoxelSocketTags.Vine |
                    WorldArtVoxelSocketTags.Traversal,
                    sockets.LedgeTop, new int3(0, 1, 0), new int3(0, 0, -1),
                    new int3(7, 5, 7), 2, 3, false, true),

                Socket("bridge-out", WorldArtVoxelSocketRole.Attachment,
                    WorldArtVoxelSocketTags.Bridge | WorldArtVoxelSocketTags.Traversal,
                    sockets.Opening, new int3(0, 0, -1), new int3(0, 1, 0),
                    new int3(halfOpening * 2 - 4, 6, depth + 6),
                    3, 1, false, true),

                Socket("vine-left-haunch", WorldArtVoxelSocketRole.Attachment,
                    WorldArtVoxelSocketTags.Decor | WorldArtVoxelSocketTags.Vine,
                    new int3(spec.BaseCentre.x - haunchX, springY + haunchY, frontZ),
                    new int3(0, 0, -1), new int3(0, 1, 0),
                    new int3(4, 5, 4), 2, 2, false, true),

                Socket("vine-right-haunch", WorldArtVoxelSocketRole.Attachment,
                    WorldArtVoxelSocketTags.Decor | WorldArtVoxelSocketTags.Vine,
                    new int3(spec.BaseCentre.x + haunchX, springY + haunchY, frontZ),
                    new int3(0, 0, -1), new int3(0, 1, 0),
                    new int3(4, 5, 4), 2, 2, false, true),

                Socket("rubble-base", WorldArtVoxelSocketRole.Debris,
                    WorldArtVoxelSocketTags.Rubble | WorldArtVoxelSocketTags.Decor,
                    sockets.RubbleBase, new int3(0, 1, 0), new int3(0, 0, -1),
                    new int3(10, 4, 8), 2, 4, false, true)
            };
        }

        private static WorldArtVoxelSocket Socket(string name, WorldArtVoxelSocketRole role,
            WorldArtVoxelSocketTags tags, int3 position, int3 forward, int3 up,
            int3 clearance, int supportRadius, byte capacity, bool required,
            bool invalidateWhenSupportLost)
        {
            return new WorldArtVoxelSocket
            {
                Name = name,
                Role = role,
                Tags = tags,
                Position = position,
                Forward = forward,
                Up = up,
                ClearanceVoxels = math.max(clearance, new int3(1)),
                SupportProbeRadius = math.max(0, supportRadius),
                Capacity = math.max((byte)1, capacity),
                RequiredAttachment = required,
                InvalidateWhenSupportLost = invalidateWhenSupportLost
            };
        }
    }
}
