# Plan

## Acceptance
No circles are marked, so acceptance is the complete native 1928×836 TerrainLookdev replay. Preserve the captured camera at `(-0.70, 18.80, -18.50)`, FOV 29; the whole frame must regain a readable layered meadow/path/rock composition rather than a flat green sheet with uniform rock clutter.

## Evidence / discriminator
- Experiment 001 removed stale camera ownership. CI/replay passed mechanically, but the real player frame remained the same broad high-angle terrain sheet: camera ownership was real cleanup, not the visual cause.
- Experiment 002 changed startup framing and passed a depth-band test, but replay stayed byte-stable because the SceneIssue harness re-pins the captured camera after startup. Camera-only fixes are falsified.
- History falsifies accidental loss of the old shelf-heavy detail pass: `aa6c580a` intentionally replaced it.
- Current production authoring is the remaining discriminator: `BuildRockFields` gives hundreds of similarly weighted limestone pieces while `BuildPath` is sparse pavers, so the pinned camera loses route and near/mid/far hierarchy.

## Selected fix / regression
Keep the captured camera unchanged. In the active TerrainLookdev authoring, reduce incidental rock/turf density, establish a continuous tapered path with restrained cobbles, and preserve five stronger outcrop groups across near/mid/far depth. `CapturedReplayFramesReadablePathAndLandmarkDepth` runs production startup and checks production-computed path/landmark points through the exact captured camera; final replay remains the visual acceptance gate.

## Blast radius / cost
TerrainLookdev authoring and its PlayMode test only; no shared renderer, storage, chunk generation, materials, or gameplay camera changes. Rock clusters drop 78→28, loose rocks 240→72, turf cushions 990→610. The continuous path adds roughly 10k bounded surface writes while removed detail offsets that work; the existing 9,000,000 authoring budget is unchanged.
