# Experiment 002 — reject leaf-sensitive player collision

## Hypothesis
Reusing the existing projectile `TrySweepImpact` query from `CharacterMotor.IsBlocked` is sufficient to restore player collision with semantic trees.

## Method
Reviewed the exact implementation in feature commit `32ef9847eb3f901205cb5c8b0dd4a471063e7d91` against `ProceduralTreeDamageService.TrySweepImpact`.

The projectile query correctly tests both branch capsules and leaf anchors. That behavior is desirable for shooting because a projectile may strike foliage, but `CharacterMotor` uses the same query with a player-sized sweep radius. As a result, a character can be blocked by leaf anchors even when no trunk or branch intersects the character volume.

## Result
**Rejected before CI.** The intermediate change restores collision against tree wood, but it also changes the gameplay contract by making foliage behave like solid geometry.

The semantic-tree integration direction is still correct; the query semantics are not. Player movement needs a wood-only overlap that ignores leaves and respects already-removed branch subtrees.

## Decision
Replace the projectile sweep in `CharacterMotor` with a dedicated wood-only AABB overlap on the semantic tree interaction capability. The regression will prove that a representative tree trunk blocks a player-sized volume and that the same tree can still be hit and severed through the projectile/damage path.
