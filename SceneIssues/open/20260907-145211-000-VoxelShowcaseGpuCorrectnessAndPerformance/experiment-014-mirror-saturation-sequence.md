# Experiment 014 — stationary mirror saturation sequence

## Goal

Use only the already accepted exact artifact from request `00e166f7073b5c8871a45cd100bb49e5731ca1bd` / run `34202852688` to bound the source-mirror stall more tightly before adding any new renderer policy or budget change. This is evidence analysis only; it does not treat GPU diagnostics as authoritative world state.

## Source evidence

- Exact validated production/test source: `909c9893bc0d491fd3676e87e2616da32e5d2933`.
- Artifact: `single-test-34202852688` / id `10050933912` / digest `sha256:b3553904f585429255f4b3077e35076b8731118cc0bdc4258810d7b63248df68`.
- Player log: `SceneIssue/player-run.log` from the 180-second stationary VoxelShowcase replay.
- Camera and visible defect are recorded in experiment 012; the step-two footprint projection is recorded in experiment 013.

## Bounded sequence reconstructed from every `RINGS` sample

The oldest active GPU source request changes only four times across 163 parsed late/settling samples:

| first sample | oldest age | step | brick-cache origin | derived step-2 chunk | missingVisible | mixed ready/cap | noSlot |
| ---: | ---: | ---: | --- | --- | ---: | ---: | ---: |
| 0 | 0.885 s | 2 | `(31,-1,31)` | `(2,0,2)` | 538 | 1,845 / 40,270 | 0 |
| 6 | 6.893 s | 2 | `(47,-1,31)` | `(3,0,2)` | 764 | 18,168 / 40,270 | 0 |
| 11 | 11.896 s | 2 | `(47,15,31)` | `(3,1,2)` | 700 | 34,506 / 40,270 | 0 |
| 72 | 72.939 s | 2 | `(47,31,31)` | `(3,2,2)` | 607 | 40,270 / 40,270 | 34,554 |

For step 2, the brick-cache origin maps to the chunk core as `(brickOrigin + 1) * 8` voxels and the 128-voxel step-2 chunk coordinate follows from that core origin. The sequence therefore advances through `(2,0,2) -> (3,0,2) -> (3,1,2) -> (3,2,2)` rather than jumping randomly between unrelated rings.

The mirror reaches exact mixed-slot capacity at the first parsed sample with `mixed=40270/40270`: oldest age `17.898 s`, oldest footprint still `(47,15,31)` / chunk `(3,1,2)`, `missingVisible=691`, and `noSlot=51`. From that point onward the mixed-slot count never drops below 40,270 while no-slot refusals accumulate:

| oldest age | oldest footprint | missingVisible | noSlot | mixed ready/cap |
| ---: | --- | ---: | ---: | ---: |
| 17.9 s | `(47,15,31)` | 691 | 51 | 40,270 / 40,270 |
| 30.9 s | `(47,15,31)` | 673 | 7,035 | 40,270 / 40,270 |
| 45.9 s | `(47,15,31)` | 649 | 16,337 | 40,270 / 40,270 |
| 60.9 s | `(47,15,31)` | 626 | 26,569 | 40,270 / 40,270 |
| 72.9 s | `(47,31,31)` | 607 | 34,554 | 40,270 / 40,270 |
| 91.0 s | `(47,31,31)` | 583 | 48,611 | 40,270 / 40,270 |
| 111.0 s | `(47,31,31)` | 561 | 62,400 | 40,270 / 40,270 |
| 131.0 s | `(47,31,31)` | 541 | 87,608 | 40,270 / 40,270 |
| 151.0 s | `(47,31,31)` | 513 | 120,097 | 40,270 / 40,270 |
| 163.0 s | `(47,31,31)` | 501 | 138,954 | 40,270 / 40,270 |

Throughout this saturated interval the shared page arena continues to report `allocFail=0`, while GPU publication does continue slowly (late ring summaries advance from roughly 324 to 330 published candidates). The source mirror remains the first observed bounded resource that is permanently full.

## Discriminator

This strengthens experiment 013 without overclaiming the missing-frustum identity:

1. The stall is not a one-frame cold-start event. The same step-2 source-acquisition path remains oldest for more than 160 seconds.
2. Saturation begins while the oldest request is chunk `(3,1,2)` and survives its eventual advance to `(3,2,2)`. Therefore one completed/admitted request does not restore enough mixed-slot headroom to let the next visible-demand footprint converge.
3. The failure is upstream of geometry page allocation: page-arena allocation failures remain zero while mirror `noSlot` grows by more than 138k.
4. Missing-visible count falls only from about 691 at first saturation to 501 near replay end; publication makes partial progress but cannot converge the visible set under the existing settle budget.
5. This still does **not** prove which exact missing-frustum candidate corresponds to the captured castle hole, nor whether the retained slots are held by active coverage, recovery, inactive residency, or another lifetime path. No capacity or eviction policy change is justified yet.

## Next discriminator

Preserve one exact missing-frustum `(step, chunk)` from the existing bounded GPU LOD-demand readback and correlate that same coordinate with renderer-local desired generation, dirty/queue age, active source-admission/coverage state, published page handle/generation, and final draw-selection/bounds state. Only after that correlation identifies the first broken transition should a production repair be attempted. Negative-coordinate traversal/return and edit/cancellation/pressure cases remain required separately by `tasks.md`.
