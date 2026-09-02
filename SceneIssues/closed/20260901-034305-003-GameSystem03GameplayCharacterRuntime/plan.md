# 03 Gameplay character runtime — implementation plan

**Target:** `Game.Characters.Api` / `Game.Characters.Runtime` with scene policy remaining in composition.
**Validated feature SHA:** `4416e89f17a4f2c3377a6905ccddc5d5faad74da`.

## Acceptance result

Implemented one engine-neutral gameplay character contract and deterministic runtime for stable `CharacterId`, semantic traits, lifecycle, bindings, kinematic snapshots, persistence capture/restore, and headless movement. Player, authored NPC/recruit, and forest-bandit enemy identities now bind into the same registry. Defeat is distinct from removal; Combat, Campaign, Sessions, WorldBuilder, Cutscenes, and presentation retain their separate policy/mechanics ownership.

Kentridge composition shares the registry through `KentridgeCharacterRegistryAnchor`. Its GameObjects, motors, and actor dictionaries are presentation/physics adapters that synchronize semantic state; they no longer provide persistent identity/lifecycle authority. The playable asmdef does not reference `Game.Characters.Runtime`; construction is isolated in the Characters-owned `Game.Characters.Composition` seam.

## Reuse and regression proof

`Game.Characters.Tests.CharacterRegistryTests` contains seven focused tests covering shared player/NPC/enemy composition, deterministic identity/binding failures and ordering, defeat/removal, persistence/tombstones, headless movement, and an independent non-Kentridge fixture.

Exact-SHA CI request `668267892702a3b8fbea9aac3908dd94015d3171` (run `33480997516`) passed the focused suite, repository-derived automatic module validation, and standalone SceneIssue replay for `KentridgePlayableSlice`.

## Blast radius / boundaries

No unrelated budgets or acceptance were changed. Existing session/network IDs, Combat participant IDs, Campaign recruitment state, authored `NpcRef`s, ambient-life state, and VoxelEngine character mechanics remain in their owning modules and are only semantically bound where demonstrated.

## Remaining gates

None. Close the SceneIssue, merge current `origin/master` if it advanced, and promote the exact resulting feature head to `master` non-force.
