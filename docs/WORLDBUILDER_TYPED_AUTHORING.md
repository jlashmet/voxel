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

Stable `RegionRef`, `SettlementRef`, `SiteRef`, `NpcRef`, `CutsceneRef`, and related values remain the compiler/runtime identity layer. They are useful for immutable plans, serialization, deterministic equality, diagnostics, save data, and networking. The designer-facing DSL should not use them as its normal relationship API.

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

The existing `RequireRegion`, `RequireSettlement`, `RequireSite`, `RequireNpc`, raw builders, and public stable `*Ref` constructors are currently retained as a low-level compatibility path because existing planner tests and integrations still construct identity values directly. New campaign content should use handles.

The intended migration is:

- [x] Add unforgeable reference-type handles for region, route, settlement, site, NPC, objective, and cutscene authoring.
- [x] Bind hierarchy ownership by nesting in the preferred DSL.
- [x] Hide `InRegion`, `PlaceAt`, raw-site spatial relations, and raw cutscene target binding from specialized authoring builders.
- [x] Add typed `PlayerSlot` cutscene targets.
- [x] Reject cross-campaign handles and cross-region route connections at authoring time.
- [x] Migrate the production known Kentridge opening to the typed DSL.
- [x] Keep compiled/runtime plans on stable `*Ref` identities.
- [ ] Migrate remaining campaign-content call sites from the legacy `Require*` path.
- [ ] Split test-only identity construction from public authoring identity construction.
- [ ] Make stable `*Ref` constructors non-public once remaining direct-construction callers have migrated.
- [ ] Remove or internalize legacy raw relationship builders once no production content depends on them.

The final target is a narrow public authoring surface where arbitrary `new SiteRef("...")`/`new RegionRef("...")` construction is no longer available to campaign content, while compiled plans retain stable deterministic IDs internally.
