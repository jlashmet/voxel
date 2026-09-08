using System;
using System.Collections.Generic;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Shared registry for canonical visual-review configurations. Art-direction/composition modules
    /// may register references and poses without teaching HouseShowcase about their semantic keys.
    /// </summary>
    public static class ArchitectureReviewRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, ArchitectureCanonicalReviewDescriptor> Reviews =
            new Dictionary<string, ArchitectureCanonicalReviewDescriptor>(StringComparer.Ordinal);

        public static bool TryRegister(ArchitectureCanonicalReviewDescriptor descriptor)
        {
            if (!descriptor.IsWellFormed)
                return false;
            lock (Gate)
            {
                if (Reviews.ContainsKey(descriptor.Key))
                    return false;
                Reviews.Add(descriptor.Key, descriptor);
                return true;
            }
        }

        public static bool TryGet(string key, out ArchitectureCanonicalReviewDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(key))
            {
                descriptor = default;
                return false;
            }
            lock (Gate)
                return Reviews.TryGetValue(key, out descriptor);
        }

        public static ArchitectureCanonicalReviewDescriptor[] All()
        {
            lock (Gate)
            {
                var result = new ArchitectureCanonicalReviewDescriptor[Reviews.Count];
                int index = 0;
                foreach (ArchitectureCanonicalReviewDescriptor review in Reviews.Values)
                    result[index++] = review;
                Array.Sort(result, (a, b) => string.CompareOrdinal(a.Key, b.Key));
                return result;
            }
        }
    }
}
