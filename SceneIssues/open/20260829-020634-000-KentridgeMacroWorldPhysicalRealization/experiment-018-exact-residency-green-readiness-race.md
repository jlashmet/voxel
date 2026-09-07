# Experiment 018 — exact residency green, presentation-readiness race

## Exact evidence under test

- Source feature SHA: `df4cbcf366404f49b2c3e757720283d478bc0985`
- CI transport wrapper: `8861a6f55d21a6f77beecbe42b0fa682e36337bb`
- Workflow run: `33346099006`
- Artifact: `9742228777`
- Focused PlayMode discriminator: `KentridgeMacroWorldVerticalResidencyTests.OrdinaryStreamingMakesTallAuthoredFeatureUpperRegionResidentWithoutTraversalForcing`
- Result: 1/1 green. Ordinary `ShowcaseWorld.StepStreaming` makes an authored feature's upper Y region resident while the viewer remains in the lower presentation layer.

## Rejected hypothesis: generic survey ownership still steals Rossdam

The previous ownership hypothesis does not survive the latest exact run. The generic harness logs `HARNESS survey on at t=50.0s`, but Rossdam remains at stable near-field coverage across that handoff and proceeds to `content-ready` / capture. The dedicated macro evidence driver's `LateUpdate` ownership is therefore effective in this run. Do not make another ownership fix from the presence of the harness log alone.

## Repeated 60-second symptom

The exact replay still does not complete the required durable sequence:

- first rendered/startup interval consumes roughly 20.8 s;
- gameplay control becomes available around 26.8 s;
- Moordell's four authored building columns settle around 38.9 s;
- macro road / player-height arrival complete around 41.9 s;
- Rossdam's four authored building columns report settled around 53.9 s;
- Rossdam lake capture completes around 58.9 s;
- Fairy reaches content-ready near the 60 s cutoff but is not captured;
- Orc, southern ridge/pass, and network overview are not reached.

This is not a post-capture dwell-frame problem and acceptance remains fixed at 60 seconds.

## New visual discriminator

Full-resolution `macro-rossdam.png` is not merely poorly angled: it visibly contains one unmistakable gabled building, despite the evidence driver reporting all four Rossdam building-centre columns `content-ready`. The generic physical plan defines exactly four real blockouts for a generic settlement, each roughly 13.6 m x 10.4 m, 5.5–7.0 m tall plus a 2.4 m gable roof, spaced 38 m apart. The survey frame spans that footprint, so three buildings being simply offscreen is not a viable explanation.

`macro-rossdam-lake-detour.png` also remains visually closure-red: the water reads as a thin distant strip rather than a substantial lake. This is a separate framing/readability task and should not be used to hide the settlement publication defect.

## Root cause

`ShowcaseWorld.QueueFeatureRegionsForColumn` correctly discovers and queues every vertical Y region occupied by explicit authored features intersecting an accepted X/Z residency column.

`ShowcaseWorld.IsPresentationColumnContentSettled`, however, checks only:

1. the terrain `SurfaceLayerSpan` for the presentation X/Z column; and
2. the caller's explicit point Y layer when that point lies outside the terrain span.

The Kentridge evidence driver calls this predicate at each building's ground/presentation position. Therefore an authored roof or upper shell in an additional feature Y region can still be pending while the ground-layer predicate returns `true`. The exact vertical-residency test proves the upper region will eventually stream, but the built-player driver can race ahead and capture before that publication finishes. This exactly discriminates the observed state: four ground columns report `content-ready`, while only the subset whose complete shell is already published is visible.

## Correctness boundary for the next fix

The presentation-readiness predicate must use the same cached semantic feature-layer set already used by feature-aware residency for the requested X/Z column and require every such resident authored-content layer to be content-settled before returning true.

Constraints:

- no Kentridge coordinate special cases;
- no wider X/Z load radius;
- no scheduler/device-budget change;
- no pre-generation or traversal forcing;
- no weakening of the four-building settlement readiness requirement;
- no replay-duration increase;
- retain an ordinary `StepStreaming` regression using production catalogue content that crosses a Y-region boundary.

After this correctness fix is proven, re-run the 60-second artifact before deciding whether sequence timing needs a separate validation-only optimization. Rossdam and lake visual acceptance must be inspected independently.