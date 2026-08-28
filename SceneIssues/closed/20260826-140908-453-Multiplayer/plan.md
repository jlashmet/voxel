# Plan

## Observed defect / acceptance
The capture has one 1928×836 saved pose and no circle annotations, so the full frame is the inspection region. The note asks for a Multiplayer scene where two peers can join, see player movement, and converge when terrain or trees are destroyed. The reported runtime repro later showed peers could see each other, but local voxel edits did not appear remotely. Acceptance for this fix is therefore authoritative shared terrain destruction while preserving the existing solo showcase path.

## Competing hypotheses / discriminator
1. **Producer bypass (confirmed):** the gameplay impact mutates `ShowcaseWorld` locally and never emits a network request. Falsifier: a connected impact reaches `ShowcaseMultiplayerSession.TryRequestExplosion` before local mutation.
2. **Relay/apply failure:** the request reaches transport but server relay/client application drops it. Falsifier: no request is produced at the gameplay boundary.

At the pre-fix source, `VoxelShowcase.StepTornadoes` called `_world.Explode(...)` directly. Commit `41a004da1c29aad4872b39ea851d54d30d9a410a` had removed the old multiplayer producer branch, while `ShowcaseMultiplayerSession` and the authoritative relay/apply machinery remained. The defect therefore occurs before transport.

## Selected fix / regression
Enable the existing multiplayer session only in the `Multiplayer` scene, restore its host/join and per-frame session plumbing, and route connected terrain impacts through `TryRequestExplosion`. Never fall back to a local explosion while networked. Solo scenes keep the direct deterministic explosion path. `ShowcaseExplosionRouterTests` covers connected routing, no-local-fallback while a request waits, and unchanged offline behavior.

## Blast radius / cost
No protocol, brush, storage, renderer, or server-arbitration change. One existing session is allocated only in `Multiplayer`; connected movement uses the existing network tick and each destruction impact emits one existing alteration request. Solo cost/path is unchanged.

## Verification
Production/test fix: `c648403ff81494a8df6fbe1dc534cce18c1abf29`. Exact CI request `1085bfb2d757da937c686dedebf4c784e3b2d51f` is green: 3/3 focused PlayMode tests passed, and the saved-pose real-player replay built/ran successfully. The replay is offline, so peer convergence is proven by the behavioral routing regression, not by the screenshot.
