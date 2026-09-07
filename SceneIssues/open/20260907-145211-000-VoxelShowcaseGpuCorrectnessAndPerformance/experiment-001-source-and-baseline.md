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

## Baseline request

Exact-SHA CI request was created by resetting only the CI transport to source `1bf4c5aea7e5da218f37a51dce79defb3eca4059` and committing only `.github/test-request.json` as transport commit `51e29e1bc8a24f9708f444bba68a5524880cd8a4`. The request asks the existing SceneIssue standalone path to replay `20260907-145211-000-VoxelShowcaseGpuCorrectnessAndPerformance` for 180 seconds. The workflow derives the tested source from the request commit parent, so the candidate under test is the feature SHA above.

- GitHub Actions run: `34137307109`
- Job: `101791149168`
- Requested source: `1bf4c5aea7e5da218f37a51dce79defb3eca4059`
- State at last check: `queued`

Repository Actions currently reports 15 queued workflow runs and zero in-progress runs. Per `SceneIssues/README.md`, this queued request is not cancelled, replaced, or superseded.

## Static discriminator work while blocked

`GpuSolidChunkCache.MissingVisibleCount` is GPU-demand accounting for an in-band, frustum-classified coordinate that is pending and has no old ready entry. A stale-but-still-ready entry is not counted missing because the old mesh remains drawable during replacement. `TryRemoveChunk` removes stored GPU-demand accounting before slot retirement, so simple accumulation of retired/off-window accounting is not a supported root cause.

The cache also already provides visible-dirty priority and the scheduler can suppress background builds while visible demand is missing. Therefore no source patch is selected from static inspection alone. The required standalone evidence must distinguish source/admission starvation (H1) from publication/retirement/draw loss (H2), as the plan requires.

## Verdict

Source identity and required-document review are complete. Fresh compile, untracked-local-file audit, standalone reproduction, visual annotation, and all implementation/performance gates remain open. The exact baseline is blocked on the repository's unavailable/idle self-hosted runner queue; independent static audit continues, but correctness implementation is deliberately not started before the production-path discriminator exists.
