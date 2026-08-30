using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures.Runtime
{
    public enum StructuralCompositionResult : byte
    {
        Ok = 0,
        RequiredSocketUnresolved = 1,
        Incompatible = 2,
        ClearanceBlocked = 3,
        MissingSupport = 4,
        CapacityExceeded = 5,
        DepthExceeded = 6,
        ChildBudgetExceeded = 7,
        PrimitiveBudgetExceeded = 8,
        SpatialExtentExceeded = 9,
        MalformedProgram = 10,
    }

    public enum StructuralAttachmentRejectReason : byte
    {
        None = 0,
        RequiredEmpty = 1,
        IncompatibleRoleOrTags = 2,
        OrientationMismatch = 3,
        ClearanceBlocked = 4,
        MissingTerrainSupport = 5,
        MissingStructuralSupport = 6,
        CapacityExceeded = 7,
        DepthExceeded = 8,
        ChildBudgetExceeded = 9,
        PrimitiveBudgetExceeded = 10,
        SpatialExtentExceeded = 11,
        MalformedDefinition = 12,
    }

    /// <summary>One independently bounded physical piece in a semantic composed structure.</summary>
    public struct StructuralInstance
    {
        public ulong SemanticStructureId;
        public ulong InstanceId;
        public int DefinitionId;
        public uint PieceId;
        public int ParentIndex;
        public uint ParentSocketId;
        public int Depth;
        public int3 Position;
        public byte Orientation;
        public int OverrideOffset;
        public int OverrideCount;

        public ExplicitPlacement Placement => new ExplicitPlacement
        {
            Position = Position,
            Orientation = Orientation,
            OverrideOffset = OverrideOffset,
            OverrideCount = OverrideCount,
        };
    }

    public struct StructuralAttachmentDecision
    {
        public ulong SemanticStructureId;
        public int ParentIndex;
        public uint SocketId;
        public uint ChildPieceId;
        public int3 Position;
        public byte Orientation;
        public bool Accepted;
        public StructuralAttachmentRejectReason Rejection;
    }

    public struct StructuralCompositionReport
    {
        public StructuralCompositionResult Result;
        public int ChildCount;
        public int PrimitiveCost;
        public int3 BoundsMin;
        public int3 BoundsMax;
        public ulong GraphHash;
    }

    /// <summary>
    /// Deterministically expands typed CallSlot declarations into physical child placements. The
    /// planner reads only catalogue data, seed and pure terrain queries; generated voxels and region
    /// order are never inputs.
    /// </summary>
    public static class StructuralCompositionPlanner
    {
        public static StructuralCompositionReport ExpandRoot(
            in FeatureCatalogue catalogue,
            uint terrainSeed,
            int rootDefinitionId,
            in ExplicitPlacement rootPlacement,
            NativeList<StructuralInstance> instances)
        {
            NativeList<StructuralAttachmentDecision> noDecisions = default;
            return ExpandRoot(in catalogue, terrainSeed, rootDefinitionId, in rootPlacement,
                instances, noDecisions);
        }

        public static StructuralCompositionReport ExpandRoot(
            in FeatureCatalogue catalogue,
            uint terrainSeed,
            int rootDefinitionId,
            in ExplicitPlacement rootPlacement,
            NativeList<StructuralInstance> instances,
            NativeList<StructuralAttachmentDecision> decisions)
        {
            instances.Clear();
            if (decisions.IsCreated) decisions.Clear();

            var report = new StructuralCompositionReport
            {
                Result = StructuralCompositionResult.Ok,
                BoundsMin = rootPlacement.Position,
                BoundsMax = rootPlacement.Position,
            };

            if ((uint)rootDefinitionId >= (uint)catalogue.DefinitionCount)
            {
                report.Result = StructuralCompositionResult.MalformedProgram;
                return report;
            }

            FeatureDefinition root = catalogue.Definitions[rootDefinitionId];
            ulong semanticId = FeatureHash.Cell(terrainSeed, rootDefinitionId, rootPlacement.Position);
            instances.Add(new StructuralInstance
            {
                SemanticStructureId = semanticId,
                InstanceId = semanticId,
                DefinitionId = rootDefinitionId,
                PieceId = root.StructuralPiece.PieceId,
                ParentIndex = -1,
                ParentSocketId = 0,
                Depth = 0,
                Position = rootPlacement.Position,
                Orientation = (byte)(rootPlacement.Orientation & 3),
                OverrideOffset = rootPlacement.OverrideOffset,
                OverrideCount = rootPlacement.OverrideCount,
            });
            report.PrimitiveCost = root.MaxPrimitives;
            ExpandBounds(ref report, rootPlacement.Position, OrientedFootprint(root.Footprint, rootPlacement.Orientation));
            report.GraphHash = HashInstance(FeatureHash.Mix(semanticId), instances[0]);

            for (int parentIndex = 0; parentIndex < instances.Length; parentIndex++)
            {
                StructuralInstance parent = instances[parentIndex];
                FeatureDefinition parentDefinition = catalogue.Definitions[parent.DefinitionId];
                if (parent.Depth >= FeatureBudget.MaxCompositionDepth && parentDefinition.SlotCount > 0)
                {
                    report.Result = StructuralCompositionResult.DepthExceeded;
                    return report;
                }

                for (int localSlotIndex = 0; localSlotIndex < parentDefinition.SlotCount; localSlotIndex++)
                {
                    SlotSpec slot = catalogue.Slots[parentDefinition.SlotOffset + localSlotIndex];
                    if (slot.SocketId == 0 || !ProgramCallsSlot(in catalogue, in parentDefinition, localSlotIndex))
                        continue;

                    ulong draw = FeatureHash.Mix(parent.InstanceId ^ slot.SocketId);
                    int count = slot.CountMin;
                    if (slot.CountMax > slot.CountMin)
                        count = FeatureHash.Range(ref draw, slot.CountMin, slot.CountMax);

                    if (count > slot.Capacity)
                    {
                        if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                            StructuralAttachmentRejectReason.CapacityExceeded,
                            StructuralCompositionResult.CapacityExceeded)) return report;
                        continue;
                    }

                    if (count == 0)
                    {
                        if ((slot.Flags & StructuralSocketFlags.Required) != 0)
                        {
                            Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.RequiredEmpty,
                                StructuralCompositionResult.RequiredSocketUnresolved);
                            return report;
                        }
                        continue;
                    }

                    for (int ordinal = 0; ordinal < count; ordinal++)
                    {
                        if (instances.Length - 1 >= FeatureBudget.MaxCompositionChildren)
                        {
                            Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.ChildBudgetExceeded,
                                StructuralCompositionResult.ChildBudgetExceeded);
                            return report;
                        }

                        if ((uint)slot.DefinitionId >= (uint)catalogue.DefinitionCount)
                        {
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.MalformedDefinition,
                                StructuralCompositionResult.MalformedProgram)) return report;
                            continue;
                        }

                        FeatureDefinition child = catalogue.Definitions[slot.DefinitionId];
                        if (!StructuralSocketValidation.Compatible(in slot, in child.StructuralPiece))
                        {
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.IncompatibleRoleOrTags,
                                StructuralCompositionResult.Incompatible)) return report;
                            continue;
                        }

                        if (!TryChildOrientation(parent.Orientation, slot.Facing,
                                child.StructuralPiece.Facing, out byte childOrientation))
                        {
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.OrientationMismatch,
                                StructuralCompositionResult.Incompatible)) return report;
                            continue;
                        }

                        int3 localAttach = DrawAttachPoint(in slot, ordinal, ref draw);
                        int3 worldAttach = parent.Position + RotatePoint(localAttach,
                            parentDefinition.Footprint, parent.Orientation);
                        int3 childIngress = RotatePoint(child.StructuralPiece.LocalPosition,
                            child.Footprint, childOrientation);
                        int3 childOrigin = worldAttach - childIngress;
                        int3 childFootprint = OrientedFootprint(child.Footprint, childOrientation);

                        if (!WithinCompositionExtent(instances[0].Position, childOrigin, childFootprint))
                        {
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.SpatialExtentExceeded,
                                StructuralCompositionResult.SpatialExtentExceeded)) return report;
                            continue;
                        }

                        if (ClearanceBlocked(in catalogue, instances, parentIndex, childOrigin,
                                childOrientation, in child.StructuralPiece))
                        {
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.ClearanceBlocked,
                                StructuralCompositionResult.ClearanceBlocked)) return report;
                            continue;
                        }

                        if (!SupportSatisfied(in slot, worldAttach, terrainSeed, in catalogue,
                                instances, parentIndex))
                        {
                            StructuralAttachmentRejectReason reason =
                                (slot.Flags & StructuralSocketFlags.RequireTerrainSupport) != 0
                                    ? StructuralAttachmentRejectReason.MissingTerrainSupport
                                    : StructuralAttachmentRejectReason.MissingStructuralSupport;
                            if (!Reject(ref report, decisions, in parent, parentIndex, in slot,
                                reason, StructuralCompositionResult.MissingSupport)) return report;
                            continue;
                        }

                        if (report.PrimitiveCost + child.MaxPrimitives > FeatureBudget.MaxCompositionPrimitiveCost)
                        {
                            Reject(ref report, decisions, in parent, parentIndex, in slot,
                                StructuralAttachmentRejectReason.PrimitiveBudgetExceeded,
                                StructuralCompositionResult.PrimitiveBudgetExceeded);
                            return report;
                        }

                        var childInstance = new StructuralInstance
                        {
                            SemanticStructureId = semanticId,
                            InstanceId = FeatureHash.Mix(draw ^ child.StructuralPiece.PieceId ^ (uint)ordinal),
                            DefinitionId = slot.DefinitionId,
                            PieceId = child.StructuralPiece.PieceId,
                            ParentIndex = parentIndex,
                            ParentSocketId = slot.SocketId,
                            Depth = parent.Depth + 1,
                            Position = childOrigin,
                            Orientation = childOrientation,
                            OverrideOffset = 0,
                            OverrideCount = 0,
                        };
                        instances.Add(childInstance);
                        report.ChildCount++;
                        report.PrimitiveCost += child.MaxPrimitives;
                        ExpandBounds(ref report, childOrigin, childFootprint);
                        report.GraphHash = HashInstance(report.GraphHash, childInstance);
                        AddDecision(decisions, in parent, parentIndex, in slot,
                            child.StructuralPiece.PieceId, childOrigin, childOrientation, true,
                            StructuralAttachmentRejectReason.None);
                    }
                }
            }

            return report;
        }

        private static bool Reject(ref StructuralCompositionReport report,
            NativeList<StructuralAttachmentDecision> decisions,
            in StructuralInstance parent, int parentIndex, in SlotSpec slot,
            StructuralAttachmentRejectReason reason, StructuralCompositionResult fatal)
        {
            AddDecision(decisions, in parent, parentIndex, in slot, 0, parent.Position, 0, false, reason);
            report.GraphHash = FeatureHash.Mix(report.GraphHash ^ slot.SocketId ^ (ulong)reason);
            if ((slot.Flags & StructuralSocketFlags.Required) == 0)
                return true;
            report.Result = fatal;
            return false;
        }

        private static void AddDecision(NativeList<StructuralAttachmentDecision> decisions,
            in StructuralInstance parent, int parentIndex, in SlotSpec slot, uint childPieceId,
            int3 position, byte orientation, bool accepted, StructuralAttachmentRejectReason rejection)
        {
            if (!decisions.IsCreated) return;
            decisions.Add(new StructuralAttachmentDecision
            {
                SemanticStructureId = parent.SemanticStructureId,
                ParentIndex = parentIndex,
                SocketId = slot.SocketId,
                ChildPieceId = childPieceId,
                Position = position,
                Orientation = orientation,
                Accepted = accepted,
                Rejection = rejection,
            });
        }

        private static bool ProgramCallsSlot(in FeatureCatalogue catalogue,
            in FeatureDefinition definition, int localSlotIndex)
        {
            return ProgramCallsSlot(in catalogue, definition.ProgramOffset,
                definition.ProgramOffset + definition.ProgramLength, localSlotIndex);
        }

        private static bool ProgramCallsSlot(in FeatureCatalogue catalogue, int start, int end, int slotIndex)
        {
            int pc = start;
            while (pc < end && pc < catalogue.Program.Length)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                if (length <= 0 || pc + length > end || pc + length > catalogue.Program.Length)
                    return false;
                if (op == ShapeOp.CallSlot && catalogue.Program[pc + 2] == slotIndex)
                    return true;

                if (op == ShapeOp.Repeat || op == ShapeOp.IfRange)
                {
                    int bodyInstructions = catalogue.Program[pc + length - 1];
                    int bodyStart = pc + length;
                    int bodyEnd = MeasureBodyEnd(in catalogue, bodyStart, end, bodyInstructions);
                    if (bodyEnd < 0) return false;
                    if (ProgramCallsSlot(in catalogue, bodyStart, bodyEnd, slotIndex)) return true;
                    pc = bodyEnd;
                }
                else
                {
                    pc += length;
                }
            }
            return false;
        }

        private static int MeasureBodyEnd(in FeatureCatalogue catalogue, int start, int end, int instructionCount)
        {
            int pc = start;
            for (int i = 0; i < instructionCount; i++)
            {
                if (pc >= end || pc >= catalogue.Program.Length) return -1;
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                if (length <= 0 || pc + length > end) return -1;
                pc += length;
                if (op == ShapeOp.Repeat || op == ShapeOp.IfRange)
                {
                    int nestedInstructions = catalogue.Program[pc - 1];
                    pc = MeasureBodyEnd(in catalogue, pc, end, nestedInstructions);
                    if (pc < 0) return -1;
                }
            }
            return pc;
        }

        private static int3 DrawAttachPoint(in SlotSpec slot, int ordinal, ref ulong draw)
        {
            if (slot.CountMax <= 1) return slot.LocalPosition;
            int3 min = slot.LocalMin;
            int3 max = slot.LocalMax;
            int x = FeatureHash.Range(ref draw, min.x, max.x);
            int y = FeatureHash.Range(ref draw, min.y, max.y);
            int z = FeatureHash.Range(ref draw, min.z, max.z);
            if (slot.Spacing > 0) x = min.x + ((x - min.x) / slot.Spacing) * slot.Spacing;
            return new int3(x, y, z);
        }

        private static bool TryChildOrientation(byte parentOrientation, Facing parentFacing,
            Facing childFacing, out byte orientation)
        {
            Facing worldParent = RotateFacing(parentFacing, parentOrientation);
            Facing target = StructuralSocketValidation.Opposite(worldParent);
            for (byte i = 0; i < 4; i++)
            {
                if (RotateFacing(childFacing, i) == target)
                {
                    orientation = i;
                    return true;
                }
            }
            orientation = 0;
            return false;
        }

        private static bool ClearanceBlocked(in FeatureCatalogue catalogue,
            NativeList<StructuralInstance> instances, int parentIndex, int3 childOrigin,
            byte childOrientation, in StructuralPieceSpec piece)
        {
            if (!NonEmpty(piece.ClearanceMin, piece.ClearanceMax)) return false;
            int3 childMin = childOrigin + RotateVector(piece.ClearanceMin, childOrientation);
            int3 childMax = childOrigin + RotateVector(piece.ClearanceMax, childOrientation);
            Normalize(ref childMin, ref childMax);

            for (int i = 0; i < instances.Length; i++)
            {
                if (i == parentIndex) continue;
                StructuralInstance other = instances[i];
                FeatureDefinition definition = catalogue.Definitions[other.DefinitionId];
                int3 otherMin = other.Position;
                int3 otherMax = other.Position + OrientedFootprint(definition.Footprint, other.Orientation);
                if (Overlaps(childMin, childMax, otherMin, otherMax)) return true;
            }
            return false;
        }

        private static bool SupportSatisfied(in SlotSpec slot, int3 worldAttach, uint terrainSeed,
            in FeatureCatalogue catalogue, NativeList<StructuralInstance> instances, int parentIndex)
        {
            if ((slot.Flags & StructuralSocketFlags.RequireTerrainSupport) != 0)
            {
                int3 min = worldAttach + slot.SupportProbeMin;
                int3 max = worldAttach + slot.SupportProbeMax;
                int contacts = 0;
                int y = max.y;
                int[] xs = { min.x, max.x };
                int[] zs = { min.z, max.z };
                for (int xi = 0; xi < 2; xi++)
                for (int zi = 0; zi < 2; zi++)
                {
                    int ground = TerrainQuery.HeightAt(xs[xi], zs[zi], terrainSeed);
                    if (ground >= min.y && ground <= y) contacts++;
                }
                if (contacts < slot.MinimumSupportContacts) return false;
            }

            if ((slot.Flags & StructuralSocketFlags.RequireStructuralSupport) != 0)
            {
                int3 probeMin = worldAttach + slot.SupportProbeMin;
                int3 probeMax = worldAttach + slot.SupportProbeMax;
                bool found = false;
                for (int i = 0; i < instances.Length; i++)
                {
                    if (i != parentIndex) continue;
                    StructuralInstance other = instances[i];
                    FeatureDefinition definition = catalogue.Definitions[other.DefinitionId];
                    int3 otherMax = other.Position + OrientedFootprint(definition.Footprint, other.Orientation);
                    if (Overlaps(probeMin, probeMax, other.Position, otherMax)) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        private static bool WithinCompositionExtent(int3 root, int3 childOrigin, int3 childFootprint)
        {
            int3 min = math.min(root, childOrigin);
            int3 max = math.max(root, childOrigin + childFootprint);
            int3 span = max - min;
            return span.x <= FeatureBudget.MaxCompositionExtentVoxels &&
                   span.y <= FeatureBudget.MaxCompositionExtentVoxels &&
                   span.z <= FeatureBudget.MaxCompositionExtentVoxels;
        }

        private static void ExpandBounds(ref StructuralCompositionReport report, int3 origin, int3 footprint)
        {
            report.BoundsMin = math.min(report.BoundsMin, origin);
            report.BoundsMax = math.max(report.BoundsMax, origin + footprint);
        }

        private static ulong HashInstance(ulong hash, in StructuralInstance instance)
        {
            hash = FeatureHash.Mix(hash ^ instance.PieceId);
            hash = FeatureHash.Mix(hash ^ (ulong)(uint)instance.DefinitionId);
            hash = FeatureHash.Mix(hash ^ (ulong)(uint)instance.Position.x);
            hash = FeatureHash.Mix(hash ^ (ulong)(uint)instance.Position.y);
            hash = FeatureHash.Mix(hash ^ (ulong)(uint)instance.Position.z);
            hash = FeatureHash.Mix(hash ^ instance.Orientation);
            hash = FeatureHash.Mix(hash ^ instance.ParentSocketId);
            return hash;
        }

        private static int3 OrientedFootprint(int3 footprint, byte orientation) =>
            (orientation & 1) == 0 ? footprint : new int3(footprint.z, footprint.y, footprint.x);

        private static Facing RotateFacing(Facing facing, byte orientation)
        {
            if (facing == Facing.Up || facing == Facing.Down) return facing;
            return (Facing)(((int)facing + orientation) & 3);
        }

        private static int3 RotateVector(int3 vector, byte orientation) =>
            (orientation & 3) switch
            {
                1 => new int3(-vector.z, vector.y, vector.x),
                2 => new int3(-vector.x, vector.y, -vector.z),
                3 => new int3(vector.z, vector.y, -vector.x),
                _ => vector,
            };

        private static int3 RotatePoint(int3 p, int3 footprint, byte orientation)
        {
            int maxX = footprint.x - 1;
            int maxZ = footprint.z - 1;
            return (orientation & 3) switch
            {
                1 => new int3(maxZ - p.z, p.y, p.x),
                2 => new int3(maxX - p.x, p.y, maxZ - p.z),
                3 => new int3(p.z, p.y, maxX - p.x),
                _ => p,
            };
        }

        private static bool NonEmpty(int3 min, int3 max) => math.any(max > min);
        private static bool Overlaps(int3 aMin, int3 aMax, int3 bMin, int3 bMax) =>
            aMin.x < bMax.x && aMax.x > bMin.x &&
            aMin.y < bMax.y && aMax.y > bMin.y &&
            aMin.z < bMax.z && aMax.z > bMin.z;

        private static void Normalize(ref int3 min, ref int3 max)
        {
            int3 originalMin = min;
            min = math.min(originalMin, max);
            max = math.max(originalMin, max);
        }
    }
}
