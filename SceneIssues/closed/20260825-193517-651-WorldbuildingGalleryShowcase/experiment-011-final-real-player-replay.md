# Experiment 011 — final real-player scene-issue replay

## Purpose
Re-run the assigned saved WorldbuildingGalleryShowcase fixture after the complete durable grass + character-interactor fix, using the existing `--scene-issue` replay path. No new capture was created.

## Source and request
- Durable feature source before temporary routing: `e48afdcf1bb7ccf086839d94874a66bb7e1a2dfe`.
- Temporary replay-router commit: `c7abf4d893f9f9b424761cd016f513b12d146dd2`.
- CI request commit: `b09329b115f14d40842eebd352c219b617583146`.
- GitHub Actions run: `32938895670`.
- Artifact: `single-test-32938895670`, artifact id `9595885655`.
- The temporary router was removed from `fixes/agent-8` immediately after evidence collection.

## Real-player evidence
The standalone player replay step completed successfully and produced three saved-view screenshots at approximately 6.7s, 16.7s, and 26.8s. The final settled frame shows the exact red-circled grass clump in the saved pose as narrow alpha-cutout blade planes instead of an opaque billboard block. Comparing the tight circled grass region between the 16.7s and 26.8s frames showed 1,228 changed pixels out of 17,050, localized to the blades, providing frame evidence that the authored grass is not static.

The player log reached `HARNESS done after 30.0s, assertion failures 0`. After warmup, repeated `PASSES` samples were emitted and the settled renderer ran roughly 424–522 FPS with p95 frame time about 2.2–3.3 ms.

## Separate smoke-test result
The Unity PlayMode smoke assertion in the same workflow still failed with the pre-existing message `Worldbuilding gallery never bound its production rendering world. Expected: True But was: False`. That editor assertion is separate from the successful standalone-player replay and does not contradict the presented-frame evidence above.

## Result
PASS for the required real-player saved-fixture visual replay. The durable branch contains no replay-routing change after this experiment.
