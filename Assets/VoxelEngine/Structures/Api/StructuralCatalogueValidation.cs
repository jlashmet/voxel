namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Authoring/load-time validation for the typed structural composition graph. It is deliberately
    /// pure over catalogue order: no global registry or discovery order can change the result.
    /// </summary>
    public static class StructuralCatalogueValidation
    {
        public static bool IsValid(in FeatureCatalogue catalogue)
        {
            for (int definitionId = 0; definitionId < catalogue.DefinitionCount; definitionId++)
            {
                FeatureDefinition definition = catalogue.Definitions[definitionId];
                if (definition.SlotOffset < 0 || definition.SlotCount < 0 ||
                    definition.SlotOffset + definition.SlotCount > catalogue.Slots.Length)
                    return false;

                if (!ValidPiece(in definition.StructuralPiece))
                    return false;

                for (int i = 0; i < definition.SlotCount; i++)
                {
                    SlotSpec slot = catalogue.Slots[definition.SlotOffset + i];
                    if (slot.SocketId == 0)
                        continue; // Legacy dormant slots remain loadable; runtime composes typed slots only.
                    if (!ValidTypedSlot(in catalogue, in slot))
                        return false;

                    for (int j = i + 1; j < definition.SlotCount; j++)
                    {
                        SlotSpec other = catalogue.Slots[definition.SlotOffset + j];
                        if (other.SocketId != 0 && other.SocketId == slot.SocketId)
                            return false;
                    }
                }
            }

            // Compute the longest outgoing structural path from every definition. Memoising this
            // path length is safe for shared DAG nodes because it is independent of the caller's
            // current depth; the previous "visited" memoisation was not and could hide a deeper
            // route that reached the same child later. The active recursion stack still catches
            // cycles deterministically.
            var state = new byte[catalogue.DefinitionCount];
            var longestPath = new int[catalogue.DefinitionCount];
            for (int i = 0; i < catalogue.DefinitionCount; i++)
            {
                if (!TryLongestPath(in catalogue, i, state, longestPath, out int depth) ||
                    depth > FeatureBudget.MaxCompositionDepth)
                    return false;
            }

            return true;
        }

        private static bool ValidPiece(in StructuralPieceSpec piece)
        {
            if (piece.PieceId == 0)
                return true;

            return piece.Role != StructuralSocketRole.None &&
                   piece.Offers != 0 && piece.Accepts != 0 &&
                   StructuralSocketValidation.IsCardinal(piece.Facing) &&
                   StructuralSocketValidation.HasValidBounds(piece.ClearanceMin, piece.ClearanceMax);
        }

        private static bool ValidTypedSlot(in FeatureCatalogue catalogue, in SlotSpec slot)
        {
            if (slot.DefinitionId < 0 || slot.DefinitionId >= catalogue.DefinitionCount)
                return false;
            if (slot.Role == StructuralSocketRole.None || slot.Offers == 0 || slot.Accepts == 0)
                return false;
            if (!StructuralSocketValidation.IsCardinal(slot.Facing))
                return false;
            if (!StructuralSocketValidation.HasValidBounds(slot.LocalMin, slot.LocalMax) ||
                !StructuralSocketValidation.HasValidBounds(slot.ClearanceMin, slot.ClearanceMax))
                return false;
            if (slot.CountMin < 0 || slot.CountMax < slot.CountMin || slot.Capacity == 0 ||
                slot.CountMax > slot.Capacity || slot.Spacing < 0)
                return false;

            bool needsSupport = (slot.Flags & (StructuralSocketFlags.RequireTerrainSupport |
                                               StructuralSocketFlags.RequireStructuralSupport)) != 0;
            if (needsSupport &&
                (!StructuralSocketValidation.HasValidBounds(slot.SupportProbeMin, slot.SupportProbeMax) ||
                 slot.MinimumSupportContacts == 0))
                return false;

            bool handsOffDecoration = (slot.Flags & StructuralSocketFlags.DecorationHandoff) != 0;
            if (handsOffDecoration != (slot.DecorationHandoff != StructuralDecorationHandoff.None))
                return false;

            FeatureDefinition child = catalogue.Definitions[slot.DefinitionId];
            return child.StructuralPiece.PieceId != 0 &&
                   StructuralSocketValidation.Compatible(in slot, in child.StructuralPiece) &&
                   StructuralSocketValidation.CanOrient(slot.Facing, child.StructuralPiece.Facing);
        }

        private static bool TryLongestPath(in FeatureCatalogue catalogue, int definitionId,
            byte[] state, int[] longestPath, out int depth)
        {
            if ((uint)definitionId >= (uint)catalogue.DefinitionCount)
            {
                depth = 0;
                return false;
            }

            if (state[definitionId] == 1)
            {
                depth = 0;
                return false;
            }
            if (state[definitionId] == 2)
            {
                depth = longestPath[definitionId];
                return true;
            }

            state[definitionId] = 1;
            int longest = 0;
            FeatureDefinition definition = catalogue.Definitions[definitionId];
            for (int i = 0; i < definition.SlotCount; i++)
            {
                SlotSpec slot = catalogue.Slots[definition.SlotOffset + i];
                if (slot.SocketId == 0)
                    continue;
                if (!TryLongestPath(in catalogue, slot.DefinitionId, state, longestPath, out int childDepth))
                {
                    depth = 0;
                    return false;
                }

                int throughChild = childDepth + 1;
                if (throughChild > longest)
                    longest = throughChild;
            }

            state[definitionId] = 2;
            longestPath[definitionId] = longest;
            depth = longest;
            return true;
        }
    }
}
