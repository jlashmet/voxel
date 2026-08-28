# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed gap and acceptance
Kentridge has no playable quest using the quest framework already on `master`, and repository search found no reusable inventory subsystem. Add a recognizable version of the original `jlashmet/mounting-force` Kentridge kid-in-the-well quest. Completing it must grant one item exactly once. The player must be able to open/close a read-only inventory and see owned items as small square icon tiles.

## Existing foundation to reuse
- `Assets/Game/Quests/Api/QuestModel.cs`
- `Assets/Game/Quests/Runtime/QuestRuntime.cs`
- existing Story/Campaign quest lifecycle seams (`StartQuest`, active/completed state, `QuestCompleted`)
- current Kentridge interaction, WorldBuilder/composition, input, and UI paths

Do not create a parallel quest-state system or Kentridge-local quest booleans.

## Competing hypotheses / first discriminator
1. The quest model/runtime is complete enough, but authored `QuestDefinition`s are still not supplied to the live campaign runtime; the work is primarily composition and event routing.
2. The live campaign already owns `QuestRuntime`, and only Kentridge authoring/interaction observations are missing.

First trace Kentridge startup from its production composition root through `CampaignRuntime`, then prove whether a real `QuestDefinition` can be started and advanced by a normal NPC interaction. This determines whether shared Campaign composition must change before scene content is authored.

## Work
1. Inspect the legacy mounting-force Kentridge well quest before implementation. Preserve its recognizable NPC/well setup, trigger, progression, and dialogue where practical; port behavior, not the old Objective-C architecture.
2. Author the well quest with the existing quest API and compose it through Kentridge's standard WorldBuilder/gameplay path. Any missing reusable interaction/world primitive belongs in shared modules, not scene-local polling.
3. Finish reusable `QuestDefinition` → `QuestRuntime` composition/event routing only where the discriminator proves it missing. Normal gameplay interactions must advance the quest and completion must flow through the existing Story/Campaign lifecycle.
4. Add a minimal reusable item definition/identity and player inventory ownership API. Keep inventory state independent from UI. Define one quest reward and grant it idempotently on completion.
5. Add an inventory open/close action using existing input/menu conventions. Render owned items in a simple read-only grid of square icon tiles, with a fallback icon if content art is absent.

## Regression and verification
Add focused behavioral coverage for quest start/step/completion, interaction routing, inventory add/read, and exactly-once reward delivery. Add a UI/integration check for open/close and one tile per owned item. Run the built-application Kentridge harness and manually verify the complete quest → reward → inventory flow, plus startup/runtime health and blast radius of shared changes.

## Non-goals
Quest journal/editor, branching overhaul, equipment, item use/stats, crafting, vendors, drag/drop, sorting/filtering, advanced stacking, or unrelated persistence/multiplayer expansion.
