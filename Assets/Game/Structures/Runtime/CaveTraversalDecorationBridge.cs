using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Bridges engine-authored traversal terminals into the existing cave-decoration patch model.
    /// This preserves terminal orientation and gives each semantic terminal its own deterministic
    /// patch identity without changing any existing surface-analysis or decoration placement policy.
    /// </summary>
    public static class CaveTraversalDecorationBridge
    {
        public static bool TryCreatePatch(
            ulong seed,
            in CaveTraversalCandidate terminal,
            in CaveConfig config,
            out CaveWalkablePatch patch)
        {
            patch = default;
            if (seed == 0 || !terminal.IsWellFormed || !config.IsWellFormed)
                return false;

            patch = CaveWalkablePatch.AtPathEnd(
                seed, terminal.Position, terminal.ExitFacing, in config);
            patch.PatchId = PatchId(seed, in terminal);
            return patch.IsWellFormed;
        }

        private static uint PatchId(ulong seed, in CaveTraversalCandidate terminal)
        {
            uint value = CaveDecorationSpaceAdapter.FoldSeed(seed);
            value = DecorationSeed.Derive(value, unchecked((uint)terminal.Position.x));
            value = DecorationSeed.Derive(value, unchecked((uint)terminal.Position.y));
            value = DecorationSeed.Derive(value, unchecked((uint)terminal.Position.z));
            value = DecorationSeed.Derive(value, (uint)terminal.TraversalDistance);

            uint semantics = terminal.BranchDepth |
                             ((uint)(byte)terminal.Flags << 8) |
                             ((uint)(byte)terminal.ExitFacing << 16);
            return DecorationSeed.Derive(value, semantics ^ 0x54524D4Eu); // TRMN
        }
    }
}
