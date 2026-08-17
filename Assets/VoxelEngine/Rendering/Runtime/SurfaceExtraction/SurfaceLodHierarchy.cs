using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Coordinate relationships for the powers-of-two surface LOD hierarchy.
    /// Chunk coordinates are local to their source step. A parent at step 2S covers exactly
    /// eight children at step S. Parent mapping floors negative coordinates correctly.
    /// </summary>
    public static class SurfaceLodHierarchy
    {
        public const int FinestSourceStep = 1;
        public const int CoarsestSourceStep = 8;
        public const int ChildrenPerParent = 8;

        public static bool IsSupportedSourceStep(int sourceStep) =>
            sourceStep == 1 || sourceStep == 2 || sourceStep == 4 || sourceStep == 8;

        public static bool TryGetParentSourceStep(int childSourceStep, out int parentSourceStep)
        {
            if (!IsSupportedSourceStep(childSourceStep) || childSourceStep >= CoarsestSourceStep)
            {
                parentSourceStep = 0;
                return false;
            }
            parentSourceStep = childSourceStep * 2;
            return true;
        }

        public static bool TryGetChildSourceStep(int parentSourceStep, out int childSourceStep)
        {
            if (!IsSupportedSourceStep(parentSourceStep) || parentSourceStep <= FinestSourceStep)
            {
                childSourceStep = 0;
                return false;
            }
            childSourceStep = parentSourceStep / 2;
            return true;
        }

        public static int3 ParentCoordinate(int3 childCoordinate) => new(
            FloorDivideByTwo(childCoordinate.x),
            FloorDivideByTwo(childCoordinate.y),
            FloorDivideByTwo(childCoordinate.z));

        public static int3 ParentCoordinate(int3 childCoordinate,
                                            int childSourceStep,
                                            int parentSourceStep)
        {
            ValidateAdjacentSteps(childSourceStep, parentSourceStep);
            return ParentCoordinate(childCoordinate);
        }

        public static int3 ChildCoordinate(int3 parentCoordinate, int childIndex)
        {
            if ((uint)childIndex >= ChildrenPerParent)
                throw new ArgumentOutOfRangeException(nameof(childIndex), childIndex,
                    $"Child index must be in [0,{ChildrenPerParent - 1}].");
            return parentCoordinate * 2 + new int3(
                childIndex & 1,
                (childIndex >> 1) & 1,
                (childIndex >> 2) & 1);
        }

        public static int3 ChildCoordinate(int3 parentCoordinate,
                                           int parentSourceStep,
                                           int childSourceStep,
                                           int childIndex)
        {
            ValidateAdjacentSteps(childSourceStep, parentSourceStep);
            return ChildCoordinate(parentCoordinate, childIndex);
        }

        public static int ChildIndexWithinParent(int3 childCoordinate)
        {
            int3 parent = ParentCoordinate(childCoordinate);
            int3 offset = childCoordinate - parent * 2;
            return offset.x | (offset.y << 1) | (offset.z << 2);
        }

        private static int FloorDivideByTwo(int value)
        {
            int quotient = value / 2;
            int remainder = value % 2;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static void ValidateAdjacentSteps(int childSourceStep, int parentSourceStep)
        {
            if (!IsSupportedSourceStep(childSourceStep))
                throw new ArgumentOutOfRangeException(nameof(childSourceStep));
            if (!IsSupportedSourceStep(parentSourceStep))
                throw new ArgumentOutOfRangeException(nameof(parentSourceStep));
            if (parentSourceStep != childSourceStep * 2)
                throw new ArgumentException(
                    $"LOD hierarchy requires adjacent steps; child {childSourceStep}, parent {parentSourceStep}.");
        }
    }
}
