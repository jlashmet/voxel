# Experiment 001 — runtime authority discriminator

## Hypothesis
The reported freeze is caused by a missing production battle-progression transition, not by a chain-combat reaction pause, asynchronous animation wait, or Kentridge scene installer failure.

## Action / source
Inspected source at baseline `0901be5a0640e3eec103cdf3c97aa12b8cd42a9e`: `KentridgeForestBanditEncounter`, `CombatService`, chain enemy/readiness/reaction coordinators, and existing Kentridge/chain PlayMode regressions.

## Result
Kentridge starts `CombatService` with one player plus three enemies and then only dispatches player movement. The service had positions and manual `CompleteCombat()` only: no HP, attack action, turn owner, AI action, terminal evaluation, or automatic completion. Chain AI's intentional reaction pause is separately resumable and is not composed into Kentridge. The Kentridge installer/activation path already has exact-scene regression coverage, and the production path has no asynchronous animation state to await.

## Verdict
Supported: after activation the production Kentridge encounter had no state transition capable of reaching victory/defeat. Fix the production authority and composition rather than adding a timeout/retry.

## Selected fix
`605d83da722e9c7f37a6129bb6c1a884ff2455af` adds bounded authoritative attack/turn/outcome progression, deterministic seeded AI control for both teams, Kentridge terminal teardown, and a built-player terminal log marker. Focused regressions are in the same ancestry.
