using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Game-facing cave floor patch discovered by cave/navigation composition. End is the forward
    /// end of the walkable patch; Facing points toward that end. Keeping facing explicit avoids
    /// reconstructing the shared cave generator's private turn state from MainPathEnd.
    /// </summary>
    public struct CaveWalkablePatch
    {
        public uint PatchId;
        public ulong Seed;
        public int3 End;
        public Facing Facing;
        public int Width;
        public int Length;
        public int Height;

        public bool IsWellFormed =>
            PatchId != 0 && Seed != 0 && Width >= 7 && Length >= 7 && Height >= 7 &&
            (Facing == Facing.North || Facing == Facing.East ||
             Facing == Facing.South || Facing == Facing.West);

        public static CaveWalkablePatch AtPathEnd(
            ulong seed,
            int3 mainPathEnd,
            Facing pathFacing,
            in CaveConfig config)
        {
            uint foldedSeed = CaveDecorationSpaceAdapter.FoldSeed(seed);
            int width = math.max(7, config.TunnelWidth - config.WallRoughness * 2);
            int length = math.max(7, math.min(config.SegmentLength, math.max(8, width)));
            int height = math.max(7, config.TunnelHeight - config.CeilingRoughness - 1);
            return new CaveWalkablePatch
            {
                PatchId = DecorationSeed.Derive(foldedSeed, 0xCA7EFA7Cu),
                Seed = seed == 0 ? 1ul : seed,
                End = mainPathEnd,
                Facing = pathFacing,
                Width = width,
                Length = length,
                Height = height,
            };
        }
    }

    public enum CaveDecorationSurfaceKind : byte
    {
        WalkableFloor = 0,
        Wall = 1,
        Ceiling = 2,
        Alcove = 3,
        Ledge = 4,
    }

    public struct CaveDecorationCandidate
    {
        public CaveDecorationSurfaceKind Kind;
        public DecorationSocket Socket;

        public bool IsWellFormed => Socket.IsWellFormed;
    }

    /// <summary>
    /// Adapts a cave-specific walkable patch into the same DecorationSpace, context, socket, and
    /// exclusion vocabulary used by rectangular castle interiors.
    /// </summary>
    public static class CaveDecorationSpaceAdapter
    {
        public static bool TryCreate(
            in CaveWalkablePatch patch,
            out DecorationSpace space,
            out DecorationContext context,
            out CaveDecorationCandidate[] candidates,
            out DecorationExclusion[] exclusions)
        {
            space = default;
            context = default;
            candidates = new CaveDecorationCandidate[0];
            exclusions = new DecorationExclusion[0];
            if (!patch.IsWellFormed)
                return false;

            DecorationBounds bounds = BoundsFor(in patch);
            space = new DecorationSpace
            {
                SpaceId = patch.PatchId,
                Kind = DecorationSpaceKind.CaveChamber,
                Bounds = bounds,
            };
            if (!space.IsWellFormed)
                return false;

            uint worldSeed = FoldSeed(patch.Seed);
            context = new DecorationContext
            {
                WorldSeed = worldSeed,
                StructureId = DecorationSeed.Derive(worldSeed, 0xCA7E0001u),
                SpaceId = patch.PatchId,
                StyleId = DecorationStyleIds.Compose(
                    DecorationStyleFamily.Frontier,
                    DecorationSeed.Derive(worldSeed, patch.PatchId)),
                StructureKind = DecorationStructureKind.Cave,
                SpaceKind = DecorationSpaceKind.CaveChamber,
                Wealth = DecorationWealthTier.Modest,
                Condition = DecorationConditionTier.Worn,
                Environment = DecorationEnvironmentTags.Interior |
                              DecorationEnvironmentTags.Underground |
                              DecorationEnvironmentTags.Damp,
            };

            candidates = CaveDecorationSurfaceAnalyzer.ExtractCandidates(in patch, in space);
            exclusions = CreateExclusions(in patch, in space);
            return context.IsWellFormed && AllCandidatesValid(candidates) && AllExclusionsValid(exclusions);
        }

        public static uint FoldSeed(ulong seed)
        {
            uint folded = (uint)(seed ^ (seed >> 32));
            return folded == 0 ? 0xCA7E5EEDu : folded;
        }

        private static DecorationBounds BoundsFor(in CaveWalkablePatch patch)
        {
            int lateralMin;
            int forwardMin;
            int lateralMax;
            int forwardMax;

            if (patch.Facing == Facing.North || patch.Facing == Facing.South)
            {
                lateralMin = patch.End.x - patch.Width / 2;
                lateralMax = lateralMin + patch.Width;
                if (patch.Facing == Facing.North)
                {
                    forwardMax = patch.End.z + 1;
                    forwardMin = forwardMax - patch.Length;
                }
                else
                {
                    forwardMin = patch.End.z;
                    forwardMax = forwardMin + patch.Length;
                }

                return new DecorationBounds
                {
                    Min = new int3(lateralMin, patch.End.y, forwardMin),
                    MaxExclusive = new int3(lateralMax, patch.End.y + patch.Height, forwardMax),
                };
            }

            lateralMin = patch.End.z - patch.Width / 2;
            lateralMax = lateralMin + patch.Width;
            if (patch.Facing == Facing.East)
            {
                forwardMax = patch.End.x + 1;
                forwardMin = forwardMax - patch.Length;
            }
            else
            {
                forwardMin = patch.End.x;
                forwardMax = forwardMin + patch.Length;
            }

            return new DecorationBounds
            {
                Min = new int3(forwardMin, patch.End.y, lateralMin),
                MaxExclusive = new int3(forwardMax, patch.End.y + patch.Height, lateralMax),
            };
        }

        private static DecorationExclusion[] CreateExclusions(
            in CaveWalkablePatch patch,
            in DecorationSpace space)
        {
            DecorationBounds bounds = space.Bounds;
            int corridorWidth = math.clamp(patch.Width / 5, 2, 4);
            int hazardSize = math.clamp(patch.Width / 6, 2, 4);
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;

            DecorationBounds navigation;
            if (patch.Facing == Facing.North || patch.Facing == Facing.South)
            {
                navigation = new DecorationBounds
                {
                    Min = new int3(centerX - corridorWidth / 2, bounds.Min.y, bounds.Min.z),
                    MaxExclusive = new int3(centerX - corridorWidth / 2 + corridorWidth,
                        bounds.Min.y + math.min(5, bounds.Size.y), bounds.MaxExclusive.z),
                };
            }
            else
            {
                navigation = new DecorationBounds
                {
                    Min = new int3(bounds.Min.x, bounds.Min.y, centerZ - corridorWidth / 2),
                    MaxExclusive = new int3(bounds.MaxExclusive.x,
                        bounds.Min.y + math.min(5, bounds.Size.y),
                        centerZ - corridorWidth / 2 + corridorWidth),
                };
            }

            uint hazardSeed = DecorationSeed.Derive(FoldSeed(patch.Seed), patch.PatchId ^ 0xA2A2A2A2u);
            bool highSide = (hazardSeed & 1u) != 0;
            int hazardX = highSide
                ? bounds.MaxExclusive.x - hazardSize - 1
                : bounds.Min.x + 1;
            int hazardZ = ((hazardSeed >> 1) & 1u) != 0
                ? bounds.MaxExclusive.z - hazardSize - 1
                : bounds.Min.z + 1;
            var hazard = new DecorationBounds
            {
                Min = new int3(hazardX, bounds.Min.y, hazardZ),
                MaxExclusive = new int3(hazardX + hazardSize,
                    bounds.Min.y + math.min(4, bounds.Size.y),
                    hazardZ + hazardSize),
            };

            return new[]
            {
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Navigation,
                    Bounds = navigation,
                },
                new DecorationExclusion
                {
                    Kind = DecorationExclusionKind.Hazard,
                    Bounds = hazard,
                },
            };
        }

        private static bool AllCandidatesValid(CaveDecorationCandidate[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return false;
            for (int i = 0; i < candidates.Length; i++)
                if (!candidates[i].IsWellFormed)
                    return false;
            return true;
        }

        private static bool AllExclusionsValid(DecorationExclusion[] exclusions)
        {
            if (exclusions == null)
                return false;
            for (int i = 0; i < exclusions.Length; i++)
                if (!exclusions[i].IsWellFormed)
                    return false;
            return true;
        }
    }

    public static class CaveDecorationSurfaceAnalyzer
    {
        public static CaveDecorationCandidate[] ExtractCandidates(
            in CaveWalkablePatch patch,
            in DecorationSpace space)
        {
            if (!patch.IsWellFormed || !space.IsWellFormed)
                return new CaveDecorationCandidate[0];

            DecorationBounds bounds = space.Bounds;
            int alcoveSize = math.clamp(math.min(bounds.Size.x, bounds.Size.z) / 4, 2, 5);
            int ledgeLength = math.clamp(math.max(bounds.Size.x, bounds.Size.z) / 3, 3, 8);
            bool northSouth = patch.Facing == Facing.North || patch.Facing == Facing.South;
            bool highSide = (FoldSeed(patch.Seed) & 1u) != 0;

            var candidates = new CaveDecorationCandidate[9];
            candidates[0] = Candidate(CaveDecorationSurfaceKind.WalkableFloor, 1,
                DecorationSocketKind.Floor,
                new DecorationBounds
                {
                    Min = bounds.Min,
                    MaxExclusive = new int3(bounds.MaxExclusive.x, bounds.Min.y + 1, bounds.MaxExclusive.z),
                }, new int3(0, 1, 0));
            candidates[1] = Candidate(CaveDecorationSurfaceKind.Wall, 2,
                DecorationSocketKind.Wall,
                new DecorationBounds { Min = bounds.Min, MaxExclusive = new int3(bounds.Min.x + 1, bounds.MaxExclusive.y, bounds.MaxExclusive.z) },
                new int3(1, 0, 0));
            candidates[2] = Candidate(CaveDecorationSurfaceKind.Wall, 3,
                DecorationSocketKind.Wall,
                new DecorationBounds { Min = new int3(bounds.MaxExclusive.x - 1, bounds.Min.y, bounds.Min.z), MaxExclusive = bounds.MaxExclusive },
                new int3(-1, 0, 0));
            candidates[3] = Candidate(CaveDecorationSurfaceKind.Wall, 4,
                DecorationSocketKind.Wall,
                new DecorationBounds { Min = bounds.Min, MaxExclusive = new int3(bounds.MaxExclusive.x, bounds.MaxExclusive.y, bounds.Min.z + 1) },
                new int3(0, 0, 1));
            candidates[4] = Candidate(CaveDecorationSurfaceKind.Wall, 5,
                DecorationSocketKind.Wall,
                new DecorationBounds { Min = new int3(bounds.Min.x, bounds.Min.y, bounds.MaxExclusive.z - 1), MaxExclusive = bounds.MaxExclusive },
                new int3(0, 0, -1));
            candidates[5] = Candidate(CaveDecorationSurfaceKind.Ceiling, 6,
                DecorationSocketKind.Ceiling,
                new DecorationBounds { Min = new int3(bounds.Min.x, bounds.MaxExclusive.y - 1, bounds.Min.z), MaxExclusive = bounds.MaxExclusive },
                new int3(0, -1, 0));

            int alcoveX = highSide ? bounds.MaxExclusive.x - alcoveSize : bounds.Min.x;
            int alcoveZ = northSouth
                ? (patch.Facing == Facing.North ? bounds.Min.z : bounds.MaxExclusive.z - alcoveSize)
                : (highSide ? bounds.Min.z : bounds.MaxExclusive.z - alcoveSize);
            int3 alcoveFacing = alcoveX == bounds.Min.x ? new int3(1, 0, 0) : new int3(-1, 0, 0);
            candidates[6] = Candidate(CaveDecorationSurfaceKind.Alcove, 7,
                DecorationSocketKind.Corner,
                new DecorationBounds
                {
                    Min = new int3(alcoveX, bounds.Min.y, alcoveZ),
                    MaxExclusive = new int3(alcoveX + alcoveSize,
                        bounds.Min.y + math.min(bounds.Size.y, 7), alcoveZ + alcoveSize),
                }, alcoveFacing);

            DecorationBounds lowLedge;
            DecorationBounds highLedge;
            if (northSouth)
            {
                int minZ = center(bounds.Min.z, bounds.MaxExclusive.z, ledgeLength);
                lowLedge = new DecorationBounds
                {
                    Min = new int3(bounds.Min.x, bounds.Min.y + 2, minZ),
                    MaxExclusive = new int3(bounds.Min.x + 2, bounds.Min.y + 3, minZ + ledgeLength),
                };
                highLedge = new DecorationBounds
                {
                    Min = new int3(bounds.MaxExclusive.x - 2, bounds.Min.y + 3, minZ),
                    MaxExclusive = new int3(bounds.MaxExclusive.x, bounds.Min.y + 4, minZ + ledgeLength),
                };
            }
            else
            {
                int minX = center(bounds.Min.x, bounds.MaxExclusive.x, ledgeLength);
                lowLedge = new DecorationBounds
                {
                    Min = new int3(minX, bounds.Min.y + 2, bounds.Min.z),
                    MaxExclusive = new int3(minX + ledgeLength, bounds.Min.y + 3, bounds.Min.z + 2),
                };
                highLedge = new DecorationBounds
                {
                    Min = new int3(minX, bounds.Min.y + 3, bounds.MaxExclusive.z - 2),
                    MaxExclusive = new int3(minX + ledgeLength, bounds.Min.y + 4, bounds.MaxExclusive.z),
                };
            }

            candidates[7] = Candidate(CaveDecorationSurfaceKind.Ledge, 8,
                DecorationSocketKind.Floor, lowLedge, new int3(0, 1, 0));
            candidates[8] = Candidate(CaveDecorationSurfaceKind.Ledge, 9,
                DecorationSocketKind.Floor, highLedge, new int3(0, 1, 0));
            return candidates;
        }

        public static DecorationSocket[] PlacementSockets(CaveDecorationCandidate[] candidates)
        {
            if (candidates == null)
                return new DecorationSocket[0];

            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                CaveDecorationSurfaceKind kind = candidates[i].Kind;
                if (kind == CaveDecorationSurfaceKind.WalkableFloor ||
                    kind == CaveDecorationSurfaceKind.Wall ||
                    kind == CaveDecorationSurfaceKind.Ceiling)
                    count++;
            }

            var sockets = new DecorationSocket[count];
            int output = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                CaveDecorationSurfaceKind kind = candidates[i].Kind;
                if (kind == CaveDecorationSurfaceKind.WalkableFloor ||
                    kind == CaveDecorationSurfaceKind.Wall ||
                    kind == CaveDecorationSurfaceKind.Ceiling)
                    sockets[output++] = candidates[i].Socket;
            }
            return sockets;
        }

        private static CaveDecorationCandidate Candidate(
            CaveDecorationSurfaceKind kind,
            uint socketId,
            DecorationSocketKind socketKind,
            DecorationBounds bounds,
            int3 facing) => new CaveDecorationCandidate
        {
            Kind = kind,
            Socket = new DecorationSocket
            {
                SocketId = socketId,
                Kind = socketKind,
                Bounds = bounds,
                Facing = facing,
            },
        };

        private static int center(int min, int maxExclusive, int length)
        {
            return (min + maxExclusive - length) / 2;
        }

        private static uint FoldSeed(ulong seed) => CaveDecorationSpaceAdapter.FoldSeed(seed);
    }
}
