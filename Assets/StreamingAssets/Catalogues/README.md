# Catalogues

A catalogue is **world identity**, not content you can tweak while a session runs.

The world's shape is derived from `(seed, catalogue)`. Every client generates the same world by
computing the same function, so two clients holding different catalogues silently generate
different worlds — the exact failure the constitution's first principle exists to prevent. The
catalogue hash is compared at join and a mismatch is refused rather than reconciled.

Consequences:

- Editing a catalogue between sessions moves or removes landmarks players remember, and
  invalidates every instance identity derived from it.
- Editing one mid-session is not supported. Reload the world.
- Reordering definitions changes their ids, and therefore every instance id in the world.

See `specs/002-world-feature-authoring/contracts/catalogue-format.md` for the format.
