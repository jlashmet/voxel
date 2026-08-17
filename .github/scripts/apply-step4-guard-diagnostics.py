#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LOD = ROOT / "Assets/Tests/PlayMode/LodRenderingTests.cs"
PLAN = ROOT / ".claude/plans/voxel-showcase-rendering-repair-v2.md"


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}\n--- old ---\n{old}")
    path.write_text(text.replace(old, new, 1))

replace_once(
    LOD,
    """        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()\n        {\n            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(\n                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));\n            yield return null;\n            yield return WaitForAtomicWorldReady();\n\n            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();\n""",
    """        public IEnumerator CastleKeepsVoxelGeometryAcrossEveryLodBand()\n        {\n            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(\n                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));\n            yield return null;\n            yield return WaitForAtomicWorldReady();\n            Step4FalseEmptyDiagnostics.Reset();\n\n            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();\n""",
)

replace_once(
    LOD,
    "                      + $\"p:{metrics.Step4FeatureFallbackPublished} \"\n                      + $\"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/\"",
    "                      + $\"p:{metrics.Step4FeatureFallbackPublished} \"\n                      + $\"guard:{Step4FalseEmptyDiagnostics.Current} \"\n                      + $\"globalInvalidations=palette:{metrics.MaterialPaletteInvalidations}/\"",
)

replace_once(
    PLAN,
    "- [ ] Run the lifecycle diagnostics on a clean current head and identify the exact ready-empty adjudication cause: distinguish whether production step-4 castle chunks enter the feature-preserving fallback and still finish empty, never enter it because exact ownership is false, or complete non-empty but fail publication. Do not change coarse geometry again until this measurement is recorded.\n",
    "- [x] Run the allocation-free step-4 visibility/fallback lifecycle diagnostics on a frozen compile-valid head. Focused run 32032548787 (`4ad0df32`) bakes successfully and measures step 4 at `known=110`, `inBand=23`, `frustum=8`, `ready=0`, `empty=8`, with fallback `scheduled/completed/nonEmpty/published = 0/0/0/0`. The relevant chunks are therefore requested and adjudicated authoritative-empty before fallback; fallback execution/publication itself is not the failing stage.\n- [ ] Distinguish the remaining fallback guard rejection: print the existing exact-owned/profile/ordinary-result `Step4FalseEmptyDiagnostics` snapshot and determine whether the eight current-empty castle chunks are rejected because exact ownership is false or because authored profile geometry suppresses fallback. Do not change coarse geometry until this guard term is measured.\n",
)

print("step4 guard diagnostics staged")
