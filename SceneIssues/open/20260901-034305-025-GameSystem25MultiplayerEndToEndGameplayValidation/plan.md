# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation, identity/baseline convergence, contention/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every tasks.md criterion remains binding.

**Ownership:** shared validation and Kentridge composition, with prerequisite fixes in existing owners. Dependencies 06/07/08/14 and authoritative gameplay modules; System24 is related, not prerequisite. No fake networking, parallel authority or privileged scenario mutation.

## Implemented / discriminator

Implementation: `9e45d7169dc6d97de54fc650975a5709ee6ec811`; planning: `66de64ff214b74ce6b8885d0ec394ac6cc00067d`; resume source: `37ddc626a2cc4c7d31751a4f16d03f6aa74df657`. Connected GitHub master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; local Git fetch fails DNS.

**H1 supported:** synchronous formation requires admitted identity before UTP can reply. T25-010C now adds optional `IAsyncSessionFormationService` / request-local `ISessionFormationOperation`; synchronous providers remain supported. Application owns one nonblocking attempt, validates terminal session/member identity, blocks overlapping intent and detaches before cancellation. Leave/Quit/Dispose cannot adopt late replies. Provider exception credentials are not exposed. Host still requires Start; clients still require connected local GameplayReady and Orchestration readiness.

**H2 supported:** `ApplicationFrontendView` previously updated only StartingSession/InGame, leaving frontend admission/readiness stalled. It now updates FrontEnd, exposes pending Cancel/Quit, and routes error recovery through coordinator-owned cleanup.

**Discriminator awaiting execution:** 37 new `ApplicationPendingFormationTests` cases cover delayed/adversarial results, cancellation/retry, host/join distinction, failures, reentrancy, navigation and synchronous compatibility using real Orchestration. The existing Application player scene includes `ApplicationPendingFormationValidation`: actual Unity view updates must produce pending, stale-result discard, single startup and normal Leave milestones. The probe never calls Application.Update. External semantic inputs are not provider/UTP or multiplayer-process proof.

Affected owners: Sessions API only (no scene behavior); Application runtime/tests/owned scene and paired scenario. Existing assembly references suffice. Structural audit verified original source hashes, scene GUID wiring and unchanged prior log/capture/run budgets. **No new C# compilation, NUnit execution or built-player execution has occurred.** T25-010A/B/C remain unchecked.

## Evidence / next gates

Request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` passed run `34002524305` for older source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; provenance in `ci-evidence-920f0e4.json`.

Preserved request `0078199d98f3cefe1508ae7331b23ad001b754f7`, source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`, run `34005489004`, job `101411770518`: still queued at final continuation check. Transport unchanged; newer source needs later exact validation after completion.

Next: real Sessions/provider and host/client composition, then every gameplay/recovery/Release case. During integration, correct the inspected `SessionNetworkAdmissionAdapter` bind-before-network-success path so rejected/repeated admission cannot destroy a live binding/readiness; retain the canonical authority.

**Cost:** one pending operation, no blocking/polling loop or new authority; ten-second player scenario/captures and 1,200-byte EVENT / 256-per-connection / 4,096-total limits unchanged. Close only after all criteria and exact gates pass; promotion remains PR + auto-merge.
