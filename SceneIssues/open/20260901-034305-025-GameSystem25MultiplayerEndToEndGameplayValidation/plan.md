# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation, identity/baseline convergence, contention/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** shared validation/Kentridge composition plus required fixes in existing Application, Sessions, GameplayReplication, Continuity and VoxelEngine.Net owners. Dependencies 06/07/08/14 and representative authoritative gameplay modules; System24 is related, not prerequisite. No fake networking, parallel authority, or privileged scenario mutation.

## Proven results and current discriminator

Exact request `0078199d98f3cefe1508ae7331b23ad001b754f7` passed run `34005489004` for source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`, proving T25-010A joined-party startup and T25-010B canonical UTP admission transport.

Exact request `81d5c60fd3844695f9932c40452dbb2fa13b29b9` passed run `34011275001`, job `101427436345`, for source `12a33443e6fd94c30bce50d234eae2697e836a15`. Automatic affected validation executed all 13 selected EditMode assemblies and 10 selected player-validation targets, including Application, Sessions, GameplayReplication, Kentridge and Net. This resolves the prior `CS0012` discriminator and proves T25-010C cancellable asynchronous formation plus T25-010D identity-preserving admission/retry at that exact source. Artifact `single-test-34011275001` has digest `sha256:4c637383d851f84dab5b382ec1e2831cb5373afbb145377ed2eb46795fd6fcf8`.

Newer production composition work is intentionally not claimed by that gate: `767cc1e48fdfa3db00009ba906a4ffc56f9e2877` adds the Kentridge UTP formation provider/authority admission bridge; `365405bcdcb8dbcdd5162a5d17dcd2149545b015` adds replicated client party/session boundaries with no client gameplay authority; `2c6710355d4b35ee5fbd8e019c3842df0d63bf07` adds explicit remote leave control; `310c774455e12fbfa5850a35e02f63be8d10e92c` exposes the authority binding needed for composition. These remain implementation inputs for T25-010/011/034 and require later exact-SHA execution.

**Current discriminator:** the shipped `KentridgePlayableSlice` still unconditionally composes `KentridgeSessionRuntimeGraphFactory` and enters its local orchestrator directly. T25-010/011 require role-aware production composition so the authority alone owns the campaign graph, while client processes join through `ApplicationFlowCoordinator` + Sessions/provider/UTP and run `KentridgeReplicatedClientSessionGraphFactory` over authoritative GameplayReplication state. Module-local multi-process validation must launch authority + client A + client B from the same build and prove durable identity and baseline convergence before gameplay mutations.

Latest connected `master` is `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; reconcile current master before final promotion.

**Cost:** constant-time ownership checks; no parallel authority or test-only transport. Existing 1,200-byte EVENT and bounded command queues remain binding. Continue topology first, then gameplay convergence, reconnect/leave, release scenarios, final exact-SHA validation, closure, and PR + auto-merge.
