# Plan

- Evidence: the capture is ~40.6 s after opening handoff; player/controller/camera/render all report roughly `(137.7, 32.35, 74.8)` on the pub roof. `circleCount` is 0, so there are no marked regions; I reviewed the full capture/runtime record.
- Hypotheses: camera-only is rejected because all runtime poses agree. A bad X/Z target is rejected because the handoff and existing production acceptance align to `Pub.InteriorApproach` horizontally. The remaining fault is re-grounding the authored 3D interior target onto the roof.
- Fix: preserve the accepted `Pub.InteriorApproach` as the full 3D gameplay handoff position instead of re-grounding that authored spawn.
- Regression: strengthen the existing real-scene PlayMode opening acceptance from X/Z-only proximity to full 3D proximity after dialogue handoff.
- Blast/cost: Kentridge handoff plus its existing Kentridge PlayMode test only; no shared motor/world changes. This removes one one-shot ground query and adds no per-frame cost.
