# Stylized textures

Source: [freestylized.com](https://freestylized.com) — royalty-free for commercial and
non-commercial use, attribution appreciated but not required. Redistribution *as texture assets*
(marketplaces, asset packs) is not permitted; using them inside this game is.

1K rather than 4K deliberately. At 10 cm voxels a wall face is a handful of texels across, so 4K
buys nothing visible and costs sixteen times the VRAM in a renderer that has to hold one texture
per material simultaneously.

`normal_gl` is the OpenGL convention (green up), which is what the raymarch shader expects.
