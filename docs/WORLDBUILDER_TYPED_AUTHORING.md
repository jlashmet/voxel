# WorldBuilder Typed Authoring DSL

WorldBuilder authoring should make invalid relationship categories impossible to express in ordinary campaign code.

## Design rule

Strings are declaration-time stable IDs only:

```csharp
RegionHandle region = game.World.Region("kentridge-region");
SettlementHandle kentridge = region.Town("kentridge");
SiteHandle pub = kentridge.Pub("starting-pub");
NpcHandle madeline = pub.Npc("madeline");
```

After a thing is declared, relationships use the returned typed handle rather than repeating its ID or constructing a `*Ref`.

Stable `RegionRef`, `SettlementRef`, `SiteRef`, `NpcRef`, `CutsceneRef`, and related values remain the compiler/runtime identity layer. They are useful for immutable plans, serialization, deterministic equality, diagnostics, save data, and networking. Their string constructors are internal to `Game.WorldBuilder.Api`; ordinary authoring code cannot manufacture a semantic ref from an arbitrary string.

## Ownership by nesting

The preferred API fixes ownership structurally:

- routes are created from a `RegionHandle`;
- settlements are created from a `RegionHandle`;
- sites are created from a `RegionHandle` or `SettlementHandle`;
- NPCs are created from a `SiteHandle`;
- site-targeted objectives and cutscenes are created from a `SiteHandle`.

The specialized authoring builders deliberately do not expose ownership mutation methods such as `InRegion`, `PlaceAt`, or `At`. A Kentridge opening can therefore be authored as:

```csharp
var game = Campaign.Create("main-campaign");

RegionHandle region = game.World.Region("kentridge-region");
SettlementHandle kentridge = region.Town("kentridge");

SiteHandle pub = kentridge.Pub(
    "starting-pub",
    site => site.RequireCapability(SiteCapability.PlayerSpawn(4)));

SiteHandle destination = region.Site(
    "first-destination",
    site => site
        .DifferentSiteFrom(pub)
        .ReachableFrom(pub, TraversalProfile.NormalParty));

NpcHandle madeline = pub.Npc("madeline");
NpcHandle destinationNpc = destination.Npc(
    "destination-npc",
    npc => npc.RequireConversation());

CutsceneHandle intro = pub.Cutscene(
    KentridgeOpeningCutscene.Definition,
    scene => scene
        .Bind(KentridgeOpeningCutscene.Lead, PlayerSlot.First)
        .Bind(KentridgeOpeningCutscene.Madeline, madeline));
```

`SiteAuthoringBuilder` relationships accept `SiteHandle`, settlement connector authoring accepts `RouteHandle`, and typed cutscene authoring accepts `NpcHandle` or `PlayerSlot`. Passing a settlement where a site is required, a site where an NPC is required, or a raw cutscene target spec into the typed cutscene builder is therefore a compile-time error.

## What remains runtime validation

C# can encode relationship *kind*, but a dynamically declared object does not get a unique nominal C# type. WorldBuilder therefore still validates instance-level constraints such as:

- a handle came from the same campaign;
- a route connected to a settlement belongs to the same region;
- IDs are unique;
- complete-graph semantic requirements are mutually satisfiable;
- generated physical facts can realize the compiled requirements.

These checks should happen at the earliest layer that has enough information.

## Compatibility boundary

Stable `*Ref` types remain public because compiled plans, runtime adapters, diagnostics, save/network code, and tests need to carry those identities across assembly boundaries. Construction from strings is internal to `Game.WorldBuilder.Api`. `VoxelEngine.Tests.EditMode` is the only friend assembly and retains direct construction strictly for test fixtures.

The legacy `RequireRegion`, `RequireSettlement`, `RequireSite`, `RequireNpc`, raw objective/cutscene placement entry points, and their underlying builders remain available internally for compatibility tests and implementation reuse, but they are no longer public campaign-authoring entry points. Production campaign content must enter through the typed handle DSL. Public story rules and loot/secret authoring remain separate surfaces until typed facades exist for those domains.

The intended migration is:

- [x] Add unforgeable reference-type handles for region, route, settlement, site, NPC, objective, and cutscene authoring.
- [x] Bind hierarchy ownership by nesting in the preferred DSL.
- [x] Hide `InRegion`, `PlaceAt`, raw-site spatial relations, and raw cutscene target binding from specialized authoring builders.
- [x] Add typed `PlayerSlot` cutscene targets.
- [x] Reject cross-campaign handles and cross-region route connections at authoring time.
- [x] Migrate the production known Kentridge opening to the typed DSL.
- [x] Keep compiled/runtime plans on stable `*Ref` identities.
- [x] Migrate current production campaign-content call sites from the legacy `Require*` path.
- [x] Split test-only identity construction from public authoring identity construction.
- [x] Make stable `*Ref` constructors non-public.
- [x] Internalize legacy world ownership, site/NPC placement, objective target, and cutscene placement entry points that the typed handle DSL replaces.

The public authoring surface can no longer express arbitrary `new SiteRef("...")`/`new RegionRef("...")` construction or enter the old ownership/placement DSL. Stable deterministic refs still flow through compiled plans and runtimes; only WorldBuilder creates authored identities from strings.
