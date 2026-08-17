#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs"


def replace_once(old: str, new: str) -> None:
    text = CACHE.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{CACHE}: expected one match, found {count}\n--- old ---\n{old}")
    CACHE.write_text(text.replace(old, new, 1))


replace_once(
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && vertexCount == 0\n            && indexCount == 0;\n""",
    """        private static bool RequiresFeaturePreservingFallback(\n            int sourceStep, bool hasOwnedSolid, bool hasProfileGeometry,\n            int vertexCount, int indexCount) =>\n            sourceStep == FeaturePreservingFallbackStep\n            && hasOwnedSolid\n            && !hasProfileGeometry\n            && vertexCount == 0\n            && indexCount == 0;\n""",
)

replace_once(
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _compactedTopologyVertices.Length + _facetedVertices.Length,\n                            _compactedTopologyIndices.Length + _facetedIndices.Length))\n""",
)

replace_once(
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
    """                    if (RequiresFeaturePreservingFallback(\n                            SourceStep, _build.HasOwnedSolid,\n                            _buildProfileBlocks.Length != 0,\n                            _facetedVertices.Length, _facetedIndices.Length))\n""",
)

print("step-4 fallback profile guard applied")
