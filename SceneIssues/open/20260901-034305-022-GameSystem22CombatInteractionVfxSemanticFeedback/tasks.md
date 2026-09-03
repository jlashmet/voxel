# 22 Combat / interaction VFX & semantic feedback — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Vfx.Api` / `Game.Vfx.Runtime`
**Execution rule:** VFX presents confirmed/predicted semantic effects; authoritative combat eligibility, damage and world mutation remain in owning gameplay/world modules.

## API / cue model

- [ ] **T22-001 — Inventory current gameplay VFX.** Find hit/death/interaction particles, scene-local prefab spawning, voxel debris, world-destruction effects and any gameplay code carrying prefab/VFX identities.
- [ ] **T22-002 — Establish asmdefs.** Vfx.Api contains no prefab/ParticleSystem/VFX Graph types; Runtime owns Unity asset mapping/pooling/spawn.
- [ ] **T22-003 — Define semantic `VfxCueRef`.** Stable presentation cue identity independent of prefab/resource name.
- [ ] **T22-004 — Define semantic origin/context.** CharacterId/WorldObjectId/world point/direction only where needed, plus stable one-shot identity for dedupe.
- [ ] **T22-005 — Define persistent treatment descriptor.** Current semantic treatment state only for effects that must survive/reconstruct after reconnect.
- [ ] **T22-006 — Define missing/mapping failure behavior.** Presentation diagnostics only; no authoritative gameplay failure.
- [ ] **T22-007 — Define combat-chain presentation context.** Consume semantic action/event identity, parent/continuation relationship and join/redirect/transform outcome from Combat without re-evaluating combo eligibility in VFX.

## Runtime / integration

- [ ] **T22-010 — Implement cue-to-Unity-effect mapping.** Local configuration and pooling/lifetime behavior stay inside Vfx.Runtime.
- [ ] **T22-011 — Subscribe to authoritative result events.** Damage/defeat/interaction/encounter/combat-chain/world-alteration semantic adapters trigger VFX after confirmed results.
- [ ] **T22-012 — Separate cosmetic destruction from authoritative mutation.** Voxel/world systems commit real state; VFX may spawn debris/particles that cannot collide/damage/own world truth.
- [ ] **T22-013 — Resolve semantic origins through presentation bindings.** Missing visual object must not invalidate semantic event processing.
- [ ] **T22-014 — Implement prediction/confirmation dedupe.** Predicted anticipation may play locally only with stable reconciliation against authoritative cue identity.
- [ ] **T22-015 — Reconstruct persistent treatments from current state.** Reconnect/restore must not replay historical hit/interaction/combo one-shots.
- [ ] **T22-016 — Remove duplicate scene-local semantic effect spawners after parity.** Keep purely environmental decoration separate.
- [ ] **T22-017 — Preserve combo continuity visually.** When a character joins, redirects, or transforms/escalates an in-flight movement/projectile/impact/spell action, carry trajectory/ownership/impact continuity so the result reads as one evolving interaction rather than unrelated proc bursts.
- [ ] **T22-018 — Present spatial/world combo consequences.** Support readable launch/fall/collision, projectile redirection, ally augmentation and destructible-world consequence cues without encoding gameplay authority in effects.

## Verification

- [ ] **T22-020 — Cue mapping/unknown cue tests.** Deterministic config lookup and safe missing mapping.
- [ ] **T22-021 — Dedupe test.** Predicted + confirmed semantic event yields one visible effect.
- [ ] **T22-022 — Persistent reconstruction test.** Current state treatment recreates after reconnect while historical one-shots stay absent.
- [ ] **T22-023 — Authoritative destruction separation test.** Removing VFX cannot change voxel/world mutation result; cosmetic debris cannot create gameplay collisions/damage.
- [ ] **T22-024 — Headless regression.** Gameplay/domain tests pass with Vfx module absent.
- [ ] **T22-025 — Module-local built-player visual validation through shared harness.** Validate real production semantic event -> visible cue mapping.
- [ ] **T22-026 — Event-chain readability proof.** A representative primary action is joined and redirected or transformed by another character, and the resulting trajectory/impact/world consequence is visually attributable without relying on a list of status-proc labels.
- [ ] **T22-027 — Multi-character ordering/dedupe regression.** A branched or multi-participant chain produces one visible treatment per authoritative semantic event in deterministic event order with no duplicated predicted/confirmed effects.

## Cleanup / close

- [ ] **T22-030 — Remove prefab/VFX identities from gameplay contracts.** Repository-wide API search.
- [ ] **T22-031 — Remove VFX-owned gameplay mutation/duplicate effect paths.** Search particle/debris scripts for collision/damage/world writes not owned by gameplay modules.
- [ ] **T22-032 — Close with isolation proof.** Authoritative results are identical with VFX enabled or absent, combo-chain presentation does not own eligibility/order, and reconnect produces no historical replay.
