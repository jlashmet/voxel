using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Authored dimensions for a side pocket hidden behind retained cave rock.</summary>
    public struct CaveSecretPocketConfig
    {
        public int BarrierThickness;
        public int EntranceWidth;
        public int EntranceHeight;
        public int ConnectorLength;
        public int PocketWidth;
        public int PocketHeight;
        public int PocketDepth;

        public bool IsWellFormed =>
            BarrierThickness > 0 && EntranceWidth > 0 && EntranceHeight > 0 &&
            ConnectorLength > 0 && PocketWidth >= EntranceWidth &&
            PocketHeight >= EntranceHeight && PocketDepth > 0;

        public static CaveSecretPocketConfig Default => new CaveSecretPocketConfig
        {
            BarrierThickness = 2,
            EntranceWidth = 5,
            EntranceHeight = 7,
            ConnectorLength = 4,
            PocketWidth = 11,
            PocketHeight = 9,
            PocketDepth = 11,
        };
    }

    /// <summary>
    /// Physical proof produced only after a solid-rock preflight succeeds. Barrier is intentionally
    /// retained; Connector and Pocket are the only carved volumes. The one-voxel solid envelope around
    /// the future hidden volume proves there was no pre-existing side route before authoring.
    ///
    /// Construction is internal on purpose: geometry-shaped data is not itself proof. Only
    /// CaveSecretPocketAuthoring may mint a verified pocket after completing the physical preflight.
    /// </summary>
    public readonly struct CaveSecretPocket
    {
        private readonly bool _verified;

        public CaveTraversalCandidate Terminal { get; }
        public DecorationBounds Barrier { get; }
        public DecorationBounds Connector { get; }
        public DecorationBounds Pocket { get; }

        internal CaveSecretPocket(
            in CaveTraversalCandidate terminal,
            in DecorationBounds barrier,
            in DecorationBounds connector,
            in DecorationBounds pocket)
        {
            Terminal = terminal;
            Barrier = barrier;
            Connector = connector;
            Pocket = pocket;
            _verified = true;
        }

        public bool IsWellFormed =>
            _verified && Terminal.IsWellFormed && Barrier.IsWellFormed &&
            Connector.IsWellFormed && Pocket.IsWellFormed &&
            !Barrier.Overlaps(in Connector) && !Barrier.Overlaps(in Pocket);

        // These mirror the existing WorldBuilder secret-topology proofs without creating a competing
        // entrance taxonomy. A composition adapter may expose a verified pocket as DestroyableFalseWall.
        public bool SeparatesHiddenSpaceBeforeOpen => IsWellFormed;
        public bool GrantsNormalTraversalAfterOpen => IsWellFormed;
        public bool SupportsDestruction => IsWellFormed;
        public bool CanMatchHostSurface => IsWellFormed;
        public bool IsStructurallyCritical => false;
    }

    public static class CaveSecretPocketAuthoring
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in CaveTraversalCandidate terminal,
            in CaveSecretPocketConfig config,
            out CaveSecretPocket secret)
        {
            secret = default;
            if (authoring == null || !terminal.IsWellFormed || !config.IsWellFormed || authoring.BudgetExceeded)
                return false;

            DecorationBounds barrier = OrientedBounds(
                terminal.Position, terminal.ExitFacing, 1,
                config.EntranceWidth, config.EntranceHeight, config.BarrierThickness);
            DecorationBounds connector = OrientedBounds(
                terminal.Position, terminal.ExitFacing, 1 + config.BarrierThickness,
                config.EntranceWidth, config.EntranceHeight, config.ConnectorLength);
            DecorationBounds pocket = OrientedBounds(
                terminal.Position, terminal.ExitFacing,
                1 + config.BarrierThickness + config.ConnectorLength,
                config.PocketWidth, config.PocketHeight, config.PocketDepth);

            if (!barrier.IsWellFormed || !connector.IsWellFormed || !pocket.IsWellFormed)
                return false;

            long writes = Volume(in connector) + Volume(in pocket);
            long remaining = (long)authoring.WriteBudget - authoring.TotalVoxelsWritten;
            if (writes > remaining)
                return false;

            // No mutation occurs until all construction-time topology proofs pass.
            if (!AllSolid(authoring, in barrier))
                return false;

            DecorationBounds hiddenEnvelope = Union(in connector, in pocket).Expanded(new int3(1, 1, 1));
            if (!AllSolid(authoring, in hiddenEnvelope))
                return false;

            authoring.Carve(connector.Min, connector.Size);
            authoring.Carve(pocket.Min, pocket.Size);
            if (authoring.BudgetExceeded)
                return false;

            secret = new CaveSecretPocket(in terminal, in barrier, in connector, in pocket);
            return secret.IsWellFormed;
        }

        private static DecorationBounds OrientedBounds(
            int3 terminal,
            Facing facing,
            int forwardOffset,
            int width,
            int height,
            int depth)
        {
            int half = width / 2;
            switch (facing)
            {
                case Facing.North:
                    return Bounds(
                        new int3(terminal.x - half, terminal.y, terminal.z + forwardOffset),
                        new int3(width, height, depth));
                case Facing.South:
                    return Bounds(
                        new int3(terminal.x - half, terminal.y,
                            terminal.z - forwardOffset - depth + 1),
                        new int3(width, height, depth));
                case Facing.East:
                    return Bounds(
                        new int3(terminal.x + forwardOffset, terminal.y, terminal.z - half),
                        new int3(depth, height, width));
                case Facing.West:
                    return Bounds(
                        new int3(terminal.x - forwardOffset - depth + 1, terminal.y,
                            terminal.z - half),
                        new int3(depth, height, width));
                default:
                    return default;
            }
        }

        private static DecorationBounds Bounds(int3 min, int3 size) => new DecorationBounds
        {
            Min = min,
            MaxExclusive = min + size,
        };

        private static DecorationBounds Union(in DecorationBounds a, in DecorationBounds b) =>
            new DecorationBounds
            {
                Min = math.min(a.Min, b.Min),
                MaxExclusive = math.max(a.MaxExclusive, b.MaxExclusive),
            };

        private static long Volume(in DecorationBounds bounds)
        {
            int3 size = bounds.Size;
            return (long)size.x * size.y * size.z;
        }

        private static bool AllSolid(IStructureAuthoringSession authoring, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (!authoring.IsSolid(x, y, z))
                    return false;
            return true;
        }
    }
}
