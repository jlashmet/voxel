# Experiment 001 — source selection

**Hypothesis:** a detailed, legitimately redistributable winged dragon can be sourced without weakening the issue's visual bar.

**Action / source SHA:** inspected current source listings and redistribution mirrors while feature head descended from `3a7e002be35d78bdfeb51b00d0dd67c094e06cf6`.

**Result:** selected Delatronic `Dragon` (Blend Swap 15891; historic 80766), CC-BY. Microsoft `DirectX-Graphics-Samples` preserves asset-specific CC-BY attribution and the Bitterli PBRT export. Scene metadata maps dragon material to four real binary PLY meshes (`Mesh008/013/014/015`) and teeth separately. Rejected the artist_71 model because it is wingless and Cethiel/Drummyfish because its mirror is visibly low-poly (~633 position vertices).

**Verdict:** source quality/licensing hypothesis supported. Byte-vendoring remains blocked by current connector limits on multi-megabyte binary blobs; do not substitute lower-quality geometry.

**Next:** vendor the exact selected source bytes when transport permits; then bake through the shared importer and measure fidelity/cost.
