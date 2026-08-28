# Experiment 014 — stem-free ivy masses and rounded bouquets

**Hypotheses.** H1: experiment 013 still reads as a diagonal garland because visible stem quads survive the final pass. H2: even with connectors removed, preserving the lush pass's radial leaf centres and original pointed petal geometry keeps the result sparse/flat rather than reference-quality.

**Captured runtime evidence.** Exact request `4df21b032625ecb5c51cabd6e2330442f160a3d9` / run `33139132235` failed the production regression with a surviving connector extent of `0.0827498` (required `<0.001`). Its real-player `verification-final.png` at the saved Hero Arch replay shows the same surviving diagonal line, repeated isolated cutout leaves, and oversized flat five-point blossoms. Shader/mesh absence is therefore falsified; the rendered meshes are present but composed incorrectly.

**Action.** Keep the existing 3 combined hero meshes/topology. In the final pass, collapse every known path/leaf stem quad plus a stem-color fallback, rebuild leaf centres from dense masonry-supported mass layouts with overlap/depth/drapes, and reconstruct each existing 7-vertex petal into a smaller rounded petal around three-head bouquets. No new renderers, vertices, GameObjects, or steady-state work.

**Discriminator / verdict gate.** The production regression must observe nonzero lush stems before the pass and zero visible stem extent after it, bounded larger left-vs-sparse-right leaf radii, non-flat flower depth, tighter three-head bouquets, deterministic rebuild reapplication, unchanged 3 draws, and `<=4096` hero vertices. Final acceptance still requires direct saved-pose replay inspection; green geometry alone is insufficient.
