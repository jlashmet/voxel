# Experiment 001 — source identity and baseline admission

## Hypothesis

The continuation must begin from the coordinator-published master state, with no agent-3 carryover, and must reproduce VoxelShowcase on that exact source before selecting a renderer fix.

## Exact source

- Feature branch: `fixes/agent-3`
- Coordinator master / merge base at assignment: `1bf4c5aea7e5da218f37a51dce79defb3eca4059`
- Unity: `6000.5.6f1` (`0e0577a1a2ac`)
- Project root: repository root (`ProjectSettings/ProjectVersion.txt`, `Assets/Scenes/VoxelShowcase.unity`)
- Targeted-CI transport: `ci-test/fixes/agent-3`

`fixes/agent-3` was cleanly fast-forwarded from its stale head to the published master SHA before any feature edits. GitHub comparison showed no carried agent-3 feature diff at that point.

## Source/document audit

Read the required workflow and architecture sources before implementation:

- `AGENTS.md`
- `SceneIssues/README.md`
- `SceneIssues/feature-readme.md`
- `.specify/memory/constitution.md`
- `specs/001-destructible-voxel-engine/device-matrix.md`
- `specs/001-destructible-voxel-engine/plan.md`
- `specs/002-world-feature-authoring/plan.md`
- prior restoration `plan.md`, `tasks.md`, `checkpoint-evidence.json`, and `cpu-render-backend-removal-ledger.md`

Tracked-tree audit confirms the retired `CpuWaterSurfaceChunkCache.cs` and `WaterBrickMeshBatchJobTests.cs` sources are absent on the assigned source. The GitHub repository API cannot prove absence of developer-machine *untracked* files; that portion of the checkout audit remains open and must not be claimed complete from remote tree state alone.

## Baseline request result

The first exact-SHA transport commit was `51e29e1bc8a24f9708f444bba68a5524880cd8a4`, with source parent `1bf4c5aea7e5da218f37a51dce79defb3eca4059`. GitHub Actions run `34137307109`, job `101791149168`, started after queue admission and completed `failure` on 2026-09-07.

The run did **not** exercise Unity compilation or VoxelShowcase. It failed immediately in `Replay SceneIssue through standalone player` because `.github/test-request.json` supplied only the SceneIssue id, while `tools/showcase-player-capture.sh` requires `--scene-issue` to be a repository path matching `SceneIssues/open/*/issue.json` (or closed/legacy pending/absolute forms). The exact error was `ERROR: invalid --scene-issue path.` This is a proven CI transport-request defect, not a renderer/product result.

Artifact `single-test-34137307109` contained only:

- `ModuleValidation/changed-files.txt` — empty, because the tested source equaled the assignment master baseline.
- `ModuleValidation/plan.json` — `hasProductionChanges=false`, `hasValidationWork=false`, no modules/tests/player validations.

No screenshots, player log, build log, compile result, or VoxelShowcase evidence were produced. Per the CI rules, this completed infrastructure/request failure may be retried only on the same `ci-test/fixes/agent-3` transport, with the request commit built directly on the current exact feature SHA and `scene_issue` corrected to `SceneIssues/open/20260907-145211-000-VoxelShowcaseGpuCorrectnessAndPerformance/issue.json`.

## Static discriminator work while waiting for a valid baseline

`GpuSolidChunkCache.MissingVisibleCount` is GPU-demand accounting for an in-band, frustum-classified coordinate that is pending and has no old ready entry. A stale-but-still-ready entry is not counted missing because the old mesh remains drawable during replacement. `TryRemoveChunk` removes stored GPU-demand accounting before slot retirement, so simple accumulation of retired/off-window accounting is not a supported root cause.

The cache also already provides visible-dirty priority and the scheduler can suppress background builds while visible demand is missing. Therefore no source patch is selected from static inspection alone. The required standalone evidence must distinguish source/admission starvation (H1) from publication/retirement/draw loss (H2), as the plan requires.

## Verdict

Source identity and required-document review are complete. The first CI request failed before Unity because its SceneIssue argument was malformed; it provides no product evidence. Fresh compile, untracked-local-file audit, standalone reproduction, visual annotation, and all implementation/performance gates remain open. No correctness implementation is selected until a valid production-path standalone baseline exists.
