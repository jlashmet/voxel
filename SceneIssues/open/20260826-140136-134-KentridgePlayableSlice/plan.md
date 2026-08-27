# Plan

- Evidence: the capture is ~40.6 s after opening handoff; player/controller/camera/render all report roughly `(137.7, 32.35, 74.8)` on the pub roof. `circleCount` is 0, so there are no marked regions; I reviewed the full capture/runtime record.
- Hypotheses: camera-only is rejected because all runtime poses agree. A bad X/Z target is rejected because the handoff and existing production acceptance align to `Pub.InteriorApproach` horizontally. `CharacterMotor.SnapToGround` then takes the highest occupied surface in the capsule footprint without respecting the supplied Y, so an interior point beneath a roof is deterministically promoted to that roof.
- Fix: restore `SnapToGround`'s documented “surface below the given position” contract by treating the supplied Y as a ceiling when a column has geometry above it; ordinary terrain keeps the existing fast path.
- Regression: add a focused real-scene PlayMode test that loads Kentridge, exercises the actual pub gameplay handoff against generated geometry, and requires full 3D proximity to `InteriorApproach`.
- Blast/cost: one shared one-shot grounding helper plus one Kentridge PlayMode regression; no movement, streaming, world-generation, or per-frame behavior changes. The downward scan runs only for footprint columns whose top occupied surface is above the authored snap point.
