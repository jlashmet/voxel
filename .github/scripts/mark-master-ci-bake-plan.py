from pathlib import Path

PLAN = Path('.claude/plans/voxel-showcase-rendering-repair-v2.md')
text = PLAN.read_text()
implementation = "- [x] Ensure the master PlayMode matrix bakes the showcase startup world for `VoxelEngine.CI.PlayMode` as well as `VoxelEngine.Tests.PlayMode`; both can open `VoxelShowcase.unity`, and runtime castle authoring is intentionally forbidden (`c331ad9a`)."
validation = "- [ ] Validate the expanded master bake prerequisite on a clean master/full-suite execution; only mark complete after `VoxelEngine.CI.PlayMode` reaches its tests with `ShowcaseWorld.bytes` present."
if implementation not in text:
    anchor = "- [x] Validate the corrected no-stutter and LOD-fidelity harnesses on the clean current head. PR run 32022085431 proves both fixtures now exercise real production rendering: no-stutter reaches a live renderer and fails on convergence (`dirty=4264`, `running=16`, `missingVisible=818`, `visible=131`), while fidelity gets past bootstrap `RenderRequest` setup and fails because LOD 1/view 0 never stabilizes (centre-step mask 0). The harness blockers are resolved; the renderer acceptance failures remain open."
    if anchor not in text:
        raise SystemExit('Section F anchor not found')
    text = text.replace(anchor, anchor + '\n' + implementation + '\n' + validation, 1)
if "- `c331ad9a` — master full-suite bake now also covers `VoxelEngine.CI.PlayMode`." not in text:
    anchor = "- `88610d2b` / `27d0f141` — two-hour memory soak split by tier/process and mirrored in PR/master workflow isolation."
    if anchor not in text:
        raise SystemExit('continuation anchor not found')
    text = text.replace(anchor, anchor + "\n- `c331ad9a` — master full-suite bake now also covers `VoxelEngine.CI.PlayMode`.", 1)
PLAN.write_text(text)
print('plan updated')
