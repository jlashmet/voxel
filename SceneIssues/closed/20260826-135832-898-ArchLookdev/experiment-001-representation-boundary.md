# Experiment 001 — representation boundary

**Hypothesis:** the close-up mismatch is caused by representing hero ivy/flowers as generic world-vegetation stamps, not by one more density/color/scale constant.

**Action / baseline:** inspected current `ArchReferenceGrowth`, the saved global `Hero Arch` capture metadata, and prior ivy/flower commits on baseline `57eab9da86a4ea751f8dcd0d18bd659a2951558f`.

**Result:** the captured runtime still routes hero growth through shared semantic card meshes. Prior passes already changed ivy growth class, clustering, color variation, flower readability, head scale, and placement; the new capture explicitly reports those changes still unlike the reference.

**Verdict:** confirmed. Move only ArchLookdev hero ivy/flowers to bounded art-directed combined meshes; retain shared vegetation for ordinary world growth and the two small ground ferns.

**Next:** exact-SHA PlayMode regression plus saved-pose replay; reject if the replay still reads as repeated strips/stars or exceeds the 3-draw/4k-vertex budget.
