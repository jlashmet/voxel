# Experiment 007 — reference silhouette refinement

## Hypothesis
With world-space lifecycle fixed, the remaining mismatch is the authored 2D silhouette language: sharp five-point-like leaf lobes and identical radial flowers make the visible growth read as game stamps instead of the reference's softer ivy and delicate blossoms.

## Single product variable
Refine only the hero mesh silhouettes/colors. Preserve cluster coordinates, left/right mass, world-space lifecycle, semantic ground accents, three combined hero draws, and the <=4096-vertex budget.

## Action
- Replace the sharp leaf outline with a broader, shallower-notched ivy/heart profile and slightly lighter natural green range.
- Change flower heads from six uniform radial ellipses to five smaller, seed-varied tapered petals with a reduced warm centre so clusters read as delicate pink blossoms rather than daisies.
- Separately correct the PlayMode harness lookup to observe the detached `DontSave` root through `Resources.FindObjectsOfTypeAll`, filtered to the exact active production object. This is an observation fix, not a product repair.

## Falsifier
Reject if exact-SHA CI is not green, the player replay loses/relocates the growth, total hero geometry exceeds 4096 vertices, or manual pixel inspection still reads as star foliage / repeated flower stamps.

## Blast radius / cost
ArchLookdev only. No shared renderer changes and no new steady-state work; same lifecycle and three draw calls.
