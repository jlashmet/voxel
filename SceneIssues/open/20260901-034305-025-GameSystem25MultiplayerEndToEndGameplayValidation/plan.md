# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation, identity/baseline convergence, contention/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** shared validation/Kentridge composition plus required fixes in existing Application, Sessions, GameplayReplication, Continuity and VoxelEngine.Net owners. Dependencies 06/07/08/14 and representative authoritative gameplay modules; System24 is related, not prerequisite. No fake networking, parallel authority, or privileged scenario mutation.

## Proven results and current discriminator

Exact request `0078199d98f3cefe1508ae7331b23ad001b754f7` passed run `34005489004` for source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`. It executed owned Application/Kentridge/Net tests and player validations plus the Kentridge integration player, so T25-010A joined-party startup and T25-010B canonical UTP admission transport are proven at that source.

Newer work adds T25-010C cancellable asynchronous provider formation and T25-010D safe live network admission/retry. Source `6f362a86753e653aa004d020322b89180a79dd6c` was tested by exact request `0f276655d84640b7fda5fbf5a268219f4146c15d`, run `34008863168`. The run failed before tests because `Game.GameplayReplication.Tests` referenced `AuthoritativeServerSession`, whose public surface now includes `IAuthoritativePlayerAdmission`, without directly referencing `VoxelEngine.Net.Api` (`CS0012`). Commit `02ad9e60cbe485d7b66170cc0b77d6c3a0bdd35e` adds that required assembly reference.

**H1:** the direct assembly reference is the only compile blocker introduced by exposing canonical Net admission. **H2:** once compilation proceeds, another product regression exists in the new formation/admission runtime or owned player probes. **Next discriminator:** exact-SHA targeted CI from the final bookkeeping head, requesting `Game.Sessions.Tests.SessionNetworkAdmissionRuntimeTests`; repository-driven module validation must compile and execute every affected owned test/player target.

Affected runtime/player owners remain Application, Sessions and VoxelEngine.Net; GameplayReplication change is test-assembly dependency only. Sessions and Net own focused real-production validation scenes. API-only formation contracts have no independent scene behavior; Application owns their player lifecycle proof.

Latest connected `master` is `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; reconcile current master before final promotion.

**Cost:** constant-time ownership checks; no new authority, packet, pool or tick budget. Existing 1,200-byte EVENT and queue limits stay unchanged. After 010C/D prove green, continue real authority/client topology, gameplay convergence, reconnect/leave, release scenarios, then closure and PR + auto-merge.
