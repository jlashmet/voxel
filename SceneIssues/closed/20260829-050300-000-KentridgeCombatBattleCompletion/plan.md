# Plan

## Evidence / target
- The assignment contains no captures or marked-region artifacts; the evidence is the manual freeze report and the existing Kentridge runtime path.
- `KentridgeForestBanditEncounter` begins a production `CombatService` session with one player and three enemies, then only ticks player movement input while the service is active.
- `CombatService` currently has participant positions and manual `CompleteCombat()`, but no health, attack/action progression, turn ownership, AI action, terminal evaluation, or autonomous teardown. A started Kentridge encounter therefore has no production path to battle completion.
- The richer chain-combat authority and tactical AI live in `Game.Combat.Runtime`, but are a separate combat model and are not composed into the Kentridge encounter.

## Competing hypotheses / discriminators
1. **Supported — missing production battle progression.** Once Kentridge activates `CombatService`, no runtime code can produce victory/defeat or call completion from battle rules.
2. **Rejected as primary — enemy reaction pause in chain combat.** Chain enemy AI intentionally pauses on `PendingReaction`, and existing tests prove players can pass/resolve and resume it; Kentridge does not use that coordinator at all.
3. **Rejected — async/animation wait.** The Kentridge production combat path has no async command/animation state to await.
4. **Rejected — scene installer lifecycle.** Existing Kentridge PlayMode coverage proves encounter installation and activation in the exact scene.

## Fix / regression approach
- Add a small engine-independent authoritative battle layer to the existing production `CombatService`: combatant HP, active turn, deterministic attack resolution, terminal outcome, and teardown invariants. Keep existing movement commands compatible.
- Add a deterministic AI battle driver that controls every active participant, chooses a living opposing target from a seeded RNG, executes exactly one legal action per step, and fails loudly if a step makes no progress.
- Compose the battle driver into the Kentridge encounter so once the forest ambush begins it advances instead of remaining permanently active; release the Combat input context when the terminal result is reached.
- Extend Kentridge PlayMode coverage with a full-battle AI-vs-AI regression that loads `KentridgePlayableSlice`, activates the real forest encounter, runs a fixed seed to a bounded terminal result, and asserts no active/pending battle work remains.
- Use the same deterministic auto-battle path for built-player validation of the exact Kentridge scene.

## Blast radius / cost
- Scope: `Game.Combat.Runtime`, Kentridge encounter composition, Kentridge focused PlayMode regression, and this issue's workflow evidence only.
- No scene/prefab serialization changes and no changes to chain-combat rules.
- Runtime cost is O(P) target selection per combat action for the four-participant Kentridge encounter; state is O(P). AI advances at a bounded cadence and stops entirely after terminal teardown.

## Verification gates
1. Focused regression compiles and repeatedly reaches the same terminal outcome/step count for a fixed seed.
2. Existing Kentridge encounter activation contract remains green.
3. Exact-SHA targeted CI runs the focused tests plus a built `KentridgePlayableSlice` replay/validation path.
4. Only after green exact-SHA CI: complete pending metadata, close the issue, merge current master, and advance master non-force.
