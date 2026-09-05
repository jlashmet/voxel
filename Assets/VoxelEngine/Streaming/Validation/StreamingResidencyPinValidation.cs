using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Streaming.Api;
using VoxelEngine.Streaming.Runtime;

namespace VoxelEngine.Streaming.Validation
{
    public sealed class StreamingResidencyPinValidation : MonoBehaviour
    {
        private RegionTable _table;
        private BrickPool _pool;
        private RegionResidencyStore _store;
        private RegionStreamingService _streaming;
        private IRegionResidencyLease _lease;
        private int3 _region;
        private float _started;
        private bool _released;
        private string _status = "starting";

        private void Start()
        {
            EnsureCamera();
            _started = Time.unscaledTime;
            _table = new RegionTable(8, Allocator.Persistent);
            _pool = new BrickPool(32, Allocator.Persistent);
            _store = new RegionResidencyStore(in _table, in _pool);
            _streaming = new RegionStreamingService(_store);
            _region = new int3(6, 0, 6);
            _store.EnsureRegionResident(_region);

            _lease = ((IRegionResidencyPins)_streaming).AcquireResidency(new RegionLoadRequest(_region, 123u));
            Require(_lease.IsReady, "pre-resident region lease was not ready");
            int cursor = 0;
            int evicted = ResidencyManager.EvictFarResidents(float3.zero, 1, _store, ref cursor, 8);
            Require(evicted == 0 && _store.IsRegionResident(_region), "active pin failed to protect resident region");
            _status = "pinned region survived distance eviction";
            Debug.Log("STREAMING_PIN_VALIDATION pinned: ready=true evicted=0 resident=true");
        }

        private void Update()
        {
            if (_released || Time.unscaledTime - _started < 1f) return;
            _lease.Dispose();
            _lease = null;
            int cursor = 0;
            int evicted = ResidencyManager.EvictFarResidents(float3.zero, 1, _store, ref cursor, 8);
            Require(evicted == 1 && !_store.IsRegionResident(_region), "released region did not return to engine eviction policy");
            _released = true;
            _status = "released pin returned region to eviction policy";
            Debug.Log("STREAMING_PIN_VALIDATION released: evicted=1 resident=false");
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(30, 30, 700, 120), string.Empty);
            GUI.Label(new Rect(50, 50, 650, 30), "STREAMING • RESIDENCY PIN VALIDATION");
            GUI.Label(new Rect(50, 85, 650, 30), _status);
        }

        private void OnDestroy()
        {
            _lease?.Dispose();
            if (_table.IsCreated) _table.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) { Debug.LogError("STREAMING_PIN_VALIDATION failure: " + message); throw new InvalidOperationException(message); }
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Streaming Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.045f, 0.055f, 1f);
        }
    }
}
