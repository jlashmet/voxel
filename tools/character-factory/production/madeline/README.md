# Madeline base character

This production path builds Madeline as the **body character only**. Cleric robes, capes, weapons, and other equipment remain separate runtime-swappable assets.

The approved four-view turnaround is stored as compact JPEG references under `views/`. `prepare_body_texture_views.py` converts the tight beige modeling layer into a smooth skin-toned mannequin surface while preserving its silhouette, face, and hair. Those body-only references are used for **both Hunyuan3D multiview shape reconstruction and final source-color projection**, so neckline/short hems are not intentionally supplied to the mesh generator as clothing features.

Madeline also uses `CHARACTER_FACTORY_ALIGNMENT_BLEND=0.15` by default. The Character Factory's historical canonical alignment blend is `0.78`; that stronger value pulls generated bodies toward generic mannequin bounds. The lower Madeline-specific value preserves most of the approved shorter/compact body proportions while still aligning enough for canonical weight transfer. Other Character Factory builds retain the historical default unless they explicitly override the environment variable.

On Apple Silicon macOS:

```bash
bash tools/character-factory/production/madeline/build_macos.sh
```

The output `madeline_base_01.fbx` is rigged to the canonical gameplay skeleton and embeds `Idle`, `Walk`, `Run`, `Cast`, and `StaffAttack`. The build also stages the verified character into Unity's Character Factory import root. Clothing should be generated separately as `Clothing` parts using `RebindSkeleton`.
