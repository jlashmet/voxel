# Madeline base character

Madeline is the woman shown in the Sunlit Cleric references. This production job creates **the reusable character body**, not the cleric costume.

## Product boundary

The runtime composition remains modular:

```text
Madeline body                 character
  + hair                      generated with the body for now
  + cleric robe / cape        clothing
  + boots / gloves / armor    clothing or accessories
  + sun staff                 weapon
```

Robe, cape, boots, jewelry, book pouch, staff, armor, and other equipment must never be baked into the Madeline body mesh.

## Approved body reference

`views/front.jpg`, `back.jpg`, `left.jpg`, and `right.jpg` are the approved turnaround selected for this character. They intentionally use a very close-fitting neutral beige modeling layer so body silhouette is readable from all directions.

That layer is **not** treated as runtime clothing. `prepare_body_texture_views.py` converts it to a smooth skin-toned mannequin surface before Hunyuan sees the images. The converted views are used for both multiview geometry generation and body/hair source-color projection. This avoids deliberately feeding neckline or shorts-hem features into the body mesh while retaining the approved silhouette.

The approved silhouette is shorter and more compact than the generic canonical mannequin. Madeline therefore builds with `CHARACTER_FACTORY_ALIGNMENT_BLEND=0.15`. The Character Factory historical default is `0.78`, which pulls generated bounds much more strongly toward the generic donor. The lower value keeps most Madeline-specific body proportions while still providing enough canonical alignment for skin-weight transfer.

## Face identity is authoritative

Madeline's face is the highest-priority identity feature. The small original face artwork is stored as `refs/madeline_face_front.png` and is applied after body reconstruction and body/hair texturing:

1. Hunyuan multiview reconstructs the clothing-free body/head geometry from the approved turnaround.
2. Character Factory transfers it to the canonical gameplay skeleton and embeds the gameplay actions.
3. Multiview source projection restores body/hair appearance from the approved turnaround.
4. `blender_project_face_texture.py` projects the original Madeline face onto Head-weighted front-facing polygons.
5. Strict rig and animation verification runs after the final appearance pass.
6. The final FBX is staged into Unity through the normal Character Factory importer.

This keeps the eyes, brows, nose, mouth, skin tone, and overall facial read tied to the original Madeline art rather than trusting an AI-painted generated texture as identity ground truth.

## Build

On the Apple Silicon self-hosted Mac:

```bash
bash tools/character-factory/production/madeline/build.sh
```

`build_macos.sh` is only a compatibility wrapper around the same production build.

Primary outputs under `Artifacts/MadelineProduction/`:

```text
madeline_body_01.fbx                 final skinned/animated character
madeline_body_01.geometry_only.fbx   pre-source-texture rigged geometry
madeline_body_01.body_basecolor.png  deterministic four-view source atlas
madeline_body_01.render.png           bind-pose lookdev proof
madeline_body_01.idle.png             animated lookdev proof
body-only-reference-report.json       modeling-layer removal audit
manifest.json                         Character Factory staging contract
```

The FBX embeds `Idle`, `Walk`, `Run`, `Cast`, and `StaffAttack`.

Unity staging target:

```text
Assets/Generated/CharacterFactory/character/madeline_body_01/
```

## Acceptance criteria

A Madeline body build is acceptable only when all of these are true:

- robe, cape, staff, boots, armor, belts, jewelry, and book pouch are absent from the base mesh;
- the temporary neutral modeling layer does not read as clothing in generated geometry or final body texture;
- body silhouette still reads as the approved shorter/compact Madeline proportions rather than the generic mannequin;
- canonical skeleton skinning passes the existing verifier;
- all five gameplay actions survive final FBX export;
- face appearance comes from `madeline_face_front.png`;
- eyes, brows, nose, mouth, hair, and overall facial proportions read as Madeline;
- the face projection has no obvious center/front seam;
- the result can equip Character Factory clothing and weapon parts without requiring another body mesh.
