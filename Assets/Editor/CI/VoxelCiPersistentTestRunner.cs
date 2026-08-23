#if UNITY_EDITOR
using System;
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

    private static TestRunnerApi s_Api;
    private static CiCallbacks s_Callbacks;
    private static bool s_Registered;
    private static bool s_FinishScheduled;

    static VoxelCiPersistentTestRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

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

        string editAssemblies = NormalizeAssemblies(
            Environment.GetEnvironmentVariable("VOXEL_CI_EDITMODE_ASSEMBLIES"));
        string playAssemblies = NormalizeAssemblies(
            Environment.GetEnvironmentVariable("VOXEL_CI_PLAYMODE_ASSEMBLIES"));
        bool bakeShowcase = IsTruthy(Environment.GetEnvironmentVariable("VOXEL_CI_BAKE_SHOWCASE"));

        File.WriteAllText(Path.Combine(resultsRoot, "persistent-failures.txt"), string.Empty);

        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(EditAssembliesKey, editAssemblies);
        SessionState.SetString(PlayAssembliesKey, playAssemblies);
        SessionState.SetString(ResultsRootKey, resultsRoot);
        SessionState.SetBool(BakeShowcaseKey, bakeShowcase);

        EnsureCallbackRegistered();

        if (!string.IsNullOrEmpty(editAssemblies))
        {
            QueuePhase("editmode");
            return;
        }

        if (!string.IsNullOrEmpty(playAssemblies))
        {
            QueuePhase("playmode");
            return;
        }

        ScheduleFinish(0, "No persistent test assemblies selected.");
    }

    private static void RestoreAfterDomainReload()
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        EnsureCallbackRegistered();

        // A queued transition can be interrupted by a script-domain reload. If so,
        // resume it here. Once Execute starts, Pending is cleared and the Test
        // Framework owns any reloads needed by that run.
        if (SessionState.GetBool(PendingKey, false))
            EditorApplication.delayCall += StartPendingPhase;
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

    private static void StartPendingPhase()
    {
        if (!SessionState.GetBool(ActiveKey, false) ||
            !SessionState.GetBool(PendingKey, false))
            return;

        string phase = SessionState.GetString(PhaseKey, string.Empty);
        string encodedAssemblies = phase == "editmode"
            ? SessionState.GetString(EditAssembliesKey, string.Empty)
            : SessionState.GetString(PlayAssembliesKey, string.Empty);

        string[] assemblies = SplitAssemblies(encodedAssemblies);
        if (assemblies.Length == 0)
        {
            if (phase == "editmode" &&
                !string.IsNullOrEmpty(SessionState.GetString(PlayAssembliesKey, string.Empty)))
            {
                QueuePhase("playmode");
                return;
            }

            ScheduleFinish(0, "No tests selected for the pending phase.");
            return;
        }

        TestMode mode;
        if (phase == "editmode")
            mode = TestMode.EditMode;
        else if (phase == "playmode")
            mode = TestMode.PlayMode;
        else
        {
            ScheduleFinish(2, "Unknown persistent CI phase: " + phase);
            return;
        }

        SessionState.SetBool(PendingKey, false);
        EnsureCallbackRegistered();

        Debug.Log(
            $"Persistent CI: starting {phase} in the existing Unity editor for " +
            $"{string.Join(", ", assemblies)}");

        var filter = new Filter
        {
            testMode = mode,
            assemblyNames = assemblies
        };

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

        bool failed =
            result.TestStatus == TestStatus.Failed ||
            result.FailCount > 0 ||
            result.InconclusiveCount > 0;

        if (failed)
        {
            ScheduleFinish(
                1,
                $"{phase} failed: {result.FailCount} failed, " +
                $"{result.InconclusiveCount} inconclusive.");
            return;
        }

        if (phase == "editmode" &&
            !string.IsNullOrEmpty(SessionState.GetString(PlayAssembliesKey, string.Empty)))
        {
            QueuePhase("playmode");
            return;
        }

        ScheduleFinish(0, "Persistent test phases passed.");
    }

    private static void ScheduleFinish(int exitCode, string message)
    {
        if (s_FinishScheduled)
            return;

        s_FinishScheduled = true;
        EditorApplication.delayCall += () => Finish(exitCode, message);
    }

    private static void Finish(int exitCode, string message)
    {
        if (!SessionState.GetBool(ActiveKey, false))
            return;

        string resultsRoot = SessionState.GetString(
            ResultsRootKey,
            Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));

        try
        {
            if (exitCode == 0 && SessionState.GetBool(BakeShowcaseKey, false))
            {
                Debug.Log("Persistent CI: baking Voxel Showcase before leaving the editor.");
                InvokeShowcaseBake();
            }

            WriteFinalSummary(resultsRoot, exitCode, message);
        }
        catch (Exception ex)
        {
            exitCode = 2;
            message = "Persistent CI finalization failed: " + ex;
            AppendFailure(message);
            try
            {
                WriteFinalSummary(resultsRoot, exitCode, message);
            }
            catch
            {
                // The editor log still contains the original exception.
            }
        }
        finally
        {
            ClearState();
        }

        Debug.Log($"Persistent CI: exiting Unity with code {exitCode}. {message}");
        EditorApplication.Exit(exitCode);
    }

    private static void InvokeShowcaseBake()
    {
        Type bakerType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(
                "VoxelEngine.Showcase.Editor.ShowcaseWorldBaker",
                throwOnError: false))
            .FirstOrDefault(type => type != null);

        if (bakerType == null)
            throw new InvalidOperationException("Could not find ShowcaseWorldBaker.");

        MethodInfo bake = bakerType
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method =>
                method.Name == "BakeShowcaseWorld" &&
                method.GetParameters().Length == 0);

        if (bake == null)
            throw new InvalidOperationException(
                "Could not find parameterless ShowcaseWorldBaker.BakeShowcaseWorld.");

        try
        {
            bake.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException("Showcase bake failed.", ex.InnerException);
        }
    }

    private static void WritePhaseSummary(string phase, ITestResultAdaptor result)
    {
        string root = SessionState.GetString(
            ResultsRootKey,
            Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));
        Directory.CreateDirectory(root);

        var text = new StringBuilder();
        text.AppendLine("phase=" + phase);
        text.AppendLine("result_state=" + result.ResultState);
        text.AppendLine("passed=" + result.PassCount);
        text.AppendLine("failed=" + result.FailCount);
        text.AppendLine("skipped=" + result.SkipCount);
        text.AppendLine("inconclusive=" + result.InconclusiveCount);
        text.AppendLine("asserts=" + result.AssertCount);
        text.AppendLine("duration_seconds=" + result.Duration.ToString("0.###"));
        File.WriteAllText(Path.Combine(root, "persistent-" + phase + ".txt"), text.ToString());
    }

    private static void WriteFinalSummary(string root, int exitCode, string message)
    {
        Directory.CreateDirectory(root);
        var text = new StringBuilder();
        text.AppendLine("exit_code=" + exitCode);
        text.AppendLine("status=" + (exitCode == 0 ? "passed" : "failed"));
        text.AppendLine("message=" + message.Replace('\n', ' ').Replace('\r', ' '));
        text.AppendLine(
            "editmode_assemblies=" + SessionState.GetString(EditAssembliesKey, string.Empty));
        text.AppendLine(
            "playmode_assemblies=" + SessionState.GetString(PlayAssembliesKey, string.Empty));
        text.AppendLine(
            "showcase_bake=" + SessionState.GetBool(BakeShowcaseKey, false));
        File.WriteAllText(Path.Combine(root, "persistent-summary.txt"), text.ToString());
    }

    private static void AppendFailure(string message)
    {
        try
        {
            string root = SessionState.GetString(
                ResultsRootKey,
                Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "PersistentCiTests"));
            Directory.CreateDirectory(root);
            File.AppendAllText(
                Path.Combine(root, "persistent-failures.txt"),
                message + Environment.NewLine);
        }
        catch
        {
            // Never mask the test failure because diagnostics could not be written.
        }
    }

    private static string NormalizeAssemblies(string value)
    {
        return string.Join(";", SplitAssemblies(value));
    }

    private static string[] SplitAssemblies(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsTruthy(string value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearState()
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetString(PhaseKey, string.Empty);
        SessionState.SetString(EditAssembliesKey, string.Empty);
        SessionState.SetString(PlayAssembliesKey, string.Empty);
        SessionState.SetString(ResultsRootKey, string.Empty);
        SessionState.SetBool(BakeShowcaseKey, false);
    }

    private sealed class CiCallbacks : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log(
                $"Persistent CI: {SessionState.GetString(PhaseKey, "unknown")} " +
                $"discovered {testsToRun.TestCaseCount} test cases.");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            OnRunFinished(result);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren)
                return;

            if (result.TestStatus == TestStatus.Failed ||
                result.TestStatus == TestStatus.Inconclusive)
            {
                AppendFailure(
                    $"{result.FullName}: {result.ResultState}{Environment.NewLine}" +
                    $"{result.Message}{Environment.NewLine}{result.StackTrace}");
            }
        }

        public void OnError(string message)
        {
            AppendFailure("Test Runner error: " + message);
            ScheduleFinish(2, "Unity Test Runner error: " + message);
        }
    }
}
#endif
