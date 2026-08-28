# Plan

## Observed defect / acceptance
The single 1920×1080 capture is a full-frame split-client comparison: the left client shows a destruction hole while the right client shows intact terrain at the same location. The note explicitly says remote-player spawning and local-camera follow are already correct. Acceptance is therefore only shared voxel/destruction state: an impact produced by either connected player must enter server authority and converge on both clients, while the solo showcase keeps its existing local edit path.

## Competing hypotheses
1. **Producer bypass (leading):** the showcase impact mutates `ShowcaseWorld` locally and never emits an alteration request. Falsifier: an active multiplayer impact reaches `ShowcaseMultiplayerSession.TryRequestExplosion` before local mutation.
2. **Relay/apply failure:** a request is sent but server relay or client application loses/rejects it. Falsifier: the request is absent at the producer; existing authoritative/two-player/event-replication tests then make a lower-layer rewrite unjustified.

## Discriminator / evidence
At feature SHA `c1419aa4c42b528322cad545ebc5a52d46d73ab6`, `VoxelShowcase.StepTornadoes` calls `_world.Explode(...)` directly. Commit `41a004da1c29aad4872b39ea851d54d30d9a410a` explicitly removed the showcase multiplayer branches, including `TryRequestExplosion`, while `ShowcaseMultiplayerSession` still owns server authority and applies ordered authoritative events. The repository also retains focused UTP, two-player authoritative, event-driven replication, convergence, and shared-destruction tests. Hypothesis 1 wins before transport.

## Selected fix
Restore the existing session only when the active scene is `Multiplayer`, restore its movement/pump and host/join surface there, and route terrain impacts through `TryRequestExplosion` when active. Never fall back to a local explosion while connected. Keep solo scenes unchanged. Add a PlayMode behavioral regression through the production explosion router, plus the existing authoritative networking coverage.

## Blast radius / cost
No protocol, storage, deterministic brush, server arbitration, or renderer change. One existing session is allocated only in `Multiplayer`; connected movement keeps the existing 30 Hz network tick. A destruction impact adds one existing alteration request/flush, not per-frame traffic. Solo cost/path remains unchanged.

## Gates
Targeted regression CI; replay the captured Multiplayer pose; inspect both halves for a converged edit; commit final verification evidence; pending/fixed bookkeeping; merge current master and non-force publish.
