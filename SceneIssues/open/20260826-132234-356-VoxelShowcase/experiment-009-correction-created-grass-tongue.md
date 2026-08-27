# Experiment 009 — correction-created grass tongue

## Hypothesis
The civic→upper taper is itself preserving the upper marked defect: the underlying urban shoulders are already Dirt, while the higher-precedence surface correction paints part of that continuous Dirt owner Moss.

## Runtime discriminator
Exact request `5c20c958bf3858857b2803c17c7bb1d7cdd80c28` / run `33092740624` passed the taper regression and completed a real-player replay. Direct inspection of the successful `verification-final.png` still shows the lower circle clean and the upper circle containing the hard-edged green tongue, so the taper is visually falsified rather than accepted.

## Source discriminator
At z=26.0 m, `upper-shoulder` already paints Dirt from its west edge x=82.8 m; `civic-summit` joins at x=84.8 m and also paints Dirt. The precedence-16 `PaintCivicToUpperWestTransition` nevertheless first paints the upper 72×72 dm west-shoulder overlap Moss and only then reclaims part of it as Dirt. Therefore the correction introduces a material discontinuity that does not exist in the district owner.

## Selected change / falsifier
Delete only that civic/upper shoulder override and return upper surface correction to its built-core-only behavior. A production PlayMode regression samples x=83/84/85 m at z=26 m, requires district Dirt ownership, requires no correction-layer surface override there, and verifies the core remains paved. If fresh saved-camera replay still shows the rectangle with resident sections, this hypothesis is false and the issue remains open.
