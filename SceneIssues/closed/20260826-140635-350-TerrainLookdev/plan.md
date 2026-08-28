# Plan

## Acceptance
No circles are marked, so acceptance is the complete native 1928×836 TerrainLookdev replay. Preserve the captured camera at `(-0.70, 18.80, -18.50)`, FOV 29; the whole frame must regain a readable layered meadow/path/rock composition rather than a flat green sheet with uniform rock clutter.

## Evidence / discriminator
- Experiment 001 removed stale camera ownership. CI/replay passed mechanically, but real-player run `33130926419` stayed the same broad high-angle terrain sheet: camera ownership was not the visual cause.
- Experiment 002 changed startup framing and passed a depth-band test, but run `33132706675` stayed byte-stable because SceneIssue replay re-pins the captured camera after startup. Camera-only fixes are falsified.
- History shows the prior shelf-heavy detail pass was intentionally replaced, so accidental loss of that old pass is also falsified.
- The active authoring remained the discriminator: hundreds of similarly weighted limestone pieces overwhelmed a route represented only by sparse pavers.

## Selected fix / regression
Keep the captured camera unchanged. Reduce incidental rock/turf density, establish a continuous tapered path with restrained cobbles, and preserve five stronger outcrop groups across near/mid/far depth. `CapturedReplayFramesReadablePathAndLandmarkDepth` runs production startup and checks production-computed path/landmark points through the exact captured camera.

## Validation / blast radius
Exact source `d841d3461d3ee7a763414cb6ad35bed69afafbf4` passed targeted CI request `fae0bdd57d4b5401f396647c4da082cd68461c19`, run `33138598594`; its 60-second real-player replay pinned the original camera and captured the revised full frame successfully. Scope is TerrainLookdev authoring plus its PlayMode test only—no shared renderer, storage, chunk generation, materials, or gameplay camera changes. Rock clusters drop 78→28, loose rocks 240→72, turf cushions 990→610; the path adds roughly 10k bounded writes while the existing 9,000,000 authoring ceiling remains unchanged. Remaining work is bookkeeping/merge only.
