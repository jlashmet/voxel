# Madeline production references

This directory intentionally does **not** use the original robe-clad turnaround as the
body input. The body conditioning set must first be normalized from the approved
Madeline artwork.

Required files:

```text
madeline_body_front.png
madeline_body_back.png
madeline_body_left.png
madeline_body_right.png
madeline_face_front.png
```

## Body views

All four body views must depict the same Madeline model with:

- neutral T-pose;
- orthographic/long-lens presentation with no perspective exaggeration;
- identical body proportions in every view;
- white or transparent background;
- bare feet;
- no jewelry, staff, book, armor, boots, robe, cape, belt, or loose fabric;
- only a simple close-fitting opaque neutral underlayer sufficient for modesty;
- hair pulled/contained so it does not become part of the torso silhouette.

The body images exist to reconstruct geometry only. They should not contain the cleric
costume because clothing is generated through the separate Character Factory
`clothing` product.

## Face identity image

`madeline_face_front.png` is more important than the body-view facial pixels. It must
be a square, front-facing neutral close-up derived from the approved Madeline/Sunlit
Cleric art, with:

- both eyes fully visible;
- forehead through chin present;
- ears/side face included where possible;
- no staff, hood, jewelry, hair strand, or scenery crossing the face;
- lighting flattened enough that a shadow is not mistaken for skin color;
- the original eye color, brows, nose, lips, complexion, and facial proportions
  preserved.

The production build projects this image directly onto the rigged Head region. Do not
replace it with a generic face texture merely because the Hunyuan mesh already has a
face-like appearance.
