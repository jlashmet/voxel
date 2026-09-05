# PropShowcase plan

## Observed state

The game already owns reusable production content through several structure/decoration/world-object catalogues and presets, but there is no single visual browser that lets a developer inspect those entries one at a time. The requested feature is a dedicated `PropShowcase` scene with a left-side catalogue and right-side live preview.

## Acceptance

- `Assets/Scenes/PropShowcase.unity` opens in the built application and is registered for the repository's normal player-scene path.
- A scrollable left panel exposes every in-scope production prop/decoration with a readable label and visible selected state.
- Selection replaces the previous preview and renders the selected entry through its real production realization path.
- The preview provides stable grounding, production-compatible lighting, and automatic framing suitable for representative small/large and floor/wall/thin-surface/voxel/procedural/interactive content.
- The showcase list derives from canonical production sources; no duplicated showcase-only identity list may silently drift.
- Repeated selection does not accumulate geometry, colliders, lights, world-object state, or other runtime resources.
- Focused automated coverage plus exact built-player evidence proves enumeration, selection, switching, framing, and representative visual fidelity.

## Ownership / architecture

Primary ownership is expected under `Assets/Game/Structures` because the canonical decoration and prop realization pipeline lives there. Existing `WorldObjects` APIs may be consumed where independently previewable world-object props are part of the vocabulary. `PropShowcase` itself is an integration consumer under `Assets/Scenes`; it must not become content authority.

If existing catalogues are not safely enumerable, add the narrowest read-only semantic enumeration/query API to the owning production module. Keep preview orchestration separate from content definitions. Any changed player-visible/runtime module must own a focused `<Module>/Validation/` scene using the same production realization path.

## Competing hypotheses / first experiment

1. Existing catalogues/presets already expose enough stable identities and descriptors to build the browser entirely as an integration adapter.
2. Some content is reachable only through switch-based/specialized preset APIs, requiring a small canonical enumeration boundary before a complete showcase can exist.

First discriminate by inventorying every canonical catalogue/preset and mapping each independently previewable entry to its existing production realization. Record gaps before adding APIs.

## Blast radius / cost

Avoid changing geometry or art generation except where a demonstrated missing production realization prevents an already-catalogued prop from being previewed. Check scene startup cost, selection latency, object cleanup, and memory/resource growth across repeated switching.

## Baseline and remaining gates

Baseline: `d46e24f05337553883636b4f5b35228830269530`.

Remaining: catalogue inventory -> enumeration boundary if required -> scene/UI/preview composition -> module-local regression/validation -> built-player visual evidence -> exact-SHA targeted CI -> closure bookkeeping -> PR affected gate and merge.
