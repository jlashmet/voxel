from pathlib import Path


def once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected one match, found {count}")
    return text.replace(old, new, 1)


cache_path = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs')
s = cache_path.read_text()

s = once(s,
'''                if (_build.Phase == 7)
                {
                    if (_hlodJobScheduled)
                    {
                        if (!_hlodJobHandle.IsCompleted) break;
                        if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                                _hlodJobHandle, ref _framePathBlockingCompletionViolations))
                            break;
                        _hlodJobScheduled = false;
                        _build.HasOwnedSolid = _indices.Length > 0;
                    }
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    if (_hlodOverflow[0] != 0)
                        throw new InvalidOperationException(
                            $"Feature-preserving HLOD output overflow in chunk {_build.Coordinate}; "
                          + "refusing to allocate or publish partial coarse geometry.");
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }
''',
'''                if (_build.Phase == 7)
                {
                    if (_hlodJobScheduled)
                    {
                        if (!_hlodJobHandle.IsCompleted) break;
                        if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                                _hlodJobHandle, ref _framePathBlockingCompletionViolations))
                            break;
                        _hlodJobScheduled = false;
                        _build.HasOwnedSolid = _indices.Length > 0;
                    }
                    if (_hlodOverflow[0] != 0)
                        throw new InvalidOperationException(
                            $"Feature-preserving HLOD output overflow in chunk {_build.Coordinate}; "
                          + "refusing to allocate or publish partial coarse geometry.");
                    // Profile blocks validate their backing against the same immutable mixed-brick
                    // payloads. Keep COW pins alive through profile emission; phase 3 releases
                    // them under the normal deadline once the last profile has consumed them.
                    if (_buildProfileBlocks.Length == 0
                        && !StepReleasePinnedSnapshotBlocks(deadline))
                        break;
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }
''',
'HLOD pin lifetime')

s = once(s,
'''                if (_build.Phase == 6)
                {
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                    _build.Phase = 5;
                    continue;
                }
''',
'''                if (_build.Phase == 6)
                {
                    // Profile geometry may still need mixed-brick backing from the immutable COW
                    // snapshot. Do not release those pins until profile emission has finished.
                    if (_buildProfileBlocks.Length == 0
                        && !StepReleasePinnedSnapshotBlocks(deadline))
                        break;
                    _build.Phase = 5;
                    continue;
                }
''',
'exact-result pin lifetime')

s = once(s,
'''                    _profileEmitTiming.Add(ElapsedMs(profileStart));
                    if (!profilesDone) continue;

                    // The step-8 HLOD grid and the step-4 inner ring both resolve geometry on a
''',
'''                    _profileEmitTiming.Add(ElapsedMs(profileStart));
                    if (!profilesDone) continue;

                    // Profile backing reads are complete. Drain the exact mixed-brick pins now,
                    // still under the worker deadline, before transition/publication can proceed.
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;

                    // The step-8 HLOD grid and the step-4 inner ring both resolve geometry on a
''',
'profile-phase pin release')

s = once(s,
'''            material = _densityMixedVoxels[brick.MixedOffset + voxelIndex];
            surface = VoxelSurfaceSemantics.FromStorage(
                _densityMixedSurfaceSemantics[brick.MixedOffset + voxelIndex]).Packed;
            boundary = _densityMixedBoundarySamples[brick.MixedOffset + voxelIndex];
''',
'''            NativeArray<byte> mixedVoxels = PinnedMixedVoxelsOrFallback();
            NativeArray<ushort> mixedSurfaces = PinnedMixedSurfaceSemanticsOrFallback();
            NativeArray<byte> mixedBoundaries = PinnedMixedBoundarySamplesOrFallback();
            material = mixedVoxels[brick.MixedOffset + voxelIndex];
            surface = VoxelSurfaceSemantics.FromStorage(
                mixedSurfaces[brick.MixedOffset + voxelIndex]).Packed;
            boundary = mixedBoundaries[brick.MixedOffset + voxelIndex];
''',
'ReadSnapshotCell pinned aliases')
cache_path.write_text(s)

arch_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
a = arch_path.read_text()
if 'ProfileBackingKeepsCowPinsUntilProfileEmissionFinishes' in a:
    raise SystemExit('profile pin lifetime architecture test already exists')
addition = r'''

        [Test]
        public void ProfileBackingKeepsCowPinsUntilProfileEmissionFinishes()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            int profilePhase = cache.IndexOf("if (_build.Phase == 3)",
                                             StringComparison.Ordinal);
            int transitionPhase = cache.IndexOf("if (_build.Phase == 4)", profilePhase,
                                                StringComparison.Ordinal);
            Assert.GreaterOrEqual(profilePhase, 0);
            Assert.Greater(transitionPhase, profilePhase);
            string profile = cache.Substring(profilePhase, transitionPhase - profilePhase);
            StringAssert.Contains("StepReleasePinnedSnapshotBlocks(deadline)", profile);

            int read = cache.IndexOf("private void ReadSnapshotCell", StringComparison.Ordinal);
            int readEnd = cache.IndexOf("private float3 DensityNormal", read,
                                        StringComparison.Ordinal);
            Assert.GreaterOrEqual(read, 0);
            Assert.Greater(readEnd, read);
            string readSnapshot = cache.Substring(read, readEnd - read);
            StringAssert.Contains("PinnedMixedVoxelsOrFallback()", readSnapshot);
            StringAssert.Contains("PinnedMixedSurfaceSemanticsOrFallback()", readSnapshot);
            StringAssert.Contains("PinnedMixedBoundarySamplesOrFallback()", readSnapshot);
            StringAssert.DoesNotContain("_densityMixedVoxels[brick.MixedOffset", readSnapshot);
        }
'''
marker = '\n\n        [Test]\n        public void ExactGeometrySnapshotsBorrowPinnedCowPayloads()'
if marker not in a:
    raise SystemExit('profile pin test insertion marker missing')
a = a.replace(marker, addition + marker, 1)
arch_path.write_text(a)

cache = cache_path.read_text()
assert 'NativeArray<byte> mixedVoxels = PinnedMixedVoxelsOrFallback();' in cache
assert 'if (!StepReleasePinnedSnapshotBlocks(deadline)) break;' in cache[cache.index('if (_build.Phase == 3)'):cache.index('if (_build.Phase == 4)')]
print('profile/COW pin lifetime fix applied')
