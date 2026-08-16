# Madeline base character

Madeline is the woman shown in the Sunlit Cleric project references. This production
job deliberately creates **the reusable character body**, not the cleric costume.

## Product boundary

The final runtime composition is:

```text
Madeline body                 character
  + hair                      clothing (until skinned accessories are supported)
  + cleric under/over-robe    clothing
  + cape                      clothing
  + boots/gloves/armor        clothing or accessories as appropriate
  + sun staff                 weapon
```

The body source views must therefore contain only a modest, close-fitting neutral
underlayer (sports-bra/tank-style top + shorts/brief-style bottom). No robe, cape,
boots, jewelry, book pouch, staff, armor, or other silhouette-changing equipment may
be present in the body references.

This is intentional: generating the body from the existing clothed cleric turnaround
would bake the robe/cape silhouette into the character mesh and defeat runtime outfit
swapping.

## Identity rule: the face is authoritative

Madeline's face is the highest-priority identity feature. Shape generation and face
appearance are handled separately:

1. Hunyuan multiview reconstructs the neutral T-pose body/head geometry.
2. Character Factory transfers that mesh to the canonical skeleton.
3. `blender_project_face_texture.py` projects the approved high-resolution Madeline
   face crop onto Head-weighted, front-facing polygons of the rigged body.
4. Only after that identity pass is the FBX staged into Unity.

The projection source comes from the original Madeline artwork, not from an AI-painted
texture emitted by the 3D shape model. This keeps the important eyes, brows, nose,
mouth, skin tone, and facial markings tied to the source character.

## Required references

The build expects these files under `refs/`:

```text
madeline_body_front.png   neutral T-pose, minimal underlayer
madeline_body_back.png
madeline_body_left.png
madeline_body_right.png
madeline_face_front.png   tight, high-resolution crop from approved Madeline art
```

The four body references are *conditioning images*, not final textures. The face crop
is an appearance source and is projected again after rigging.

## Build

```bash
tools/character-factory/production/madeline/build.sh
```

The production script uses the Character Factory `character` pipeline with the Hunyuan
multiview `quality` preset, verifies that the result is skinned, applies the face
identity pass, verifies the post-processed FBX again, and stages the result under:

```text
Assets/Generated/CharacterFactory/character/madeline_body_01/
```

The generated character prefab remains compatible with the existing modular equipment
catalogue, so outfits can be authored and swapped independently.

## Acceptance criteria

A Madeline body build is acceptable only when all of these are true:

- robe/cape/staff/armor are not baked into the body mesh;
- the body has only a minimal neutral underlayer;
- canonical skeleton skinning passes the existing deformation verifier;
- the face material comes from `madeline_face_front.png`;
- eyes, eyebrows, nose, mouth and overall facial proportions read as the reference;
- the face projection has no obvious seam across the center/front of the face;
- the output can equip the existing Character Factory clothing/weapon parts without
  requiring a second body mesh.
