from pathlib import Path

TEST = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")
PLAN = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


test = TEST.read_text()

test = replace_once(
    test,
    """                foreach (var band in bands)\n                {\n                    // Fixed orthographic framing means camera distance selects the LOD ring without\n""",
    """                foreach (var band in bands)\n                {\n                    // Keep the lifecycle snapshot specific to the step-4 observation window. The\n                    // renderer still runs every ring while the camera visits steps 1/2, so resetting\n                    // here avoids attributing earlier background step-4 work to the failing capture.\n                    if (band.step == 4)\n                        Step4FalseEmptyDiagnostics.Reset();\n\n                    // Fixed orthographic framing means camera distance selects the LOD ring without\n""",
    "step4 lifecycle reset",
)

test = replace_once(
    test,
    """                      + $\"frustum:{metrics.Step4VisibilityFrustum}/ready:{metrics.Step4VisibilityReady}/\"\n                      + $\"empty:{metrics.Step4VisibilityEmpty} \"\n                      + $\"fallback:{metrics.Step4FeatureFallbackScheduled}/{metrics.Step4FeatureFallbackCompleted}/\"\n""",
    """                      + $\"frustum:{metrics.Step4VisibilityFrustum}/ready:{metrics.Step4VisibilityReady}/\"\n                      + $\"empty:{metrics.Step4VisibilityEmpty} \"\n                      + $\"lifecycle:{Step4FalseEmptyDiagnostics.Current} \"\n                      + $\"fallback:{metrics.Step4FeatureFallbackScheduled}/{metrics.Step4FeatureFallbackCompleted}/\"\n""",
    "step4 lifecycle failure output",
)

TEST.write_text(test)

plan = PLAN.read_text()

old_diag = "- [x] Add and wire cache-lifecycle diagnostics for the step-4 fallback path: exact owned/unowned classification, ordinary non-empty/empty output, fallback schedules/completions/non-empty completions/publications, and final ready-empty publications are exposed in `LodRenderingTests`; this instrumentation does not alter admission, geometry or publication behavior (`3385982f` plus lifecycle wiring)."
new_diag = "- [x] Add and wire cache-lifecycle diagnostics for the step-4 fallback path: exact owned/unowned classification, ordinary non-empty/empty output, fallback schedules/completions/non-empty completions/publications, and final ready-empty publications are counted without altering admission, geometry or publication behavior (`3385982f` plus lifecycle wiring)."
plan = replace_once(plan, old_diag, new_diag, "plan lifecycle instrumentation wording")

old_open = "- [ ] Run the lifecycle diagnostics on a clean current head and identify the exact ready-empty adjudication cause: distinguish whether production step-4 castle chunks enter the feature-preserving fallback and still finish empty, never enter it because exact ownership is false, or complete non-empty but fail publication. Do not change coarse geometry again until this measurement is recorded."
new_open = """- [x] Run the frozen compile-fixed step-4 visibility/lifecycle fixture far enough to classify ring ownership. Frozen run 32032548787 (`4ad0df32`) bakes successfully and reports step 4 `known=110/inBand=23/frustum=8/ready=0/empty=8`, with aggregate fallback `scheduled=0/completed=0/nonEmpty=0/published=0`. This proves the disappearing castle chunks are inside the correct ring and camera frustum but are adjudicated authoritative-empty before any feature-preserving fallback is entered; ring-band/frustum ownership is not the defect.\n- [x] Fix the lifecycle validation compile blocker (`CS1654`) by giving the temporary `NativeArray` fixtures explicit writable locals plus `try/finally` disposal; the frozen run above reaches the PlayMode fixture after the repair.\n- [ ] Expose the already-wired `Step4FalseEmptyDiagnostics.Current` snapshot in the step-4 failure output and rerun the frozen fixture to distinguish exact-unowned, profile-suppressed ordinary-empty, and fallback-empty adjudication. Do not change coarse geometry until this final empty-decision measurement is recorded.\n- [ ] Identify the exact ready-empty adjudication cause from that snapshot and fix only that measured branch: exact ownership false, profile suppression, fallback completion empty, or non-empty fallback publication failure."
plan = replace_once(plan, old_open, new_open, "plan lifecycle diagnosis tasks")

PLAN.write_text(plan)
