using System.Runtime.CompilerServices;

// ShowcaseWorld lives in Game.Composition.Showcase but is still built on this assembly's
// composition internals — the storage runtime lifetime it owns, and the feature-generation
// build/report types it drives. Moving it across the assembly boundary without exporting that
// surface left it unable to compile. Granting internals access keeps the surface unpublished
// while the relocation is finished properly, and matches how Storage.Api, Structures.Api and
// Rendering.Runtime already expose themselves to Composition.
[assembly: InternalsVisibleTo("Game.Composition.Showcase")]
