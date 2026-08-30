# WorldBuilder Secret Discovery and Clue Design

## Status and related work

Status: Draft

Related:
- `Docs/worldbuilding-interactables-plan.md`
- `Docs/worldbuilding-interactables-tasks.md`
- `specs/002-world-feature-authoring/plan.md`
- `SceneIssues/open/20260830-014314-000-ExplorationInteractablesSecretsShowcase/issue.json`

> Current `master` does not expose a single verified canonical WorldBuilder `SecretPlanner` type. The contracts and names below are proposed additions that layer onto the current world-feature and interactable authoring architecture.

## Purpose

Make secret exploration an authored/generated gameplay system. WorldBuilder should create secrets that are discoverable from intentional evidence, connect routes to reusable interactables, and emit stable metadata for runtime discovery/reward without owning save state.

## Design principles

- Secrets are places/content, not just chests.
- Exploration rewards observation and inference, not wall-spamming.
- WorldBuilder plans; runtime systems execute and persist.
- Generation is deterministic for the same inputs/seed.
- Clues are semantic, not prefab-specific.
- Multiple routes/mechanisms may lead to one secret.
- Physical loot and discovery credit are separate.
- Important optional content should retain value when discovered late.
- Multiplayer discovery credit is authoritative and idempotent.
- Voxel destruction is an authored part of the solution space, not an accidental bypass.

## Core model

**Secret → Route(s) → Clue(s)**

Definitions:
- **Secret**: stable hidden region or optional destination.
- **Route**: one legal access mechanism/path.
- **Clue**: pre-solve observable sign supporting inference.
- **Discovery**: runtime first-credit event.
- **Reward profile**: reference to runtime XP/evergreen reward tuning.
- **Bypass policy**: whether voxel modification may circumvent a route.

Important:
- A secret may exist without a physical loot chest.
- A clue is not necessarily an interactable.
- A route may be a reusable interactable, natural traversal, or intentional destructible geometry.
- A secret may support multiple legitimate routes.
- Runtime discovery triggers regardless of which legitimate route is used.

## Existing architecture boundary

### World-feature authoring

Align with `specs/002-world-feature-authoring/plan.md`: deterministic, constraint-driven generation/planning.

### Interactables

Reuse the existing `InteractableDescriptor`, `InteractableAnchor`, `InteractableState`, `IWorldInteractable`, and `IInteractableRealizer` concepts documented in `Docs/worldbuilding-interactables-plan.md`. Secret route mechanisms should not become one-off scene systems.

### Persistent discovery

The Exploration Interactables SceneIssue requires persistent discovery or integration with the canonical repository authority and expressly forbids showcase-local secret state. Its acceptance flow references `memory://secrets`; until an owning runtime API is verified, treat that as an integration target rather than making WorldBuilder the persistence authority.

### Physical loot

Discovery credit and physical treasure are independent. Do not require every secret to contain a chest, and do not make a loot policy responsible for XP/discovery persistence.

## Proposed planning contracts

Names are conceptual and should be adapted to repository naming conventions.

```csharp
public enum SecretImportance
{
    Minor,
    Standard,
    Major
}

public enum SecretRouteKind
{
    Door,
    Trapdoor,
    Pushable,
    BreakableBarrier,
    PressurePlateMechanism,
    Climb,
    Swim,
    NaturalTraversal,
    ScriptedMechanism
}

public enum SecretClueChannel
{
    Spatial,
    Visual,
    Audio,
    Environmental,
    Mechanical,
    Narrative,
    Navigation
}

public enum SecretBypassPolicy
{
    ProtectedShell,
    AuthoredBreakablesOnly,
    SystemicBypassAllowed
}

public sealed record SecretPlan(
    SecretId Id,
    SecretImportance Importance,
    IReadOnlyList<SecretRoutePlan> Routes,
    IReadOnlyList<SecretCluePlan> Clues,
    DiscoveryRewardProfileId? DiscoveryReward);

public sealed record SecretRoutePlan(
    SecretRouteId Id,
    SecretId SecretId,
    SecretRouteKind Kind,
    SecretBypassPolicy BypassPolicy,
    InteractableId? InteractableId);

public sealed record SecretClueRequirement(
    SecretClueChannel Channel,
    int MinimumCount,
    bool MustBePreSolveObservable);

public sealed record SecretCluePlan(
    SecretClueId Id,
    SecretId SecretId,
    SecretRouteId? RouteId,
    SecretClueChannel Channel,
    SemanticAnchorId AnchorId,
    int Strength);
```

IDs and value types above are illustrative. Prefer repository-native stable-ID/value-object conventions when implemented.

## Semantic clue anchors

WorldBuilder templates/nodes expose **candidate semantic anchors**, not prefab names.

Initial semantic roles:
- `ApproachEvidence`
- `ExteriorEvidence`
- `RouteAdjacentEvidence`
- `SightlineHint`
- `AcousticHint`
- `TraversalHint`
- `NarrativeHint`

Candidate anchor metadata should be able to declare:
- supported clue channels;
- observable side/region;
- placement bounds and facing;
- pre-solve reachability;
- useful distance range to secret/route;
- relation to hidden volume;
- tags and compatibility constraints.

The clue plan describes **meaning**. Realization chooses presentation appropriate to the generated feature. For example, a castle spatial clue may become an inaccessible dormer window; a crypt mechanical clue may become scrape marks; a cave environmental clue may become escaping water or airflow.

## Clue planning pipeline

1. Plan/select the hidden destination.
2. Plan legal route candidates.
3. Derive clue requirements from importance, route kind, and authored difficulty.
4. Gather compatible clue anchors in pre-solve-observable space.
5. Score candidates for observability, relevance, channel diversity, distance, duplication/overexposure, and thematic fit.
6. Select deterministically using the feature seed and stable tie-breaking.
7. Validate count, channel diversity, observability, and dependency topology.
8. Emit immutable secret/route/clue plans.
9. Realization maps route plans to reusable interactables/geometry and clue plans to presentation.
10. Runtime discovery registers and credits the stable secret identity.

## Default clue policy

Initial tuning; feature authors may override with explicit constraints.

| Importance | Default clue expectation |
| --- | --- |
| Minor | 0–1 subtle clue |
| Standard | At least 1 meaningful clue |
| Major | At least 2 clues across independent channels |

Progression-critical hidden content should not use Minor-secret readability rules. If content is required for the main path, the authoring system should require stronger discoverability constraints and may choose not to classify it as a reward-bearing "secret."

## Observability constraints

- A required clue cannot require solving the same route it explains.
- A required clue must be visible, audible, or otherwise perceivable from a pre-solve state.
- Required clues cannot all be inside the hidden region.
- Major-secret clues should not all rely on the same channel.
- Avoid generic "glowing crack means secret" as a universal visual language.
- Route animation or closed-state geometry must not obscure all required clues.
- An impossible clue topology should fail validation or select a deterministic fallback; do not silently emit an unsolvable secret.

## Interactable integration

Route mechanisms use the existing descriptor/anchor/state contract:
- stable route/secret metadata links the route to a `SecretId`;
- the route does not duplicate interaction authority;
- tags such as `secret-route`, plus mechanism-specific tags, remain useful for authoring/querying;
- the SceneIssue's reusable `SimpleDoor`, `SimpleTrapdoor`, resolution-based voxel barrier, kinematic pushable, and pressure plate are valid route realizations.

Responsibility split:
- **WorldBuilder planner:** what routes/clues exist, where they attach, and what constraints they satisfy.
- **Interactable runtime:** authoritative interaction behavior, state transitions, and replication.
- **Clue realization:** visuals/audio/environmental presentation only; it does not own route state.
- **Discovery runtime:** first-discovery detection, persistence, party credit, and reward grant.

## Runtime discovery and rewards

WorldBuilder should emit enough stable data for runtime registration:
- stable `SecretId`;
- importance/classification;
- region/bounds or trigger identity;
- optional `DiscoveryRewardProfileId`;
- tags/telemetry metadata.

The runtime discovery authority owns:
- first-discovery detection;
- save persistence;
- authoritative multiplayer credit;
- party-wide/idempotent XP/reward granting;
- user-facing "Secret Discovered" presentation;
- reload/revisit behavior;
- analytics/telemetry.

Reward guidance:
- Discovery XP should normally be party/shared XP.
- Standard/Major optional content should usually include at least one reward whose usefulness does not strongly depend on player level: technique unlock, evergreen/mastery point, recipe, unique equipment property, etc.
- The exact evergreen progression resource/name is intentionally TBD.
- Tiny nooks do not all need permanent progression.
- Physical loot remains separate and may be absent.

## Voxel destruction and route bypass

Every authored secret route should declare an explicit bypass policy.

### `ProtectedShell`

The secret shell is protected from arbitrary voxel modification where needed to preserve the authored route.

Use for puzzles/mechanisms whose meaning depends on the intended access path.

### `AuthoredBreakablesOnly`

Specific barrier voxels are intentionally destructible; surrounding shell remains protected or constrained.

Use for breakable walls, collapsed masonry, boards, and other explicit destructive routes.

### `SystemicBypassAllowed`

Digging, climbing, destruction, construction, or another systemic action may create an alternate route. Entering the secret still counts as discovery.

Use when player creativity is intended to be part of the exploration game.

Validation should check:
- no trivial unintended one-voxel bypass under `ProtectedShell`;
- designated breakable ownership/resolution/collision is stable;
- `AuthoredBreakablesOnly` does not leak into surrounding protected geometry;
- progression-gating secrets do not allow systemic bypass unless explicitly configured;
- destruction does not leave interactable/discovery persistence in an inconsistent state.

## Examples

### Castle attic

**Secret:** Hidden attic room  
**Route:** Trapdoor  
**Clues:**
- exterior dormer/window implies inaccessible volume (`Spatial`/`Visual`);
- stair/floor geometry implies another level (`Navigation`/`Spatial`).

Likely bypass policy: `ProtectedShell` or `AuthoredBreakablesOnly`.

### Crypt

**Secret:** Hidden burial chamber  
**Route:** Pushable stone panel  
**Clues:**
- scrape marks at the panel (`Mechanical`/`Visual`);
- draft or muffled sound leak (`Environmental`/`Audio`).

### Cliff cave

**Secret:** Optional cave  
**Route:** Natural climb/swim route  
**Clues:**
- visible ledge (`Navigation`);
- footprints, water flow, or vegetation pattern (`Environmental`).

No interactable is required.

### Pressure-plate chamber

**Secret:** Alternate chamber beyond gated geometry  
**Route:** Pressure plate + pushable block changes traversal  
**Clues:**
- plate wear pattern;
- block scrape/track marks;
- visible but inaccessible opening.

At least one required clue must be observable before activation.

## Validation invariants

At generation/validation time:
- every `SecretId`, `SecretRouteId`, and `SecretClueId` is stable and unique;
- each secret has at least one valid route unless explicitly future-ability-gated;
- required clue count is satisfied;
- required clue-channel diversity is satisfied;
- required clues are pre-solve observable;
- no required clue has a circular dependency on its route;
- clue anchor and requested channel are compatible;
- same seed + same inputs produce the same plan;
- route bypass policy is honored;
- secrets intended for persistent credit include runtime registration metadata;
- multiple routes map back to one discovery identity.

At runtime/integration test level:
- revisiting/reloading does not duplicate rewards;
- multiplayer discovers/credits the party once;
- any legitimate route discovers the same secret;
- the SceneIssue hidden route visibly updates persistent secret state.

## Implementation stages

1. Add secret/route/clue planning contracts and metadata.
2. Add semantic clue anchors to world-feature authoring/templates.
3. Add deterministic clue candidate generation/scoring/selection.
4. Add structural validation and deterministic tests.
5. Bridge route plans onto reusable interactable descriptors/realizers.
6. Integrate stable `SecretId` registration with the canonical persistent discovery/reward authority once its API is verified.
7. Convert the Exploration Interactables & Secrets Showcase to consume these reusable paths rather than local secret logic.
8. Add representative generated castle/cave/ruin fixtures and replay tests.

## Test matrix

- same seed gives same route/clue plan;
- Standard secret receives required clue;
- Major secret receives independent clue channels;
- clue is perceivable from pre-solve state;
- impossible/circular clue fails validation;
- secret with multiple routes has one discovery identity;
- protected shell blocks accidental voxel bypass;
- authored breakable barrier works at intended resolution;
- systemic bypass still awards discovery;
- save/reload grants no duplicate reward;
- multiplayer party discovery credits once;
- SceneIssue hidden route updates persistent secret state.

## Open questions

1. What is the canonical runtime secret discovery store/API? The SceneIssue references `memory://secrets`, but ownership still needs verification.
2. What is the evergreen optional-content reward called and who owns it?
3. How should clue difficulty/subtlety be exposed to feature authors: named bands, constraint values, or both?
4. Which clue channels can be realized immediately (geometry/material) versus later systems (audio/narrative)?
5. Is protected voxel volume a general WorldBuilder constraint or specifically a secret-route constraint?
6. Which telemetry distinctions matter: secret discovered, route solved, clue observed, loot collected?

## Decision summary

- Adopt **Secret → Route(s) → Clue(s)**.
- WorldBuilder owns deterministic planning and validation.
- Reusable interactable runtime owns route behavior/state.
- Canonical persistent discovery authority owns save/discovery/rewards.
- Clues are semantic and may use independent sensory/spatial channels.
- Voxel bypass policy is explicit per authored route/secret.
- Important optional content should remain rewarding even when discovered late.
