using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Reusable built-player evidence predicate for waypoints that must prove real vertical
    /// traversal. Horizontal route steering remains scene/runtime owned; this contract only decides
    /// whether the production motor's feet and grounded state satisfy an optional anchored Y band.
    /// </summary>
    public static class ShowcaseWaypointTraversalContract
    {
        public static bool Matches(
            float feetY,
            bool grounded,
            bool requireGrounded,
            bool hasVerticalAnchor,
            float verticalAnchorY,
            float expectedYOffset,
            float yTolerance)
        {
            if (requireGrounded && !grounded) return false;
            if (yTolerance < 0f) return true;
            if (!hasVerticalAnchor) return false;
            return Mathf.Abs(feetY - (verticalAnchorY + expectedYOffset)) <= yTolerance;
        }
    }
}
