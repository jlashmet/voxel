# Plan

## Reopen reason
- The original ticket asked to hook the tuned ArchLookdev arch into Kentridge.
- Prior closure proved catalogue/program behavior but its saved visual evidence came from `ArchLookdev` / `Hero Arch Camera`, not the production Kentridge playable slice.
- The user loaded `KentridgePlayableSlice` and could not find or identify the result, so the visual acceptance gate was not actually satisfied.

## Required runtime discriminator
1. Launch the exact built application for `Assets/Scenes/KentridgePlayableSlice.unity`.
2. Starting from normal gameplay, locate at least one actual Kentridge landmark whose production WorldBuilder program uses the ArchLookdev-derived hero entrance treatment.
3. Approach it at player height and confirm the arch is reachable, visible, correctly attached to the building, and visually recognizable as the segmented projecting hero arch rather than merely containing hidden/generated seam primitives.
4. If no such arch can be found or read in normal gameplay, treat that as a product failure and fix the reusable Kentridge/WorldBuilder placement or presentation path. Do not place a showcase-only ArchLookdev object directly into the scene.

## Closure evidence
- Add durable built-player screenshots captured **inside `KentridgePlayableSlice`**, not ArchLookdev.
- At minimum include: (a) a wider shot showing recognizable Kentridge context/building and approach path; and (b) a closer player-height shot where the segmented voussoir/projecting arch treatment is plainly readable.
- Record the landmark identity/location or deterministic semantic anchor so another reviewer can reproduce the walk-up.
- Human visual inspection is mandatory. ArchLookdev screenshots, source inspection, catalogue assertions, primitive counts, or automated regressions alone cannot close the issue.
- Keep or strengthen the production-path regression so the verified landmark remains generated through the reusable WorldBuilder architecture vocabulary.

## Blast radius / cost
- Confirm any correction stays scoped to intended Kentridge landmark entrances and does not unintentionally apply the hero treatment to window-scale arches or unrelated settlements.
- Report any generation/rendering cost change if production geometry or placement is modified.
