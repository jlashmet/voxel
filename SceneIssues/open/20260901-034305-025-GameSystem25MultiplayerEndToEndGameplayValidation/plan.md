# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation, identity/baseline convergence, contention/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every tasks.md criterion remains binding.

**Ownership:** shared validation and Kentridge composition plus required fixes in existing owners. Dependencies 06/07/08/14 and authoritative gameplay modules; System24 is related, not prerequisite. No fake networking, parallel authority or privileged scenario mutation.

## Current work / discriminators

Resume source `9c146000d9ee28ac8a0c0a97704ce49c40f17ad1` includes Application async formation `9e45d7169dc6d97de54fc650975a5709ee6ec811`, joined-party correction and canonical admission inbox. These newer changes are not yet exact-SHA proven.

**H1 supported by source:** `SessionNetworkAdmissionAdapter` binds before asking Net; rejected/repeated admission can disconnect the existing member and reset readiness while Net retains its original authenticated actor.

**H2 supported by source:** moving connection handles alone cannot provide reconnect. `ServerPlayerRegistry` deliberately rejects duplicate connection/player registration; the old connection must be torn down through the production authority before replacement.

Required T25-010D fix: preflight existing Sessions ownership, reject replacement of a live connection, ask the lower-level admission port before publishing a new Sessions binding, and preserve readiness on same-connection retries. Expose the existing Net authority through its own `IAuthoritativePlayerAdmission` API; same connection/player authentication is idempotent without position/permission reset or duplicate lifecycle accounting. Keep unrelated identity collisions rejected. Admission remains owning-thread work, not a network callback.

Affected owners: `Assets/Game/Sessions` runtime and existing tests plus new owned `Validation/SessionNetworkAdmissionValidation.unity` and paired scenario; `Assets/VoxelEngine/Net` API/runtime/tests and existing `Validation/SessionAdmissionTransportValidation.unity` probe/scenario. Scenes use real UTP/authority/PartySession; no alternate networking or gameplay authority. Tests must observe rejection/exception, duplicate and conflicting admission, old-connection cleanup, stable member/slot/character identity and readiness. These focused boundary proofs are not separate-process gameplay acceptance.

## Evidence / next gates

Request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` passed run `34002524305` for older source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; provenance in `ci-evidence-920f0e4.json`.

Preserve request `0078199d98f3cefe1508ae7331b23ad001b754f7`, source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`, run `34005489004`, job `101411770518`: queued at resume. No replacement while active. After completion, validate newer source before checking prerequisites.

Next: real Sessions/provider and host/client composition, then all gameplay/recovery/Release cases. Latest connected master `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; local git ls-remote still fails DNS. Reconcile current master before final promotion.

**Cost:** constant-time ownership checks; no new authority, packet, pool or tick budget. Existing 1,200-byte EVENT and queue limits unchanged. Close only after every criterion and exact gate passes; promotion remains PR + auto-merge.
