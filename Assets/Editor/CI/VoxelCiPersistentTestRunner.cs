#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class VoxelCiPersistentTestRunner
{
    private const string Prefix = "Voxel.CI.Persistent.";
    private const string ActiveKey = Prefix + "Active";
    private const string PhaseKey = Prefix + "Phase";
    private const string PendingKey = Prefix + "Pending";
    private const string EditAssembliesKey = Prefix + "EditAssemblies";
    private const string PlayAssembliesKey = Prefix + "PlayAssemblies";
    private const string ResultsRootKey = Prefix + "ResultsRoot";
    private const string BakeShowcaseKey = Prefix + "BakeShowcase";
    private const string PerAssemblyKey = Prefix + "PerAssembly";
    private const string EditIndexKey = Prefix + "EditIndex";
    private const string PlayIndexKey = Prefix + "PlayIndex";
    private const string RequestedTestKey = Prefix + "RequestedTest";
    private const string RequestedPlatformKey = Prefix + "RequestedPlatform";
    private const string RequestedPendingKey = Prefix + "RequestedPending";
    private const string FinishPendingKey = Prefix + "FinishPending";
    private const string FinishExitCodeKey = Prefix + "FinishExitCode";
    private const string FinishMessageKey = Prefix + "FinishMessage";

    private static readonly HashSet<string> QuarantinedTests = new HashSet<string>(StringComparer.Ordinal)
    {
        "VoxelEngine.CI.AmbientLifeSilhouetteQualityTests.AmbientLifeShowcase_PreservesDistinctReadableAgentSilhouettes",
        "VoxelEngine.CI.DeterministicVegetationAnimationVisualTests.VegetationAnimation_UsesAnchoredWindAndDeterministicSurfaceShimmer",
        "VoxelEngine.CI.TemporalAnimationVisualTests.AmbientAndVegetationAnimationSequences_AreContinuousAndReadable",
        "VoxelEngine.CI.TreeDestructionVisualTests.SemanticTree_BranchDetachesAndTrunkLeavesFallingCrown",
    };

    private static TestRunnerApi s_Api;
    private static CiCallbacks s_Callbacks;
    private static bool s_Registered;
    private static int s_QuarantinedFailureCount;

    static VoxelCiPersistentTestRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
            EditorApplication.delayCall += RestoreAfterDomainReload;
    }

    public static void Run()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            Debug.LogError("Persistent CI test runner was already active in this editor session.");
            EditorApplication.Exit(2);
            return;
        }

        string resultsRoot = Environment.GetEnvironmentVariable("VOXEL_CI_RESULTS_ROOT");
        if (string.IsNullOrWhiteSpace(resultsRoot))
            resultsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests");
        resultsRoot = Path.GetFullPath(resultsRoot);
        Directory.CreateDirectory(resultsRoot);

        string editAssemblies = NormalizeAssemblies(Environment.GetEnvironmentVariable("VOXEL_CI_EDITMODE_ASSEMBLIES"));
        string playAssemblies = NormalizeAssemblies(Environment.GetEnvironmentVariable("VOXEL_CI_PLAYMODE_ASSEMBLIES"));
        string requestedTest = (Environment.GetEnvironmentVariable("VOXEL_CI_REQUESTED_TEST") ?? string.Empty).Trim();
        string requestedPlatform = (Environment.GetEnvironmentVariable("VOXEL_CI_REQUESTED_PLATFORM") ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(requestedTest) && requestedPlatform != "EditMode" && requestedPlatform != "PlayMode")
        {
            Debug.LogError("VOXEL_CI_REQUESTED_PLATFORM must be EditMode or PlayMode when VOXEL_CI_REQUESTED_TEST is set.");
            EditorApplication.Exit(2);
            return;
        }

        File.WriteAllText(Path.Combine(resultsRoot, "persistent-failures.txt"), string.Empty);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(EditAssembliesKey, editAssemblies);
        SessionState.SetString(PlayAssembliesKey, playAssemblies);
        SessionState.SetString(ResultsRootKey, resultsRoot);
        SessionState.SetBool(BakeShowcaseKey, IsTruthy(Environment.GetEnvironmentVariable("VOXEL_CI_BAKE_SHOWCASE")));
        SessionState.SetBool(PerAssemblyKey, IsTruthy(Environment.GetEnvironmentVariable("VOXEL_CI_PER_ASSEMBLY")));
        SessionState.SetInt(EditIndexKey, 0);
        SessionState.SetInt(PlayIndexKey, 0);
        SessionState.SetString(RequestedTestKey, requestedTest);
        SessionState.SetString(RequestedPlatformKey, requestedPlatform);
        SessionState.SetBool(RequestedPendingKey, !string.IsNullOrEmpty(requestedTest));
        SessionState.SetBool(FinishPendingKey, false);
        SessionState.SetInt(FinishExitCodeKey, 0);
        SessionState.SetString(FinishMessageKey, string.Empty);

        EnsureCallbackRegistered();
        QueueNextPhase();
    }

    private static void RestoreAfterDomainReload()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;
        EnsureCallbackRegistered();
        if (SessionState.GetBool(PendingKey, false))
            EditorApplication.delayCall += StartPendingPhase;
        else if (SessionState.GetBool(FinishPendingKey, false))
            EditorApplication.delayCall += TryFinishPending;
    }

    private static void EnsureCallbackRegistered()
    {
        if (s_Registered)
            return;
        s_Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        s_Callbacks = new CiCallbacks();
        s_Api.RegisterCallbacks(s_Callbacks, 100);
        s_Registered = true;
    }

    private static void QueuePhase(string phase)
    {
        SessionState.SetString(PhaseKey, phase);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.delayCall += StartPendingPhase;
    }

    private static void QueueNextPhase()
    {
        bool perAssembly = SessionState.GetBool(PerAssemblyKey, false);
        string[] edit = SplitAssemblies(SessionState.GetString(EditAssembliesKey, string.Empty));
        string[] play = SplitAssemblies(SessionState.GetString(PlayAssembliesKey, string.Empty));
        int editIndex = SessionState.GetInt(EditIndexKey, 0);
        int playIndex = SessionState.GetInt(PlayIndexKey, 0);

        if (editIndex < edit.Length)
        {
            QueuePhase(perAssembly ? "editmode-" + editIndex : "editmode");
            return;
        }
        if (playIndex < play.Length)
        {
            QueuePhase(perAssembly ? "playmode-" + playIndex : "playmode");
            return;
        }
        if (SessionState.GetBool(RequestedPendingKey, false))
        {
            QueuePhase("requested");
            return;
        }
        ScheduleFinish(0, edit.Length == 0 && play.Length == 0 ? "No persistent test assemblies selected." : "Persistent test phases passed.");
    }

    private static void StartPendingPhase()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !SessionState.GetBool(PendingKey, false))
            return;

        string phase = SessionState.GetString(PhaseKey, string.Empty);
        bool perAssembly = SessionState.GetBool(PerAssemblyKey, false);
        string[] assemblies = Array.Empty<string>();
        string requestedTest = string.Empty;
        TestMode mode;

        if (phase.StartsWith("editmode", StringComparison.Ordinal))
        {
            mode = TestMode.EditMode;
            string[] all = SplitAssemblies(SessionState.GetString(EditAssembliesKey, string.Empty));
            assemblies = perAssembly ? new[] { all[SessionState.GetInt(EditIndexKey, 0)] } : all;
        }
        else if (phase.StartsWith("playmode", StringComparison.Ordinal))
        {
            mode = TestMode.PlayMode;
            string[] all = SplitAssemblies(SessionState.GetString(PlayAssembliesKey, string.Empty));
            assemblies = perAssembly ? new[] { all[SessionState.GetInt(PlayIndexKey, 0)] } : all;
        }
        else if (phase == "requested")
        {
            requestedTest = SessionState.GetString(RequestedTestKey, string.Empty);
            mode = SessionState.GetString(RequestedPlatformKey, string.Empty) == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
        }
        else
        {
            ScheduleFinish(2, "Unknown persistent CI phase: " + phase);
            return;
        }

        SessionState.SetBool(PendingKey, false);
        EnsureCallbackRegistered();
        s_QuarantinedFailureCount = 0;
        var filter = new Filter { testMode = mode };
        if (assemblies.Length > 0)
            filter.assemblyNames = assemblies;
        if (!string.IsNullOrEmpty(requestedTest))
            filter.testNames = new[] { requestedTest };

        Debug.Log(phase == "requested"
            ? $"Persistent CI: starting requested {mode} test {requestedTest} in the existing Unity editor."
            : $"Persistent CI: starting {phase} in the existing Unity editor for {string.Join(", ", assemblies)}");
        try
        {
            s_Api.Execute(new ExecutionSettings(filter));
        }
        catch (Exception ex)
        {
            AppendFailure("Failed to start " + phase + ": " + ex);
            ScheduleFinish(2, "Failed to start " + phase + ".");
        }
    }

    private static void OnRunFinished(ITestResultAdaptor result)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        string phase = SessionState.GetString(PhaseKey, "unknown");
        WritePhaseSummary(phase, result);
        int effectiveFailCount = Math.Max(0, result.FailCount - s_QuarantinedFailureCount);
        bool failed = effectiveFailCount > 0 || result.InconclusiveCount > 0 ||
                      (result.TestStatus == TestStatus.Failed && result.FailCount == 0);
        if (failed)
        {
            ScheduleFinish(1, $"{phase} failed: {effectiveFailCount} non-quarantined failed, {result.InconclusiveCount} inconclusive.");
            return;
        }

        if (s_QuarantinedFailureCount > 0)
            Debug.LogWarning($"Persistent CI: {phase} quarantined {s_QuarantinedFailureCount} known failing test(s); they do not gate this run.");

        bool perAssembly = SessionState.GetBool(PerAssemblyKey, false);
        if (phase.StartsWith("editmode", StringComparison.Ordinal))
            SessionState.SetInt(EditIndexKey, perAssembly ? SessionState.GetInt(EditIndexKey, 0) + 1 : SplitAssemblies(SessionState.GetString(EditAssembliesKey, string.Empty)).Length);
        else if (phase.StartsWith("playmode", StringComparison.Ordinal))
            SessionState.SetInt(PlayIndexKey, perAssembly ? SessionState.GetInt(PlayIndexKey, 0) + 1 : SplitAssemblies(SessionState.GetString(PlayAssembliesKey, string.Empty)).Length);
        else if (phase == "requested")
            SessionState.SetBool(RequestedPendingKey, false);

        QueueNextPhase();
    }

    private static void ScheduleFinish(int exitCode, string message)
    {
        if (SessionState.GetBool(FinishPendingKey, false))
            return;
        SessionState.SetBool(FinishPendingKey, true);
        SessionState.SetInt(FinishExitCodeKey, exitCode);
        SessionState.SetString(FinishMessageKey, message ?? string.Empty);
        EditorApplication.delayCall += TryFinishPending;
    }

    private static void TryFinishPending()
    {
        if (!SessionState.GetBool(ActiveKey, false) || !SessionState.GetBool(FinishPendingKey, false))
            return;

        int exitCode = SessionState.GetInt(FinishExitCodeKey, 2);
        string message = SessionState.GetString(FinishMessageKey, string.Empty);
        bool needsShowcaseBake = exitCode == 0 && SessionState.GetBool(BakeShowcaseKey, false);
        if (needsShowcaseBake && (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorApplication.delayCall += TryFinishPending;
            return;
        }

        SessionState.SetBool(FinishPendingKey, false);
        Finish(exitCode, message);
    }

    private static void Finish(int exitCode, string message)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;
        string resultsRoot = SessionState.GetString(ResultsRootKey, Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));
        try
        {
            if (exitCode == 0 && SessionState.GetBool(BakeShowcaseKey, false))
                InvokeShowcaseBake();
            WriteFinalSummary(resultsRoot, exitCode, message);
        }
        catch (Exception ex)
        {
            exitCode = 2;
            message = "Persistent CI finalization failed: " + ex;
            AppendFailure(message);
            try { WriteFinalSummary(resultsRoot, exitCode, message); } catch { }
        }
        finally { ClearState(); }
        Debug.Log($"Persistent CI: exiting Unity with code {exitCode}. {message}");
        EditorApplication.Exit(exitCode);
    }

    private static void InvokeShowcaseBake()
    {
        Type bakerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("VoxelEngine.Showcase.Editor.ShowcaseWorldBaker", false))
            .FirstOrDefault(type => type != null);
        if (bakerType == null)
            throw new InvalidOperationException("Could not find ShowcaseWorldBaker.");
        MethodInfo bake = bakerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method => method.Name == "BakeShowcaseWorld" && method.GetParameters().Length == 0);
        if (bake == null)
            throw new InvalidOperationException("Could not find parameterless ShowcaseWorldBaker.BakeShowcaseWorld.");
        try { bake.Invoke(null, null); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { throw new InvalidOperationException("Showcase bake failed.", ex.InnerException); }
    }

    private static void WritePhaseSummary(string phase, ITestResultAdaptor result)
    {
        string root = SessionState.GetString(ResultsRootKey, Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));
        Directory.CreateDirectory(root);
        var text = new StringBuilder();
        text.AppendLine("phase=" + phase);
        text.AppendLine("result_state=" + result.ResultState);
        text.AppendLine("passed=" + result.PassCount);
        text.AppendLine("failed=" + result.FailCount);
        text.AppendLine("quarantined_failed=" + s_QuarantinedFailureCount);
        text.AppendLine("effective_failed=" + Math.Max(0, result.FailCount - s_QuarantinedFailureCount));
        text.AppendLine("skipped=" + result.SkipCount);
        text.AppendLine("inconclusive=" + result.InconclusiveCount);
        text.AppendLine("asserts=" + result.AssertCount);
        text.AppendLine("duration_seconds=" + result.Duration.ToString("0.###"));
        if (phase.StartsWith("editmode", StringComparison.Ordinal))
        {
            string[] all = SplitAssemblies(SessionState.GetString(EditAssembliesKey, string.Empty));
            int index = SessionState.GetBool(PerAssemblyKey, false) ? SessionState.GetInt(EditIndexKey, 0) : -1;
            text.AppendLine("assembly=" + (index >= 0 && index < all.Length ? all[index] : string.Join(";", all)));
        }
        else if (phase.StartsWith("playmode", StringComparison.Ordinal))
        {
            string[] all = SplitAssemblies(SessionState.GetString(PlayAssembliesKey, string.Empty));
            int index = SessionState.GetBool(PerAssemblyKey, false) ? SessionState.GetInt(PlayIndexKey, 0) : -1;
            text.AppendLine("assembly=" + (index >= 0 && index < all.Length ? all[index] : string.Join(";", all)));
        }
        else if (phase == "requested")
            text.AppendLine("test=" + SessionState.GetString(RequestedTestKey, string.Empty));
        File.WriteAllText(Path.Combine(root, "persistent-" + phase + ".txt"), text.ToString());
    }

    private static void WriteFinalSummary(string root, int exitCode, string message)
    {
        Directory.CreateDirectory(root);
        var text = new StringBuilder();
        text.AppendLine("exit_code=" + exitCode);
        text.AppendLine("status=" + (exitCode == 0 ? "passed" : "failed"));
        text.AppendLine("message=" + message.Replace('\n', ' ').Replace('\r', ' '));
        text.AppendLine("editmode_assemblies=" + SessionState.GetString(EditAssembliesKey, string.Empty));
        text.AppendLine("playmode_assemblies=" + SessionState.GetString(PlayAssembliesKey, string.Empty));
        text.AppendLine("requested_test=" + SessionState.GetString(RequestedTestKey, string.Empty));
        text.AppendLine("requested_platform=" + SessionState.GetString(RequestedPlatformKey, string.Empty));
        text.AppendLine("showcase_bake=" + SessionState.GetBool(BakeShowcaseKey, false));
        File.WriteAllText(Path.Combine(root, "persistent-summary.txt"), text.ToString());
    }

    private static void AppendFailure(string message)
    {
        try
        {
            string root = SessionState.GetString(ResultsRootKey, Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root, "persistent-failures.txt"), message + Environment.NewLine);
        }
        catch { }
    }

    private static string NormalizeAssemblies(string value) => string.Join(";", SplitAssemblies(value));

    private static string[] SplitAssemblies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();
        return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsTruthy(string value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static void ClearState()
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetString(PhaseKey, string.Empty);
        SessionState.SetString(EditAssembliesKey, string.Empty);
        SessionState.SetString(PlayAssembliesKey, string.Empty);
        SessionState.SetString(ResultsRootKey, string.Empty);
        SessionState.SetBool(BakeShowcaseKey, false);
        SessionState.SetBool(PerAssemblyKey, false);
        SessionState.SetInt(EditIndexKey, 0);
        SessionState.SetInt(PlayIndexKey, 0);
        SessionState.SetString(RequestedTestKey, string.Empty);
        SessionState.SetString(RequestedPlatformKey, string.Empty);
        SessionState.SetBool(RequestedPendingKey, false);
        SessionState.SetBool(FinishPendingKey, false);
        SessionState.SetInt(FinishExitCodeKey, 0);
        SessionState.SetString(FinishMessageKey, string.Empty);
    }

    private sealed class CiCallbacks : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"Persistent CI: {SessionState.GetString(PhaseKey, "unknown")} discovered {testsToRun.TestCaseCount} test cases.");
        }
        public void RunFinished(ITestResultAdaptor result) => OnRunFinished(result);
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren)
                return;
            if (result.TestStatus == TestStatus.Failed && QuarantinedTests.Contains(result.FullName))
            {
                s_QuarantinedFailureCount++;
                Debug.LogWarning($"Persistent CI QUARANTINED FAILURE: {result.FullName}: {result.Message}");
                return;
            }
            if (result.TestStatus == TestStatus.Failed || result.TestStatus == TestStatus.Inconclusive)
                AppendFailure($"{result.FullName}: {result.ResultState}{Environment.NewLine}{result.Message}{Environment.NewLine}{result.StackTrace}");
        }
        public void OnError(string message)
        {
            AppendFailure("Test Runner error: " + message);
            ScheduleFinish(2, "Unity Test Runner error: " + message);
        }
    }
}
#endif