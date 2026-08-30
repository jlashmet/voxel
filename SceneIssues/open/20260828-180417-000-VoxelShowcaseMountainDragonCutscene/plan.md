# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules must own the geometry and interaction. Current `AGENTS.md` requires player-visible work to be `production-quality`; the issue lowers that bar only for final dragon art, not the mountain/path. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, traversal, headroom, sparse upper-dragon bake coverage, and the exact CI-only native bake exit are proven. Run `33310677691` on source `2100df40287a...` passed the exact acceptance, fresh bake, Unity reopen, 17/17 production `AutoWalk -> CharacterMotor.Step` replay, and dialogue capture under unchanged 240 s / 14 GiB guards. Current master `dfbc43b086b6...` is merged as second parent of `fb7dd3b14853...`.

## New visual discriminator
Re-reviewing the exact six final-run frames under current master’s art rubric classifies the mountain/path as `prototype/blockout quality`: almost every landform/support surface uses the same bright masonry, the repeated rounded/frustum masses remain obvious, carved path faces read as retaining walls, and the dirt ramp has weak terrain/material integration. The cube itself remains acceptable because the issue explicitly permits placeholder dragon art.

Two plausible causes remain: (1) the single mountain material makes otherwise serviceable tapered masses read as repeated masonry primitives; (2) even with material separation, the silhouette/support proportions may still look procedural. Test (1) first because it is the smaller reusable change.

## Selected next change
Generalize the reusable mountain catalogue to distinct rock and ground-cover/support material roles while preserving the existing single-material overload. Keep the main core/cut faces rock; use ground cover on protruding shoulders and tapered path-support banks; keep the road material distinct. Compose VoxelShowcase with dark masonry rock + moss/ground cover + dirt road. Bump the startup landmark contract revision because rendered realization changes without layout dimensions. Add a focused program regression proving the role separation and unchanged support/traversal contract.

## Remaining gates
1. Implement and run the exact final PlayMode + built-player capture; require `production-quality` review or continue improving silhouette/supports.
2. Re-bake and source-match the new accepted payload/manifest; replace the stale checked-in startup image without weakening provenance or inventing another CI transport.
3. Only when every checklist/acceptance item is green, move only this issue `open -> pending -> closed`, set fixed metadata, merge then-current master, and push exact head to master non-force.
