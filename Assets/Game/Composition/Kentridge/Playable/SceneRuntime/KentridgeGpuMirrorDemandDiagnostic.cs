using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-profile-only observation of GPU mirror coverage demand. It distinguishes pending
    /// blocks that still belong to a live requested footprint from stale recovery backlog left by a
    /// released footprint, and reports mirror residency/capacity so a dense relocation can separate
    /// true slot saturation from recovery-throughput or bookkeeping failures. The diagnostic never
    /// mutates renderer state and samples at a low cadence.
    /// </summary>
    [DefaultExecutionOrder(21000)]
    internal sealed class KentridgeGpuMirrorDemandDiagnostic : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float FirstSampleSeconds = 60f;
        private const float SampleIntervalSeconds = 10f;

        private static readonly BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private Type _coordinatorType;
        private FieldInfo _demandFootprintsField;
        private FieldInfo _pendingBlocksField;
        private FieldInfo _readyBlocksField;
        private FieldInfo _mixedReadyBlocksField;
        private FieldInfo _activeFootprintsField;
        private FieldInfo _activeExtractionCountField;
        private FieldInfo _mirrorField;
        private float _nextSampleSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge GPU Mirror Demand Diagnostic");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeGpuMirrorDemandDiagnostic>();
        }

        private void Awake()
        {
            _coordinatorType = FindCoordinatorType();
            if (_coordinatorType == null)
            {
                Debug.LogError("GPU_MIRROR_DEMAND_DIAG unavailable: coordinator-type-missing");
                enabled = false;
                return;
            }

            _demandFootprintsField = _coordinatorType.GetField("s_DemandFootprints", StaticPrivate);
            _pendingBlocksField = _coordinatorType.GetField("s_PendingBlocks", StaticPrivate);
            _readyBlocksField = _coordinatorType.GetField("s_ReadyBlocks", StaticPrivate);
            _mixedReadyBlocksField = _coordinatorType.GetField("s_MixedReadyBlocks", StaticPrivate);
            _activeFootprintsField = _coordinatorType.GetField("s_ActiveFootprints", StaticPrivate);
            _activeExtractionCountField = _coordinatorType.GetField("s_ActiveExtractionCount", StaticPrivate);
            _mirrorField = _coordinatorType.GetField("s_Mirror", StaticPrivate);
            if (_demandFootprintsField == null
                || _pendingBlocksField == null
                || _readyBlocksField == null
                || _mixedReadyBlocksField == null
                || _activeFootprintsField == null
                || _activeExtractionCountField == null
                || _mirrorField == null)
            {
                Debug.LogError("GPU_MIRROR_DEMAND_DIAG unavailable: coordinator-fields-missing");
                enabled = false;
                return;
            }

            _nextSampleSeconds = FirstSampleSeconds;
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextSampleSeconds) return;
            _nextSampleSeconds = Time.realtimeSinceStartup + SampleIntervalSeconds;
            Sample();
        }

        private void Sample()
        {
            object demands = _demandFootprintsField.GetValue(null);
            object pending = _pendingBlocksField.GetValue(null);
            object ready = _readyBlocksField.GetValue(null);
            object mixedReady = _mixedReadyBlocksField.GetValue(null);
            object active = _activeFootprintsField.GetValue(null);
            if (!(demands is IEnumerable demandEnumerable)
                || !(pending is IEnumerable pendingEnumerable)
                || !(ready is IEnumerable readyEnumerable)
                || !(mixedReady is IEnumerable mixedReadyEnumerable))
            {
                Debug.LogError("GPU_MIRROR_DEMAND_DIAG unavailable: coordinator-collections-missing");
                return;
            }

            var liveDemand = new HashSet<int3>();
            int demandFootprints = 0;
            int demandReferences = 0;
            foreach (object pair in demandEnumerable)
            {
                if (pair == null) continue;
                Type pairType = pair.GetType();
                object key = pairType.GetProperty("Key", InstanceAny)?.GetValue(pair);
                object value = pairType.GetProperty("Value", InstanceAny)?.GetValue(pair);
                if (key == null || !(value is int references)) continue;

                FieldInfo originField = key.GetType().GetField("Origin", InstanceAny);
                FieldInfo edgeField = key.GetType().GetField("Edge", InstanceAny);
                if (originField == null || edgeField == null) continue;
                if (!(originField.GetValue(key) is int3 origin)
                    || !(edgeField.GetValue(key) is int edge)
                    || edge <= 0)
                    continue;

                demandFootprints++;
                demandReferences += references;
                for (var z = 0; z < edge; z++)
                for (var y = 0; y < edge; y++)
                for (var x = 0; x < edge; x++)
                    liveDemand.Add(origin + new int3(x, y, z));
            }

            int pendingCount = 0;
            int pendingLive = 0;
            foreach (object boxed in pendingEnumerable)
            {
                if (!(boxed is int3 block)) continue;
                pendingCount++;
                if (liveDemand.Contains(block)) pendingLive++;
            }

            int readyLive = 0;
            foreach (object boxed in readyEnumerable)
            {
                if (boxed is int3 block && liveDemand.Contains(block)) readyLive++;
            }

            int mixedReadyCount = 0;
            int mixedReadyLive = 0;
            foreach (object boxed in mixedReadyEnumerable)
            {
                if (!(boxed is int3 block)) continue;
                mixedReadyCount++;
                if (liveDemand.Contains(block)) mixedReadyLive++;
            }

            int pendingStale = pendingCount - pendingLive;
            int activeFootprints = CollectionCount(active);
            int activeExtractions = (int)_activeExtractionCountField.GetValue(null);
            object mirror = _mirrorField.GetValue(null);
            int mirrorSlots = IntProperty(mirror, "SlotCapacity");
            int mirrorMixedResident = IntProperty(mirror, "ResidentBricks");
            int mirrorPinned = PinnedCount(mirror);
            ulong mirrorRefused = UlongProperty(mirror, "RefusedNoSlot");
            ulong mirrorEvictions = UlongProperty(mirror, "Evictions");
            ulong directoryRefusals = UlongProperty(mirror, "DirectoryRefusals");

            Debug.Log(
                "GPU_MIRROR_DEMAND_DIAG " +
                $"demandFootprints={demandFootprints} demandRefs={demandReferences} " +
                $"demandUnique={liveDemand.Count} pending={pendingCount} " +
                $"pendingLive={pendingLive} pendingStale={pendingStale} " +
                $"ready={CollectionCount(ready)} readyLive={readyLive} " +
                $"mixedReady={mixedReadyCount} mixedReadyLive={mixedReadyLive} " +
                $"mixedReadyInactive={mixedReadyCount - mixedReadyLive} " +
                $"mirrorSlots={mirrorSlots} mirrorMixedResident={mirrorMixedResident} " +
                $"mirrorPinned={mirrorPinned} mirrorRefused={mirrorRefused} " +
                $"mirrorEvictions={mirrorEvictions} directoryRefusals={directoryRefusals} " +
                $"activeFootprints={activeFootprints} activeExtractions={activeExtractions}");
        }

        private static int CollectionCount(object collection)
        {
            if (collection == null) return -1;
            PropertyInfo count = collection.GetType().GetProperty("Count", InstanceAny);
            return count != null && count.GetValue(collection) is int value ? value : -1;
        }

        private static int IntProperty(object target, string name)
        {
            if (target == null) return -1;
            PropertyInfo property = target.GetType().GetProperty(name, InstanceAny);
            return property != null && property.GetValue(target) is int value ? value : -1;
        }

        private static ulong UlongProperty(object target, string name)
        {
            if (target == null) return 0UL;
            PropertyInfo property = target.GetType().GetProperty(name, InstanceAny);
            return property != null && property.GetValue(target) is ulong value ? value : 0UL;
        }

        private static int PinnedCount(object mirror)
        {
            if (mirror == null) return -1;
            FieldInfo slotsField = mirror.GetType().GetField("_slots", InstanceAny);
            object slots = slotsField?.GetValue(mirror);
            return IntProperty(slots, "PinnedCount");
        }

        private static Type FindCoordinatorType()
        {
            const string fullName =
                "VoxelEngine.Rendering.Runtime.GpuVoxel.GpuSurfaceMirrorCoordinator";
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, throwOnError: false);
                if (type != null) return type;
            }
            return null;
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = null;
            string path = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            const string key = "\"validationProfile\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int colon = json.IndexOf(':', keyIndex + key.Length);
            int firstQuote = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int secondQuote = firstQuote >= 0 ? json.IndexOf('"', firstQuote + 1) : -1;
            if (firstQuote < 0 || secondQuote <= firstQuote) return false;
            profile = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            return true;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
    }
}
