# Interactables / Secrets v1 inventory

## Activation sources

| Candidate | Decision | Rationale |
|---|---|---|
| Lever | Accepted | Generic toggle source; required remote-route secret example. |
| Visible button / wall switch | Accepted as Button | Generic one-shot activation source. `Switch` is deferred as redundant visual vocabulary for v1 because Button covers the required visible control semantics. |
| Hidden / disguised button | Accepted | Same generic Button behavior, composed with concealment in the showcase; no separate source-target code. |
| Pressure plate | Accepted | Generic enter/exit activation source and useful toggle/re-close demonstration. |
| Crank / winch | Deferred | Existing vocabulary can represent it, but no acceptance scenario requires the additional affordance in v1. |
| Pull chain | Deferred | Existing vocabulary can represent it, but it duplicates lever-style toggle semantics for this showcase. |
| Key / lock input | Deferred | Lock/unlock remains supported by generic door behavior, but inventory acceptance does not require a separate key-item system. |
| Timed / multi-step input | Deferred | Optional in the issue and would add timing policy not needed to prove source-target composition. |

## Mechanisms

| Candidate | Decision | Rationale |
|---|---|---|
| Door | Accepted | Existing direct/local behavior and non-secret reuse baseline. |
| Trapdoor | Accepted | Existing direct/local behavior; regression protects the prior concept. |
| Gate | Accepted | Remote linked route mechanism. |
| Portcullis | Accepted | Distinct accepted gate presentation already supported by shared runtime. |
| Elevator / lift | Accepted | Required high-place secret composition. |
| Drawbridge | Accepted | Existing generic moving-route mechanism and non-secret reuse example. |
| Bookshelf / false wall / secret panel | Accepted as RotatingWall secret panel | `RotatingWall` supplies moving false-wall passage semantics without teaching the shared runtime a scene-specific bookshelf rule. A bookshelf prop may dress the station visually; behavior remains the shared mechanism. |

## Control patterns

- Direct/local: accepted; door, trapdoor, elevator, secret panel can be operated through their generic behavior.
- Remote linked: accepted; lever/plate/button signal through `WorldObjectConnection` to a separate target.
- One-shot: accepted; Button emits activation and the required secret reveal/open is not coupled to a matching deactivate connection.
- Toggle: accepted; Lever and direct mechanisms demonstrate reversible transitions.
- Timed: deferred for v1; optional and not needed to prove the reusable contract.
