# Madeline base character

This production path builds Madeline as the **body character only**. Cleric robes, capes, weapons, and other equipment remain separate runtime-swappable assets.

The approved four-view turnaround is stored as compact JPEG references under `views/`. The raw turnaround is used for Hunyuan3D multiview **shape reconstruction** so the body silhouette and proportions come from the approved reference. Before source-color projection, `prepare_body_texture_views.py` neutralizes the tight beige modeling layer into a smooth skin-toned mannequin surface. This prevents the modeling layer from becoming a visible clothing asset on the generated character while keeping the face and hair reference detail.

On Apple Silicon macOS:

```bash
bash tools/character-factory/production/madeline/build_macos.sh
```

The output `madeline_base_01.fbx` is rigged to the canonical gameplay skeleton and embeds `Idle`, `Walk`, `Run`, `Cast`, and `StaffAttack`. The build also stages the verified character into Unity's Character Factory import root. Clothing should be generated separately as `Clothing` parts using `RebindSkeleton`.
