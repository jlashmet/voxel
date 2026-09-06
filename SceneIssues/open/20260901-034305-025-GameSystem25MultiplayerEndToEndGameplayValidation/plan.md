# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build; separate authority/client processes; production formation, durable identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every required tasks.md item remains binding.

**Ownership:** shared validation infrastructure and Kentridge composition, with prerequisite fixes in existing owners. Dependencies 06/07/08/14 and authoritative gameplay modules; System24 is related, not a prerequisite. No fake networking, parallel authority or privileged scenario mutation.

## Current discriminator / selected work

Resume source `37ddc626a2cc4c7d31751a4f16d03f6aa74df657`; implementation parent `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`. Master fetched through connected GitHub: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. Local Git fetch cannot resolve github.com.

Canonical authority admission now drains copied, bounded bytes at its fixed tick; Net does not decide membership/readiness. Application's joined-party readiness correction and Net execution remain pending.

**H1 supported by inspection:** `ISessionFormationService.Join` requires a terminal member immediately, while UTP admission is asynchronous. Reusing it alone would require blocking or manufacturing an unconfirmed client member.

**H2 / next discriminator:** adding pending formation without request-local ownership would allow late completion after Leave/Quit to adopt the wrong party or start a graph. Exercise delayed authority result, cancellation followed by a fresh request, malformed/mismatched success, provider failure, and host versus join behavior through the real Application coordinator and Orchestrator.

Selected T25-010C: optional nonblocking Sessions formation operation contract; one owned cancellable pending operation in Application; do not adopt identity until matching authority success; preserve the separate connected/GameplayReady gate and synchronous-provider compatibility. No networking or authority implementation belongs in Application. Provider deadlines and transport pumping remain provider-owned.

Affected owners: `Assets/Game/Sessions/Api` (semantic contracts only; no scene behavior, domain boundary tests via consumers) and `Assets/Game/Application` (owned EditMode tests and existing `Validation/ApplicationFrontendValidation.unity` plus paired scenario). Extend the existing nonvisual lifecycle discriminator; it is not evidence of real provider/network/multiplayer topology. Real provider and separate-process integration remain required T25-010/011.

## Evidence / gates

Request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` passed run `34002524305` for source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; provenance/counts in `ci-evidence-920f0e4.json`. It does not prove later code.

Preserve active request `0078199d98f3cefe1508ae7331b23ad001b754f7`, source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`, run `34005489004`, job `101411770518`, queued at continuation check. New source needs later exact validation after this request completes. No acceptance checkboxes may be inferred from authored tests.

**Cost:** one pending operation, no polling loop/blocking wait or extra authority; existing 1,200-byte EVENT and 256/4,096 admission limits unchanged. Finish all gameplay/recovery/Release gates, then close, merge current master and promote through PR + auto-merge only.
