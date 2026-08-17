using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Selects optional scene slots deterministically and returns all selected slots in dependency
    /// order. Required slots and their dependency chains are mandatory and do not consume the
    /// optional budget. Optional dependencies do consume that budget.
    /// </summary>
    public static class DecorationSceneScheduler
    {
        public static bool TrySelectAndOrder(
            in DecorationContext context,
            uint sceneId,
            DecorationSceneSlot[] slots,
            int optionalBudget,
            out DecorationSceneSlot[] orderedSlots)
        {
            orderedSlots = new DecorationSceneSlot[0];
            if (!context.IsWellFormed || sceneId == 0 || optionalBudget < 0 ||
                !DecorationValidation.ValidateScene(slots, out _))
                return false;

            var selected = new bool[slots.Length];

            // Required slots imply their entire anchor chain, even when an anchor slot was
            // authored as optional. A required dependent cannot exist without its dependency.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Required)
                    MarkDependencyChain(slots, i, selected);
            }

            int remaining = optionalBudget;
            while (remaining > 0)
            {
                int bestIndex = -1;
                int bestCost = 0;
                ulong bestPriority = ulong.MaxValue;
                uint bestSlotId = uint.MaxValue;

                for (int i = 0; i < slots.Length; i++)
                {
                    if (selected[i] || slots[i].Required)
                        continue;

                    int cost = CountUnselectedOptionalDependencyChain(slots, i, selected);
                    if (cost <= 0 || cost > remaining)
                        continue;

                    ulong priority = WeightedPriority(in context, sceneId, in slots[i]);
                    if (priority < bestPriority ||
                        (priority == bestPriority && slots[i].SlotId < bestSlotId))
                    {
                        bestIndex = i;
                        bestCost = cost;
                        bestPriority = priority;
                        bestSlotId = slots[i].SlotId;
                    }
                }

                if (bestIndex < 0)
                    break;

                MarkDependencyChain(slots, bestIndex, selected);
                remaining -= bestCost;
            }

            int selectedCount = 0;
            for (int i = 0; i < selected.Length; i++)
                if (selected[i])
                    selectedCount++;

            var ordered = new DecorationSceneSlot[selectedCount];
            var emitted = new bool[slots.Length];
            int output = 0;

            // Preserve authoring order among otherwise-independent slots while guaranteeing that
            // every anchor is emitted before a dependent slot.
            while (output < selectedCount)
            {
                bool progressed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!selected[i] || emitted[i])
                        continue;

                    uint anchorSlotId = slots[i].AnchorSlotId;
                    if (anchorSlotId != 0 && !IsEmitted(slots, emitted, anchorSlotId))
                        continue;

                    ordered[output++] = slots[i];
                    emitted[i] = true;
                    progressed = true;
                }

                // ValidateScene guarantees an acyclic dependency graph, so this is defensive only.
                if (!progressed)
                    return false;
            }

            orderedSlots = ordered;
            return true;
        }

        private static ulong WeightedPriority(
            in DecorationContext context,
            uint sceneId,
            in DecorationSceneSlot slot)
        {
            uint seed = DecorationSeed.ForSlot(in context, sceneId, slot.SlotId);
            // Lower priority wins. Dividing a deterministic random key by Weight gives higher
            // authored weights proportionally more opportunities to win without mutable RNG state.
            return ((ulong)seed << 16) / slot.Weight;
        }

        private static int CountUnselectedOptionalDependencyChain(
            DecorationSceneSlot[] slots,
            int index,
            bool[] selected)
        {
            int count = 0;
            int current = index;
            while (current >= 0 && !selected[current])
            {
                if (!slots[current].Required)
                    count++;

                uint anchorSlotId = slots[current].AnchorSlotId;
                current = anchorSlotId == 0 ? -1 : FindSlot(slots, anchorSlotId);
            }
            return count;
        }

        private static void MarkDependencyChain(
            DecorationSceneSlot[] slots,
            int index,
            bool[] selected)
        {
            int current = index;
            while (current >= 0 && !selected[current])
            {
                selected[current] = true;
                uint anchorSlotId = slots[current].AnchorSlotId;
                current = anchorSlotId == 0 ? -1 : FindSlot(slots, anchorSlotId);
            }
        }

        private static bool IsEmitted(
            DecorationSceneSlot[] slots,
            bool[] emitted,
            uint slotId)
        {
            int index = FindSlot(slots, slotId);
            return index >= 0 && emitted[index];
        }

        private static int FindSlot(DecorationSceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].SlotId == slotId)
                    return i;
            return -1;
        }
    }
}
