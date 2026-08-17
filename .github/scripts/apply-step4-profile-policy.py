from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"
PLAN = ROOT / ".claude/plans/voxel-showcase-rendering-repair-v2.md"
WORKFLOW = ROOT / ".github/workflows/apply-step4-profile-policy.yml"


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    path.write_text(text.replace(old, new, 1))


replace_once(
    CACHE,
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
)

replace_once(
    CACHE,
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
)

replace_once(
    CACHE,
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && vertexCount == 0\n            && indexCount == 0;\n""",
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, bool hasAuthoredProfiles,\n            int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && !hasAuthoredProfiles\n            && vertexCount == 0\n            && indexCount == 0;\n""",
)

replace_once(
    PLAN,
    """- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.\n""",
    """- [x] Implement the proven step-4 false-empty repair: when exact classification owns solid content but ordinary step-4 topology/faceted output is empty, reuse the existing exact 2-voxel subcell summary/greedy HLOD path before publication. Authored profile chunks are explicitly excluded so their geometry cannot be duplicated. Normal step-4 geometry, LOD distances, the 0.50 ms global build budget and fidelity thresholds remain unchanged.\n""",
)

WORKFLOW.unlink(missing_ok=True)
Path(__file__).unlink(missing_ok=True)
