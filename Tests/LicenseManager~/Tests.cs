using AGXUnity;
using AGXUnityEditor;
using AGXUnityEditor.Windows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

public static class Tests
{
  private static readonly List<LicenseManagerWindow> Windows = new List<LicenseManagerWindow>();
  private const BindingFlags Private = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
  private static agx.Runtime Native => agx.Runtime.Current;
  private static void Check(bool condition, string message) { if (!condition) throw new System.Exception(message); }
  private static object Call(object target, string method, params object[] args)
  {
    try { return (target as Type ?? target.GetType()).GetMethod(method, Private).Invoke(target is Type ? null : target, args); }
    catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
  }
  private static object Get(object target, string name) => (target as Type ?? target.GetType()).GetField(name, Private).GetValue(target is Type ? null : target);
  private static void Set(object target, string name, object value) => (target as Type ?? target.GetType()).GetField(name, Private).SetValue(target is Type ? null : target, value);
  private static string FileFor(string name, string content = "<SoftwareKey>floating:shared")
  {
    var path = Path.GetFullPath(name);
    File.WriteAllText(path, content); File.WriteAllText(path + ".meta", "synthetic meta");
    return path;
  }
  private static bool Operation(Action<Action<bool>> start)
  {
    var result = new TaskCompletionSource<bool>();
    start(value => result.SetResult(value));
    Check(result.Task.Wait(5000), "Operation callback timed out");
    LicenseManager.AwaitTasks();
    return result.Task.Result;
  }
  private static bool Connect(string path) => Operation(done => LicenseManager.ConnectFloatingAsync(path, done));
  private static bool Return() => Operation(LicenseManager.ReturnFloatingAsync);
  private static LicenseManagerWindow Window()
  {
    var window = new LicenseManagerWindow(); Windows.Add(window);
    Call(window, "OnEnable"); Wait(window); return window;
  }
  private static void Wait(LicenseManagerWindow window)
  {
    LicenseManager.AwaitTasks();
    var until = DateTime.UtcNow.AddSeconds(5);
    do {
      EditorApplication.Tick();
      Call(window, "OnEditorUpdate");
      if (!window.IsUpdatingLicenseInformation && Get(window, "m_licenseOperationTask") == null) return;
      Thread.Sleep(1);
    } while (DateTime.UtcNow < until);
    throw new System.Exception("Window update timed out");
  }
  private static object Data(string path)
  {
    var type = typeof(LicenseManagerWindow).GetNestedType("LicenseData", BindingFlags.NonPublic);
    var data = Activator.CreateInstance(type);
    type.GetField("Filename").SetValue(data, path);
    type.GetField("LicenseInfo").SetValue(data, LicenseManager.QueryInfo(path));
    return data;
  }
  private static void Capture() => LicenseWarnings.Capture(LicenseManager.UpdateLicenseInformation());

  public static int Main()
  {
    SessionState.MainThread = Thread.CurrentThread.ManagedThreadId;
    Call(typeof(LicenseManager), "InitializeEditorSessionHandling");
    var cases = new Action[] {
      MetadataDoesNotCheckout, ConnectUsesOnlyNetworkSession, ConnectionFailuresPersist,
      ReturnUpdatesValidityAndPreservesFile, FailedReturnPreservesSession, NativeExceptionsComplete,
      ConcurrentOperationsAreRejected, ReturnSurvivesEditorTransitions, DisabledLoadingSkipsFallback,
      DeactivationRejectsFloatingAndUnknown, ActiveDeletionReturnsFirst, FailedDeletionReturnKeepsFiles,
      InactiveDeletionUsesPaths, UnknownSourceRequiresReturn, ControlsFollowSelectedMetadata,
      PlayModeDisablesChanges, ImportsInspectBeforeConnect, NonFloatingWorkflows,
      RuntimeGenerationWhileFloating, ClosingWindowDuringReturn, ExternalSessionChangesAreObserved,
      HeldSeatRejectsReplacementOperations, ReloadWaitsForPendingReturn,
      RejectedLoadsDoNotBorrowExistingValidity, FailedRuntimeActivationPreservesRequest,
      FailedRuntimeActivationSkipsRefreshAndFloatingFallback, SuccessfulLoadsAndRuntimeActivation,
      AcceptedLoadRequiresValidLicense, LoadExceptionsRefreshValidity,
      SearchPreservesValidNativeLicense, FailedSearchUsesNativeState, EmptySearchUsesNativeState,
      AssetImportWorkersCannotAccessLicensing
    };
    var root = Path.Combine(Path.GetTempPath(), "agx-license-fixtures-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var originalDirectory = Directory.GetCurrentDirectory();
    var failures = 0;
    foreach (var test in cases) {
      try {
        var directory = Path.Combine(root, test.Method.Name);
        Directory.CreateDirectory(Path.Combine(directory, "Assets"));
        Directory.SetCurrentDirectory(directory);
        LicenseManager.AwaitTasks();
        agx.Runtime.Current = new agx.Runtime();
        AssetDatabase.WorkerProcess = false;
        Set(typeof(LicenseManager), "s_isAssetImportWorkerProcess", null);
        SessionState.Values.Clear();
        Set(typeof(LicenseManager), "s_pendingFloatingSessionState", 0);
        Set(typeof(LicenseManager), "s_activeFloatingLicenseFilename", null);
        Capture(); TestUI.Reset();
        EditorApplication.isPlayingOrWillChangePlaymode = false;
        test();
        Console.WriteLine("PASS " + test.Method.Name);
      }
      catch (System.Exception error) { ++failures; Console.WriteLine("FAIL " + test.Method.Name + ": " + error); }
      finally { foreach (var window in Windows) Call(window, "OnDisable"); Windows.Clear(); }
    }
    Directory.SetCurrentDirectory(originalDirectory);
    Console.WriteLine($"{cases.Length - failures}/{cases.Length} cases passed. Fixtures: {root}");
    return failures == 0 ? 0 : 1;
  }

  private static void MetadataDoesNotCheckout()
  {
    Native.CurrentType = 0; Native.CurrentId = "service"; Native.Valid = true;
    var path = FileFor("floating.lfx");
    Check(LicenseManager.QueryInfo(path).IsFloating, "Floating metadata lost with service active");
    Check(!LicenseManager.QueryInfo(FileFor("unknown.lfx", "invalid")).IsParsed, "Unknown metadata identified as service");
    Check(Native.OpenCalls == 0 && Native.LoadCalls == 0, "Metadata query acquired a seat");
  }
  private static void ConnectUsesOnlyNetworkSession()
  {
    var path = FileFor("floating.lfx");
    Check(Connect("./floating.lfx"), "Connect failed");
    Check(LicenseManager.ActiveFloatingLicenseFilename == path.Replace('\\', '/'), "Source path not normalized");
    Check(LicenseManager.LicenseInfo.IsFloating && NativeHandler.Instance.HasValidLicense, "Cached/native validity not updated");
    Check(Native.OpenCalls == 1 && Native.ActivateCalls == 0 && Native.RefreshCalls == 0 && Native.LoadCalls == 0 && Native.DeactivateCalls == 0, "Connect used other license operations");
    Check(!Connect(path), "Connect switched an active session");
    Check(!LicenseManager.LoadFile(FileFor("service.lfx", "<SoftwareKey>service:other")), "Load replaced a held seat");
  }
  private static void ConnectionFailuresPersist()
  {
    var path = FileFor("floating.lfx");
    var window = Window(); Native.OpenResult = false;
    foreach (var failure in new[] { "Server unreachable", "No seats available" }) {
      Native.Failure = failure;
      Call(window, "ConnectLicense", path); Wait(window);
      var error = (string)Get(window, "m_operationError");
      Check(error.Contains(failure) && error.Contains("Native operation detail"), "Failure details lost before scan");
      var attempts = Native.OpenCalls;
      for (int i = 0; i < 5; ++i) { Call(window, "OnGUI"); Call(window, "StartUpdateLicenseInformation"); Wait(window); }
      Check(Native.OpenCalls == attempts && (string)Get(window, "m_operationError") == error, "Scan/repaint retried or erased error");
      Check(TestUI.Fields.Any(f => f.Label == "License type" && f.Value == "Floating"), "Failed checkout not labeled Floating");
    }
  }
  private static void ReturnUpdatesValidityAndPreservesFile()
  {
    var path = FileFor("floating.lfx"); Check(Connect(path), "Connect failed"); Check(Return(), "Return failed");
    Check(!LicenseManager.LicenseInfo.IsValid && !NativeHandler.Instance.HasValidLicense, "Return left valid cache");
    Check(LicenseManager.ActiveFloatingLicenseFilename == null && !LicenseManager.AutomaticFloatingCheckoutEnabled, "Return did not clear source/disable auto checkout");
    Check(File.Exists(path) && File.Exists(path + ".meta") && Native.DeactivateCalls == 0, "Return deleted or deactivated");
  }
  private static void FailedReturnPreservesSession()
  {
    var path = FileFor("floating.lfx"); Connect(path); Native.CloseResult = false;
    Check(!Return(), "Return should fail");
    Check(LicenseManager.ActiveFloatingLicenseFilename != null && LicenseManager.LicenseInfo.IsFloating && LicenseManager.AutomaticFloatingCheckoutEnabled, "Failed return discarded session or disabled checkout");
  }
  private static void NativeExceptionsComplete()
  {
    var path = FileFor("floating.lfx");
    Native.BeforeOpen = () => throw new IOException("synthetic connection exception");
    Check(!Connect(path) && LicenseManager.LastOperationError.Contains("synthetic connection exception"), "Connect exception lost");
    Native.BeforeOpen = null; Connect(path);
    Native.BeforeClose = () => throw new IOException("synthetic return exception");
    Check(!Return() && LicenseManager.LastOperationError.Contains("synthetic return exception") && NativeHandler.Instance.HasValidLicense, "Return exception lost/session invalidated");
  }
  private static void ConcurrentOperationsAreRejected()
  {
    var path = FileFor("floating.lfx");
    using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    Native.BeforeOpen = () => { entered.Set(); Check(release.Wait(5000), "Blocked open timed out"); };
    var result = new TaskCompletionSource<bool>();
    LicenseManager.ConnectFloatingAsync(path, success => result.SetResult(success));
    try {
      Check(entered.Wait(5000) && LicenseManager.IsBusy && LicenseManager.IsConnecting, "Busy state missing");
      var rejected = 0;
      Action<bool> reject = success => { Check(!success, "Overlapping operation accepted"); ++rejected; };
      LicenseManager.ReturnFloatingAsync(reject); LicenseManager.ConnectFloatingAsync(path, reject);
      LicenseManager.RefreshAsync(path, reject); LicenseManager.ActivateAsync(1, "password", ".", reject);
      Check(rejected == 4 && Native.OpenCalls == 1 && Native.CloseCalls == 0 && Native.ActivateCalls == 0, "Overlapping native request executed");
    }
    finally { release.Set(); }
    Check(result.Task.Wait(5000) && result.Task.Result, "Original request lost");
    LicenseManager.AwaitTasks(); Check(!LicenseManager.IsBusy, "Busy state stuck");
  }
  private static void ReturnSurvivesEditorTransitions()
  {
    var path = FileFor("floating.lfx"); Connect(path); Return();
    AssemblyReloadEvents.Reload();
    Set(typeof(LicenseManager), "s_pendingFloatingSessionState", 0); // New domain; SessionState survives.
    Check(!LicenseManager.AutomaticFloatingCheckoutEnabled, "Reload forgot manual return");
    Check(!LicenseManager.LoadFile(allowFloating: LicenseManager.AutomaticFloatingCheckoutEnabled), "Reload acquired a seat");
    EditorApplication.PlayTransition(PlayModeStateChange.ExitingEditMode);
    EditorApplication.PlayTransition(PlayModeStateChange.ExitingPlayMode);
    Check(!LicenseManager.LoadFile(allowFloating: LicenseManager.AutomaticFloatingCheckoutEnabled) && Native.OpenCalls == 1, "Play transition acquired a seat");
    Capture(); LicenseWarnings.ScheduleStartupWarning(); EditorApplication.Tick();
    Check(TestUI.Dialogs.Count == 0 && string.IsNullOrEmpty(LicenseWarnings.GetWarningMessage(LicenseWarnings.CurrentLicense)), "Intentional return displayed a persistent message or startup warning");
    Native.OpenResult = false; Check(!Connect(path) && !LicenseManager.AutomaticFloatingCheckoutEnabled, "Failed connect enabled auto checkout");
    Native.OpenResult = true; Check(Connect(path), "Explicit reconnect failed"); EditorApplication.Tick();
    Check(LicenseManager.AutomaticFloatingCheckoutEnabled, "Explicit connect did not enable checkout");
    Return(); EditorApplication.Tick(); SessionState.Values.Clear(); // Editor restart.
    Check(LicenseManager.AutomaticFloatingCheckoutEnabled && LicenseManager.LoadFile(), "Fresh session did not auto-connect");
  }
  private static void DisabledLoadingSkipsFallback()
  {
    FileFor("fallback.lfx", "<SoftwareKey>fallback");
    Check(!LicenseManager.LoadFile(allowFloating: false) && Native.OpenCalls == 0, "Disabled fallback checked out");
    Check(LicenseManager.LoadFile() && Native.OpenCalls == 1, "Default fallback changed");
  }
  private static void DeactivationRejectsFloatingAndUnknown()
  {
    Native.CurrentType = 0; Native.Valid = true; Native.CurrentId = "service";
    foreach (var path in new[] { FileFor("floating.lfx"), FileFor("unknown.lfx", "invalid") })
      Check(!LicenseManager.DeactivateAndDelete(path) && File.Exists(path) && File.Exists(path + ".meta"), "Unsafe file deactivation/deletion");
    Check(Native.LoadCalls == 0 && Native.DeactivateCalls == 0 && Native.CurrentType == 0, "Guard loaded/deactivated another license");
    Native.CurrentType = 1; Capture(); Check(!LicenseManager.DeactivateLoaded() && Native.DeactivateCalls == 0, "Floating session deactivated");
  }
  private static void ActiveDeletionReturnsFirst()
  {
    var path = FileFor("floating.lfx"); Connect(path); var window = Window();
    TestUI.ConfirmDialog = false; Call(window, "DeleteLicense", Data(path));
    Check(Native.CloseCalls == 0 && File.Exists(path), "Cancel changed session");
    TestUI.ConfirmDialog = true;
    Native.BeforeClose = () => Check(File.Exists(path) && File.Exists(path + ".meta"), "File removed before return");
    Call(window, "DeleteLicense", Data(path)); Wait(window);
    Check(!File.Exists(path) && !File.Exists(path + ".meta") && Native.CloseCalls == 1 && Native.DeactivateCalls == 0, "Return/delete failed");
    Check(TestUI.Dialogs.All(d => !d.ToLowerInvariant().Contains("deactivat")) && TestUI.Dialogs.Last().Contains("Return Seat and Delete"), "Floating dialog offered deactivation");
  }
  private static void FailedDeletionReturnKeepsFiles()
  {
    var path = FileFor("floating.lfx"); Connect(path); var window = Window();
    Native.CloseResult = false; TestUI.ConfirmDialog = true;
    Call(window, "DeleteLicense", Data(path)); Wait(window);
    Check(File.Exists(path) && File.Exists(path + ".meta") && ((string)Get(window, "m_operationError")).Contains(Native.Failure), "Failed return deleted file or lost error");
    Call(window, "StartUpdateLicenseInformation"); Wait(window);
    Check(!string.IsNullOrEmpty((string)Get(window, "m_operationError")), "Rescan erased return failure");
  }
  private static void InactiveDeletionUsesPaths()
  {
    foreach (var id in new[] { "shared", "" }) {
      var active = FileFor("a" + id + ".lfx", "<SoftwareKey>floating:" + id);
      var inactive = FileFor("b" + id + ".lfx", "<SoftwareKey>floating:" + id);
      Connect(active); var window = Window(); TestUI.ConfirmDialog = true;
      var returns = Native.CloseCalls;
      Call(window, "DeleteLicense", Data(inactive)); Wait(window);
      Check(!File.Exists(inactive) && Native.CloseCalls == returns && LicenseManager.HasFloatingSession, "Duplicate/empty ID returned another file's seat");
      Check(!LicenseManager.DeleteFile(active), "Raw deletion removed active file");
      Return();
    }
  }
  private static void UnknownSourceRequiresReturn()
  {
    var path = FileFor("floating.lfx", "<SoftwareKey>floating:");
    Native.CurrentType = 1; Native.Valid = true; Native.CurrentId = ""; Capture();
    var window = Window(); Call(window, "OnGUI");
    Check(LicenseManager.ActiveFloatingLicenseFilename == null, "Guessed external session source");
    Check(TestUI.Buttons.Any(b => b.Text == "Return Seat" && b.Enabled), "Unknown source has no Return Seat");
    Check(TestUI.MiscButtons.Where(b => b.Icon == MiscIcon.EntryRemove).All(b => !b.Enabled), "Unknown source permits floating deletion");
    Call(window, "DeleteLicense", Data(path)); Check(File.Exists(path) && TestUI.Dialogs.Count == 0, "Unknown source deletion prompted or deleted");
    Call(window, "ReturnSeat", new object[] { null }); Wait(window);
    Check(!LicenseManager.HasFloatingSession && File.Exists(path), "Unknown source return failed");
  }
  private static void ControlsFollowSelectedMetadata()
  {
    var floating = FileFor("floating.lfx"); var service = FileFor("service.lfx", "<SoftwareKey>service:service"); var unknown = FileFor("unknown.lfx", "invalid");
    var window = Window();
    foreach (var path in new[] { floating, service, unknown }) {
      TestUI.Reset(); Call(window, "LicenseDataGUI", Data(path));
      Check(TestUI.MiscButtons.Any(b => b.Tooltip.Contains("deactivate")) == (path == service), "Wrong deactivation tooltip");
      Check(TestUI.Buttons.Any(b => b.Text == "Connect") == (path == floating), "Wrong Connect control");
      Check(TestUI.MiscButtons.Any(b => b.Icon == MiscIcon.Update) == (path != floating), "Floating refresh control exposed");
    }
    Connect(floating); Capture(); TestUI.Reset(); Call(window, "OnGUI");
    Check(TestUI.Buttons.Where(b => b.Text == "Connect" || b.Text == "Activate").All(b => !b.Enabled), "Held seat permits replacement");
    Check(TestUI.MiscButtons.Where(b => b.Icon == MiscIcon.Update).All(b => !b.Enabled), "Held seat permits refresh");
  }
  private static void PlayModeDisablesChanges()
  {
    var path = FileFor("floating.lfx"); Connect(path); var window = Window();
    EditorApplication.isPlayingOrWillChangePlaymode = true; TestUI.Reset(); Call(window, "OnGUI");
    Check(TestUI.Buttons.All(b => !b.Enabled) && TestUI.MiscButtons.All(b => !b.Enabled), "Play mode enables license changes");
  }
  private static void ImportsInspectBeforeConnect()
  {
    var path = FileFor("source.lfx"); var window = Window();
    TestUI.ImportFile = path; TestUI.ConfirmDialog = true;
    try { Call(window, "ActivateLicenseGUI"); } catch (UnityEngine.ExitGUIException) { }
    TestUI.ImportFile = null; Wait(window);
    Check(File.Exists("Assets/source.lfx") && Native.OpenCalls == 0 && Native.LoadCalls == 0, "Floating import checked out a seat");
    Check(((string)Get(window, "m_importMessage")).Contains("Connect"), "Import gave no Connect guidance");
    TestUI.ImportFile = FileFor("unidentified.lfx", "<SoftwareKey>fallback");
    try { Call(window, "ActivateLicenseGUI"); } catch (UnityEngine.ExitGUIException) { }
    TestUI.ImportFile = null; Wait(window);
    Check(File.Exists("Assets/unidentified.lfx") && Native.OpenCalls == 0 && Native.LoadCalls == 0, "Unidentified import used checkout fallback");
  }
  private static void NonFloatingWorkflows()
  {
    var service = FileFor("service.lfx", "<SoftwareKey>service:service");
    Check(LicenseManager.LoadFile(service), "Service load failed");
    Check(LicenseManager.Refresh(service) && Native.RefreshCalls == 1, "Service refresh failed");
    Check(LicenseManager.Activate(123, "synthetic", "Assets") && Native.ActivateCalls == 1, "Activation failed");
    Check(LicenseManager.DeactivateAndDelete(service) && Native.DeactivateCalls == 1, "Service deactivation failed");
    var imported = FileFor("import.lfx", "<SoftwareKey>service:imported"); var window = Window();
    TestUI.ImportFile = imported; TestUI.ConfirmDialog = true;
    try { Call(window, "ActivateLicenseGUI"); } catch (UnityEngine.ExitGUIException) { }
    TestUI.ImportFile = null; Wait(window); Check(Native.CurrentId == "imported", "Service import failed to load");
  }
  private static void RuntimeGenerationWhileFloating()
  {
    var path = FileFor("floating.lfx"); Connect(path);
    File.WriteAllText("application.exe", "synthetic reference");
    Check(LicenseManager.GenerateEncryptedRuntime(42, "synthetic", Directory.GetCurrentDirectory(), "application.exe") && File.Exists("agx.rtlfx"), "Runtime generation gated on floating editor license");
    Check(LicenseManager.HasFloatingSession && Native.OpenCalls == 1 && Native.CloseCalls == 0, "Runtime generation changed editor seat");
  }
  private static void ClosingWindowDuringReturn()
  {
    var path = FileFor("floating.lfx"); Connect(path); var window = Window();
    using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    Native.BeforeClose = () => { entered.Set(); Check(release.Wait(5000), "Return wait timed out"); };
    Call(window, "ReturnSeat", path);
    try { Check(entered.Wait(5000), "Return did not start"); Call(window, "OnDisable"); }
    finally { release.Set(); }
    LicenseManager.AwaitTasks(); EditorApplication.Tick();
    Check(!LicenseManager.AutomaticFloatingCheckoutEnabled && !LicenseManager.HasFloatingSession, "Closing window lost return state");
    Check(File.Exists(path) && File.Exists(path + ".meta"), "Closed window deleted files");
  }
  private static void ExternalSessionChangesAreObserved()
  {
    var path = FileFor("floating.lfx"); var window = Window();
    Native.CurrentType = 1; Native.Valid = true;
    Call(window, "OnEditorUpdate"); Call(window, "OnGUI");
    Check(TestUI.Buttons.Any(b => b.Text == "Return Seat" && b.Enabled) && LicenseManager.ActiveFloatingLicenseFilename == null, "External session not observed");
    Native.clear(); Call(window, "OnEditorUpdate");
    Check(!LicenseWarnings.CurrentLicense.IsFloating, "External return not observed");
    Connect(path); Native.clear();
    Check(LicenseManager.ActiveFloatingLicenseFilename == null, "Ended session kept source filename");
    Native.CurrentType = 1; Capture();
    Check(LicenseManager.ActiveFloatingLicenseFilename == null, "New external session inherited old source");
  }
  private static void HeldSeatRejectsReplacementOperations()
  {
    var floating = FileFor("floating.lfx"); var service = FileFor("service.lfx", "<SoftwareKey>service:service");
    Check(!LicenseManager.Refresh(floating) && Native.RefreshCalls == 0 && Native.OpenCalls == 0, "Floating file refreshed/connected via refresh");
    Connect(floating);
    Check(!LicenseManager.Activate(1, "synthetic", "Assets") && Native.ActivateCalls == 0, "Activation replaced seat");
    Check(!LicenseManager.Refresh(service) && Native.RefreshCalls == 0, "Refresh replaced seat");
    Check(!LicenseManager.Load("<SoftwareKey>service:service") && Native.LoadCalls == 0, "Content load replaced seat");
    Check(!LicenseManager.CreateOfflineLicense("synthetic", "offline.lfx", false) && !File.Exists("offline.lfx"), "Offline activation replaced seat");
    var window = Window(); TestUI.ImportFile = service; TestUI.ConfirmDialog = true;
    try { Call(window, "ActivateLicenseGUI"); } catch (UnityEngine.ExitGUIException) { }
    TestUI.ImportFile = null; Wait(window);
    Check(File.Exists("Assets/service.lfx") && Native.LoadCalls == 0, "Import replaced seat");
    Check(LicenseManager.HasFloatingSession && Native.CloseCalls == 0, "Replacement attempt returned seat");
  }
  private static void ReloadWaitsForPendingReturn()
  {
    var path = FileFor("floating.lfx"); Connect(path); EditorApplication.Tick();
    using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    Native.BeforeClose = () => { entered.Set(); Check(release.Wait(5000), "Pending return timed out"); };
    LicenseManager.ReturnFloatingAsync(null);
    Check(entered.Wait(5000) && LicenseManager.IsReturning, "Return busy state missing");
    var releaseTask = Task.Run(() => { Thread.Sleep(25); release.Set(); });
    AssemblyReloadEvents.Reload();
    Check(!LicenseManager.IsBusy && !LicenseManager.HasFloatingSession && SessionState.GetBool("AGXUnity.FloatingLicenseReturned", false), "Reload failed to await and persist return");
    releaseTask.Wait();
  }

  private static void RejectedLoadsDoNotBorrowExistingValidity()
  {
    Native.CurrentType = 0; Native.CurrentId = "old-license"; Native.Valid = true; Capture();
    Native.LoadResult = false;
    foreach (var content in new[] { "<SoftwareKey>service:rejected", "Key { invalid legacy }", "invalid-obfuscated" }) {
      Check(!LicenseManager.Load(content), "Rejected content reported success using the old license");
      var file = FileFor("rejected" + (content.StartsWith("<SoftwareKey>") ? ".lfx" : ".lic"), content);
      Check(!LicenseManager.LoadFile(file) && File.ReadAllText(file) == content, "Rejected file reported success or changed");
      Check(LicenseManager.LicenseInfo.IsValid && NativeHandler.Instance.HasValidLicense && Native.CurrentId == "old-license", "Rejected load misrepresented or cleared the surviving license");
    }
  }

  private static void FailedRuntimeActivationPreservesRequest()
  {
    Native.CurrentType = 0; Native.CurrentId = "old-license"; Native.Valid = true; Capture();
    Native.RuntimeActivationResult = false;
    var request = FileFor("activation.rtlfx", "RT=synthetic-rejected");
    Check(!LicenseManager.ActivateEncryptedRuntime(request, "Assets"), "Rejected runtime activation borrowed old license validity");
    Check(File.ReadAllText(request) == "RT=synthetic-rejected" && File.Exists(request + ".meta") && !File.Exists("Assets/agx.lfx"), "Rejected runtime activation lost the request or created a license");
    Check(Native.CurrentId == "old-license" && NativeHandler.Instance.HasValidLicense, "Rejected runtime activation changed the surviving license");
  }

  private static void FailedRuntimeActivationSkipsRefreshAndFloatingFallback()
  {
    Native.RuntimeActivationResult = false; Native.IsRefreshed = true;
    Native.Failure = "Cannot activate a floating license";
    var request = FileFor("activation.rtlfx", "RT=synthetic-rejected");
    Check(!LicenseManager.ActivateEncryptedRuntime(request, "Assets"), "Failed activation used another operation's success");
    Check(File.ReadAllText(request) == "RT=synthetic-rejected" && Native.OpenCalls == 0, "Failed runtime activation rewrote the request or tried floating checkout");
  }

  private static void SuccessfulLoadsAndRuntimeActivation()
  {
    foreach (var content in new[] { "<SoftwareKey>service:valid", "Key { synthetic legacy }", "synthetic-obfuscated" })
      Check(LicenseManager.Load(content) && LicenseManager.LicenseInfo.IsValid && NativeHandler.Instance.HasValidLicense, "Successful load failed");
    var request = FileFor("activation.rtlfx", "RT=synthetic-valid");
    Check(LicenseManager.ActivateEncryptedRuntime(request, "Assets") && File.Exists("Assets/agx.lfx") && !File.Exists(request), "Successful runtime activation did not replace its request with a license");
  }

  private static void AcceptedLoadRequiresValidLicense()
  {
    Native.ValidAfterLoad = false;
    Check(!LicenseManager.Load("<SoftwareKey>service:invalid") && !LicenseManager.LicenseInfo.IsValid && !NativeHandler.Instance.HasValidLicense, "Native acceptance was treated as a valid license");
  }

  private static void LoadExceptionsRefreshValidity()
  {
    Native.CurrentType = 0; Native.CurrentId = "old-license"; Native.Valid = true; Capture();
    Native.BeforeLoad = () => { Native.clear(); throw new IOException("synthetic load exception after native state change"); };
    Check(!LicenseManager.Load("<SoftwareKey>service:rejected"), "Load exception reported success");
    Check(!NativeHandler.Instance.HasValidLicense && !LicenseManager.LicenseInfo.IsValid, "Load exception left stale managed validity");
  }

  private static void SearchPreservesValidNativeLicense()
  {
    Native.CurrentType = 0; Native.CurrentId = "script-loaded-license"; Native.Valid = true; Capture();
    Native.LoadResult = false;
    Check(LicenseManager.LoadFile(), "Search rejected a valid native license with no files");
    Check(Native.isValid() && Native.CurrentId == "script-loaded-license" && NativeHandler.Instance.HasValidLicense, "Search cleared a script-loaded license and restored only its metadata");
    FileFor("invalid.lfx", "<SoftwareKey>service:rejected");
    Check(LicenseManager.LoadFile() && Native.LoadCalls == 0 && Native.isValid(), "Search replaced an already valid native license");
  }

  private static void FailedSearchUsesNativeState()
  {
    Native.CurrentType = 0; Native.CurrentId = "expired-old-license"; Native.Status = "Old expiration error"; Capture();
    Native.LoadResult = false; Native.Failure = "Current file rejected";
    FileFor("invalid.lfx", "<SoftwareKey>service:rejected");
    Check(!LicenseManager.LoadFile(), "Failed search reported success");
    Check(!LicenseManager.LicenseInfo.IsValid && !NativeHandler.Instance.HasValidLicense && LicenseManager.LicenseInfo.Status == Native.Failure, "Failed search restored previous license information instead of native state");
  }

  private static void EmptySearchUsesNativeState()
  {
    Native.CurrentType = 0; Native.CurrentId = "expired-old-license"; Native.Status = "Old expiration error"; Capture();
    Check(!LicenseManager.LoadFile(), "Empty search reported success");
    Check(!LicenseManager.LicenseInfo.IsParsed && LicenseManager.LicenseInfo.Status == Native.Status && !NativeHandler.Instance.HasValidLicense, "Empty search restored stale metadata");
  }

  private static void AssetImportWorkersCannotAccessLicensing()
  {
    var floating = FileFor("floating.lfx");
    var runtime = FileFor("runtime.rtlfx", "RT=synthetic");
    AssetDatabase.WorkerProcess = true;
    Set(typeof(LicenseManager), "s_isAssetImportWorkerProcess", null);
    agx.Runtime.InstanceCalls = 0;
    try {
      Check(!LicenseManager.CanAccessRuntime && !LicenseManager.HasFloatingSession, "Asset worker can access native session state");
      Check(!LicenseInfo.Create().IsParsed && !LicenseManager.LicenseInfo.IsParsed && !LicenseManager.UpdateLicenseInformation().IsParsed, "Asset worker obtained license information");
      Check(!LicenseManager.QueryInfo(floating).IsParsed, "Asset worker queried a license");
      Check(!LicenseManager.LoadFile() && !LicenseManager.LoadFile(false) && !LicenseManager.LoadFile(floating) && !LicenseManager.Load("<SoftwareKey>service:synthetic"), "Asset worker loaded a license");
      var previousTask = Get(typeof(LicenseManager), "s_operationTask");
      var rejected = 0;
      Action<bool> reject = success => { Check(!success, "Asset worker operation succeeded"); ++rejected; };
      LicenseManager.ConnectFloatingAsync(floating, reject);
      LicenseManager.ReturnFloatingAsync(reject);
      LicenseManager.ActivateAsync(1, "synthetic", "Assets", reject);
      LicenseManager.RefreshAsync(floating, reject);
      Check(rejected == 4 && !LicenseManager.IsBusy && ReferenceEquals(previousTask, Get(typeof(LicenseManager), "s_operationTask")), "Asset worker scheduled a licensing task or missed a callback");
      Check(!LicenseManager.Activate(1, "synthetic", "Assets") && !LicenseManager.Refresh(floating), "Asset worker activated or refreshed synchronously");
      Check(!LicenseManager.ActivateEncryptedRuntime("Assets") && !LicenseManager.ActivateEncryptedRuntime(runtime, "Assets"), "Asset worker activated a runtime license");
      Check(!LicenseManager.GenerateEncryptedRuntime(1, "synthetic", Directory.GetCurrentDirectory(), floating), "Asset worker generated runtime activation data");
      Check(!LicenseManager.GenerateOfflineActivation(1, "synthetic", "offline-request.txt") && !LicenseManager.CreateOfflineLicense("synthetic", "offline.lfx"), "Asset worker used offline licensing");
      Check(!LicenseManager.DeactivateLoaded() && !LicenseManager.DeactivateAndDelete(floating) && !LicenseManager.DeleteFile(floating), "Asset worker removed a license");
      LicenseManager.Reset();
      Check(!LicenseManager.ReturnFloating("Asset worker shutdown"), "Asset worker returned a session");
      Check(File.Exists(floating) && File.Exists(floating + ".meta") && File.Exists(runtime) && !File.Exists("Assets/agx.lfx"), "Asset worker modified licensing files");
      Check(agx.Runtime.InstanceCalls == 0, "Asset worker reached the native runtime");
    }
    finally {
      AssetDatabase.WorkerProcess = false;
      Set(typeof(LicenseManager), "s_isAssetImportWorkerProcess", null);
    }
  }
}
