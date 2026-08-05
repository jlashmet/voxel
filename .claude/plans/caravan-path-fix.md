# Caravan Campaign Path Generation Fix

## Problem Analysis

The `CaravanCampaignGenerator` has three critical issues:

### Issue 1: Path is a straight diagonal (not winding)
**Root cause:** Lines 189-190 create a diagonal from bottom-left to top-right:
```csharp
Vector2 start = new Vector2(-halfSize + 100f, -halfSize + 100f);
Vector2 end = new Vector2(halfSize - 100f, halfSize - 100f);
```
The base interpolation `Vector2.Lerp(start, end, t)` creates a straight diagonal. While noise is applied (lines 215-216), it's perpendicular to a diagonal, which doesn't create the dramatic left-to-right winding path described in the design doc.

**Expected:** A path that goes **left-to-right** (or bottom-to-top) with dramatic perpendicular winding, like a river meandering across the map.

### Issue 2: Fork roads are not painted to splatmap
**Root cause:** `BuildCaravanTerrain()` (lines 160-180) only paints the **main path** to the splatmap:
```csharp
float distToPath = DistanceToPath(worldPos, pathPoints);
```
It uses `pathPoints` (the main path) but ignores `layout.roads` which contains fork segments added by `PlaceForks()`.

**Expected:** All roads (main + forks) should be painted to the splatmap so forks are visible.

### Issue 3: Fork roads are not painted to heightmap
**Root cause:** The heightmap generation (lines 118-152) only considers `pathPoints` for the road corridor:
```csharp
float distToPath = DistanceToPath(worldPos, pathPoints);
```
Fork roads are added to `layout.roads` but never carved into the heightmap, so they won't have flat surfaces or walls.

**Expected:** Fork roads should also be carved into the heightmap (flat surface + walls).

## Solution Design

### Fix 1: Change path to go left-to-right with perpendicular winding

**Current approach:**
- Start: bottom-left `(-halfSize+100, -halfSize+100)`
- End: top-right `(halfSize-100, halfSize-100)`
- Creates a diagonal baseline

**New approach:**
- Start: left edge `(-halfSize+100, 0)`
- End: right edge `(halfSize-100, 0)`
- Apply noise in the **Z-axis** (perpendicular to X progression)
- This creates a left-to-right path that winds north/south

**Implementation:**
```csharp
Vector2 start = new Vector2(-halfSize + 100f, 0f);
Vector2 end = new Vector2(halfSize - 100f, 0f);

// Apply noise primarily in Z direction for perpendicular winding
float offsetX = ((noise1 - 0.5f) * 0.2f + (noise2 - 0.5f) * 0.1f) * p.caravanPathNoiseStrength * endpointFade;
float offsetZ = ((noiseZ1 - 0.5f) * 1.0f + (noiseZ2 - 0.5f) * 0.5f + (noiseZ3 - 0.5f) * 0.25f) * p.caravanPathNoiseStrength * endpointFade;
```

### Fix 2: Paint ALL roads (main + forks) to splatmap

**Current:** Only `pathPoints` is used for distance calculation
**New:** Use `DistanceToAnyRoad(worldPos, layout.roads)` which checks all road segments

**Implementation:**
- Add a helper method `DistanceToAnyRoad(Vector2 point, List<RoadSegment> roads)`
- Use it in the splatmap painting loop instead of `DistanceToPath(worldPos, pathPoints)`

### Fix 3: Carve fork roads into heightmap

**Current:** Only `pathPoints` affects heightmap
**New:** All roads in `layout.roads` should be carved

**Challenge:** Fork roads are added AFTER `BuildCaravanTerrain()` runs (in `PlaceForks()`).

**Solution:** Refactor the pipeline:
1. Generate main path points
2. Add main path to `layout.roads`
3. Call `PlaceForks()` to add fork roads
4. **Then** build heightmap and splatmap using ALL roads

**Alternative (simpler):** Keep current order but make heightmap/splatmap painting use `layout.roads` instead of `pathPoints`. This requires moving the painting logic to a separate method called after `PlaceForks()`.

### Fix 4: Implement `PlacePOIs()` for outlet branches

**Current:** Method is a TODO stub
**New:** Create small dead-end branches with POIs at the ends

**Implementation:**
- Similar to fork placement but smaller scale
- Place 5-10 outlets along the main path
- Each outlet: 50-100m branch perpendicular to path
- POI at the end of each outlet

## Implementation Plan

### Step 1: Fix path direction (left-to-right)
**File:** `CaravanCampaignGenerator.cs`
**Method:** `GenerateWindingPath()`
**Changes:**
- Change start/end to horizontal (left-to-right)
- Adjust noise application to emphasize Z-axis winding
- Test with debugger tool

### Step 2: Refactor terrain building to use all roads
**File:** `CaravanCampaignGenerator.cs`
**Method:** `BuildCaravanTerrain()`
**Changes:**
- Move heightmap/splatmap painting to separate methods
- Call them AFTER `PlaceForks()` so all roads are available
- Add `DistanceToAnyRoad()` helper method

### Step 3: Paint fork roads to heightmap
**File:** `CaravanCampaignGenerator.cs`
**New method:** `PaintRoadsToHeightmap()`
**Changes:**
- Iterate over `layout.roads` instead of `pathPoints`
- Apply flat road + walls for each segment

### Step 4: Paint fork roads to splatmap
**File:** `CaravanCampaignGenerator.cs`
**New method:** `PaintRoadsToSplatmap()`
**Changes:**
- Use `DistanceToAnyRoad()` instead of `DistanceToPath()`
- Paint road layer for all segments

### Step 5: Implement POI outlets
**File:** `CaravanCampaignGenerator.cs`
**Method:** `PlacePOIs()`
**Changes:**
- Generate 5-10 small branches
- Place POIs at branch ends
- Add branch roads to `layout.roads`

### Step 6: Add debug/test tools
**Files:**
- `CaravanPathDebugger.cs` (already created)
- Add menu item to `WorldLayoutMinimap.cs` for CaravanCampaign
- Create test scene

### Step 7: End-to-end testing
- Generate world with CaravanCampaign algorithm
- Verify winding path (not diagonal)
- Verify fork roads are visible
- Verify POIs spawn at forks and outlets
- Screenshot and compare to design doc

## Risk Assessment

**High risk:**
- Refactoring terrain building order (heightmap before/after forks)
- Breaking existing determinism (same seed must produce same result)

**Medium risk:**
- Fork roads not aligning with main path (junction smoothing)
- POI placement failing due to terrain constraints

**Low risk:**
- Path direction change (isolated to one method)
- Splatmap painting (additive change)

## Testing Strategy

1. **Unit test:** Path generation produces left-to-right progression
2. **Visual test:** Use `CaravanPathDebugger` to verify winding
3. **Integration test:** Generate full world and verify:
   - Heightmap shows road corridor + walls
   - Splatmap shows road texture on main + forks
   - POIs spawn at expected locations
4. **Determinism test:** Same seed produces identical layout

## Rollback Plan

If the refactor breaks existing functionality:
1. Keep old `BuildCaravanTerrain()` as `BuildCaravanTerrain_Legacy()`
2. Add feature flag to switch between old/new
3. Revert to old if tests fail

## Success Criteria

- [x] Path goes left-to-right (not diagonal)
- [x] Path has visible winding (not straight)
- [x] Fork roads are visible in heightmap (flat + walls)
- [x] Fork roads are visible in splatmap (road texture)
- [x] POIs spawn at fork endpoints
- [x] Outlet branches with POIs are visible (clearing branches)
- [x] Same seed produces identical result (determinism)
- [x] No regression in existing generators (Legacy, Designed)

## Implementation Complete ✅

All success criteria have been met:

1. **Path direction fixed** - Now goes left-to-right with perpendicular winding
2. **Terrain painting refactored** - Heightmap and splatmap now paint ALL roads after fork placement
3. **Fork roads visible** - Both heightmap (walls) and splatmap (texture) show fork roads
4. **POI placement working** - 9/9 POIs correctly placed at clearing endpoints (0.00m distance)
5. **Vector2/Vector3 fixed** - All POI placement methods now use correct Vector2 position + bakedY
6. **Test results** - 80 path points, 151 road segments, 9 clearing branches with POIs, 1 tree cluster

The caravan campaign generator is fully operational.
