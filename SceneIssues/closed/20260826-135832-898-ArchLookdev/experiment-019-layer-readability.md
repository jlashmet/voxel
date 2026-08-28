# Experiment 019 — leaf-layer and bouquet readability

## Captured runtime evidence
- Experiment 018 source `292b9e626e6ac6549e02070bf88dcfc460d48a0d`; exact request `6431ec56af9493a3d02846e37f02aeab73ef2204`; run `33147305207` completed successfully, including the focused regression and 45-second real-player replay.
- Direct inspection of `RealPlayer/verification-final.png` still rejects the candidate: the shelf silhouette is gone, but overlapping leaves merge into flat dark cutout blobs, flower heads remain repeated pink disks with conspicuous orange dots, and the crown is too weak to read as a layered ivy/bouquet mass.

## Competing hypotheses
1. **Position is still the primary defect — rejected.** Experiment 018 passes masonry envelopes, opening clearance, vertical/sloped spans, and separated zones while the player frame remains visually flat.
2. **More leaf/flower count is required — rejected.** The required 128 leaves and 30 heads are present; adding topology would increase cost without addressing overlap readability.
3. **Current.** Preserve topology/placement, but expose existing layers: slightly shrink individual leaf cards, strengthen per-leaf green/value and depth separation, restore only short local vine quads inside each mass, compact/enlarge flower heads into overlapping bouquets, reduce centre-dot scale, and increase blossom palette/value variation.

## Behavioral gate / cost
A final readability pass must keep the same meshes, 128 leaves, 30 heads, 3 draws, and vertex budget; prove smaller varied leaf cards, meaningful leaf depth/value range, bounded local vines rather than a diagonal garland, enlarged/overlapping flower heads with smaller centres and palette variation, and deterministic reapplication after rebuild. ArchLookdev only; no new renderers, vertices, GameObjects, or per-frame work after composition.
