#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class VoxelCiPersistentTestProgressLogger
{
    private const string ActiveKey = "Voxel.CI.Persistent.Active";
    private static readonly TestRunnerApi Api;
    private static readonly ProgressCallbacks Callbacks;

    static VoxelCiPersistentTestProgressLogger()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Callbacks = new ProgressCallbacks();
        Api.RegisterCallbacks(Callbacks, 110);
    }

    private sealed class ProgressCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { }

        public void TestStarted(ITestAdaptor test)
        {
            if (!SessionState.GetBool(ActiveKey, false))
                return;
            Debug.Log($"Persistent CI TEST START: {test.FullName}");
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ActiveKey, false) || result.HasChildren)
                return;
            Debug.Log($"Persistent CI TEST FINISH: {result.FullName} status={result.TestStatus} duration={result.Duration:0.###}s");
        }
    }
}
#endif
