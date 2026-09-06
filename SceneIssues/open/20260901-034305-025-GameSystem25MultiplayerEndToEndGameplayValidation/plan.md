# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation; durable identity/baseline convergence; contention/conservation; combat/progression; interruption/reconnect/current-state recovery; explicit leave; configured capacity/JIP/repeated reconnect/persisted rehost; durable exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** shared validation/Kentridge composition plus required fixes in Application, Sessions, GameplayReplication, Continuity and VoxelEngine.Net. No fake networking, parallel gameplay authority, privileged mutation seam, or test-only transport.

## Proven results

Harness role isolation, launch/kill/relaunch, exact executable/source identity, monotonic semantic waits, and role-attributed artifacts are already exact-SHA proven. Application joined-party startup plus canonical UTP admission passed exact source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8` in run `34005489004`. Cancellable async formation and identity-preserving admission/retry passed exact source `12a33443e6fd94c30bce50d234eae2697e836a15` in run `34011275001`.

Request `fc4e467e202c295459d775ee64c450afab6f15e8` failed run `34025141968` on three test-only `byte[].AsSpan(...)` compiler errors. The scoped compatibility fix was validated by request `046cca7d61f7052828878fd749050dbf592f6f88`, run `34028935498`, which completed successfully for feature source `bfdcf1ee81ce8541669edac570c46544b998743b`.

## Current discriminator and selected fix

The generic harness is sufficient; the remaining topology gap is a real separate-process consumer of the production Kentridge multiplayer wrappers. The branch now stages `KentridgeMultiplayerTopologyValidation`: authority uses `KentridgeSessionRuntimeGraphFactory`, the production campaign bootstrap, `ApplicationFlowCoordinator.RequestHost`, Sessions formation, canonical `AuthoritativeServerSession`, and UTP. Client A/B use `RequestJoin`, the production formation service, gameplay replication, and `KentridgeReplicatedClientSessionGraphFactory`. The scenario launches authority first, then A then B, so slot/member/character allocation is deterministic and all three roles must report the same three-member topology signature.

This implementation is **not yet acceptance evidence**. T25-010 through T25-013 remain unchecked until exact-SHA CI compiles the new validation assembly and the built multi-process player target passes. If that gate is green, record the artifact and then proceed to gameplay contention/conservation before reconnect/release coverage.

**Remaining gates:** exact-SHA topology smoke; T25-020–023 gameplay convergence; T25-030–034 reconnect/leave; T25-040–043 release scenarios; T25-051 automatic selection; T25-052 final evidence; reconcile current `origin/master`; closure metadata; PR + auto-merge.
