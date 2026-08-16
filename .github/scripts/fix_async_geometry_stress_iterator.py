from pathlib import Path

p = Path('Assets/Tests/PlayMode/AsyncGeometryStressTests.cs')
s = p.read_text()

s = s.replace(
'''            yield return LoadShowcase(out VoxelShowcase showcase, out ShowcaseWorld world,
                                     out Camera camera, out CastlePlan plan, out Vector3 centre);
''',
'''            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);
''', 1)
s = s.replace(
'''            yield return LoadShowcase(out _, out ShowcaseWorld world,
                                     out Camera camera, out CastlePlan plan, out Vector3 centre);
''',
'''            yield return LoadShowcaseScene();
            GetShowcaseContext(out _, out ShowcaseWorld world,
                               out Camera camera, out CastlePlan plan, out Vector3 centre);
''', 1)

old = '''        private static IEnumerator LoadShowcase(out VoxelShowcase showcase,
                                                out ShowcaseWorld world,
                                                out Camera camera,
                                                out CastlePlan plan,
                                                out Vector3 centre)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                 plan.Centre.z) * 0.1f;
        }
'''
new = '''        private static IEnumerator LoadShowcaseScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static void GetShowcaseContext(out VoxelShowcase showcase,
                                               out ShowcaseWorld world,
                                               out Camera camera,
                                               out CastlePlan plan,
                                               out Vector3 centre)
        {
            showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.NotNull(showcase);
            world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(showcase);
            camera = Camera.main;
            Assert.NotNull(camera);

            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);

            int ground = world.SurfaceHeight(256, 376);
            plan = CastleBuilder.Plan(new int3(256, ground, 376), world.Seed);
            centre = new Vector3(plan.Centre.x, plan.Centre.y + plan.PlateauHeight,
                                 plan.Centre.z) * 0.1f;
        }
'''
if s.count(old) != 1:
    raise SystemExit(f'legacy iterator context helper expected once, found {s.count(old)}')
s = s.replace(old, new, 1)
p.write_text(s)

text = p.read_text()
assert 'IEnumerator LoadShowcase(out' not in text
assert 'IEnumerator LoadShowcaseScene()' in text
assert 'void GetShowcaseContext(out VoxelShowcase' in text
