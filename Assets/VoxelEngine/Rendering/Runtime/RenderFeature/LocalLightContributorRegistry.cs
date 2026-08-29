using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Renderer-level keyed local-light contributors. Scene/application systems can register
    /// independent light sets without sharing another feature's presentation storage. The bridge
    /// composes these contributors with the caller-owned base lights only when a source changes;
    /// render frames remain allocation-free.
    /// </summary>
    public static class LocalLightContributorRegistry
    {
        private readonly struct Entry
        {
            public readonly Vector4[] Lights;
            public readonly Vector4[] Colours;

            public Entry(Vector4[] lights, Vector4[] colours)
            {
                Lights = lights ?? Array.Empty<Vector4>();
                Colours = colours ?? Array.Empty<Vector4>();
            }

            public int Count => Math.Min(Lights.Length, Colours.Length);
        }

        private static readonly Dictionary<string, Entry> s_entries = new(StringComparer.Ordinal);

        public static event Action Changed;

        public static int ContributorCount => s_entries.Count;

        public static void Set(string key, Vector4[] lights, Vector4[] colours)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A local-light contributor requires a stable non-empty key.", nameof(key));
            if (lights == null) throw new ArgumentNullException(nameof(lights));
            if (colours == null) throw new ArgumentNullException(nameof(colours));
            if (lights.Length != colours.Length)
                throw new ArgumentException("Local-light positions and colours must have identical lengths.");

            s_entries[key] = new Entry((Vector4[])lights.Clone(), (Vector4[])colours.Clone());
            Changed?.Invoke();
        }

        public static bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key) || !s_entries.Remove(key)) return false;
            Changed?.Invoke();
            return true;
        }

        public static void Clear()
        {
            if (s_entries.Count == 0) return;
            s_entries.Clear();
            Changed?.Invoke();
        }

        internal static void Compose(
            Vector4[] baseLights,
            Vector4[] baseColours,
            out Vector4[] lights,
            out Vector4[] colours)
        {
            int baseCount = Math.Min(baseLights?.Length ?? 0, baseColours?.Length ?? 0);
            int count = baseCount;
            foreach (Entry entry in s_entries.Values)
                count += entry.Count;

            if (count == 0)
            {
                lights = Array.Empty<Vector4>();
                colours = Array.Empty<Vector4>();
                return;
            }

            lights = new Vector4[count];
            colours = new Vector4[count];

            // Feature contributors go first. The shader has a fixed local-light cap, so appending
            // feature lights after a large base landmark set can make an independently registered
            // cave contribute zero rendered lights. The base layer fills whatever capacity remains.
            int output = 0;
            foreach (Entry entry in s_entries.Values)
            {
                int entryCount = entry.Count;
                if (entryCount == 0) continue;
                Array.Copy(entry.Lights, 0, lights, output, entryCount);
                Array.Copy(entry.Colours, 0, colours, output, entryCount);
                output += entryCount;
            }

            if (baseCount > 0)
            {
                Array.Copy(baseLights, 0, lights, output, baseCount);
                Array.Copy(baseColours, 0, colours, output, baseCount);
            }
        }
    }
}
