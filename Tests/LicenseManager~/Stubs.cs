// Test-only substitutes. The production source files are compiled unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace agx
{
  public class LicenseInfo
  {
    public int licenseType;
    public string endDate = "2099-01-01", product = "AGX - Professional", user = "Test", contact = "Test", installationID = "";
    public string[] modules = { "AgX" };
  }
  public static class agxSWIG { public static string agxGetVersion(bool unused) => "agx-test"; }
  public class Runtime
  {
    public static Runtime Current = new Runtime();
    public static int InstanceCalls;
    public static Runtime instance()
    {
      Interlocked.Increment(ref InstanceCalls);
      if (UnityEditor.AssetDatabase.WorkerProcess)
        throw new InvalidOperationException("Native runtime accessed by an asset import worker");
      return Current;
    }
    public int OpenCalls, CloseCalls, LoadCalls, RefreshCalls, ActivateCalls, DeactivateCalls, QueryCalls;
    public int CurrentType = -1;
    public string CurrentId = "", Status = "", Failure = "Server unreachable", Extended = "Native operation detail";
    public bool Valid, OpenResult = true, CloseResult = true, LoadResult = true, RuntimeActivationResult = true, ValidAfterLoad = true, IsRefreshed;
    public Action BeforeOpen, BeforeClose, BeforeLoad;
    public bool isFloatingLicense() => CurrentType == 1;
    public bool isValid() => Valid;
    public string getStatus() => Status;
    public string getExtendedStatus() => Extended;
    public bool hasKey(string key) => key == "InstallationID" && CurrentType >= 0 && CurrentId.Length > 0;
    public string readValue(string key) => key == "InstallationID" ? CurrentId : key == "EndDate" ? "2099-01-01" : key == "Product" ? "AGX - Professional" : "";
    public string[] getEnabledModules() => new[] { "AgX" };
    public LicenseInfo queryLicenseInformation(string content)
    {
      ++QueryCalls;
      Status = "Status overwritten by metadata query";
      return new LicenseInfo { licenseType = content.Contains("floating") ? 1 : content.Contains("service") ? 0 : -1,
                               installationID = content.Contains(":") ? content.Split(':').Last() : "" };
    }
    public bool openNetworkSession(string file)
    {
      ++OpenCalls;
      BeforeOpen?.Invoke();
      if (!OpenResult) { Status = Failure; return false; }
      CurrentType = 1; Valid = true; CurrentId = File.ReadAllText(file).Split(':').Last(); Status = "";
      return true;
    }
    public bool closeNetworkSession()
    {
      ++CloseCalls;
      BeforeClose?.Invoke();
      if (!CloseResult) { Status = Failure; return false; }
      clear();
      return true;
    }
    public void clear() { CurrentType = -1; Valid = false; CurrentId = ""; Status = ""; }
    public bool loadLicenseString(string content)
    {
      ++LoadCalls;
      BeforeLoad?.Invoke();
      if (!LoadResult) { Status = Failure; return false; }
      if (content.Contains("fallback")) { Status = "floating license requires a session"; return false; }
      CurrentType = 0; Valid = ValidAfterLoad; CurrentId = content.Split(':').Last(); Status = ""; return true;
    }
    public bool loadLicenseFile(string file, bool refresh) { ++RefreshCalls; return loadLicenseString(File.ReadAllText(file)); }
    public bool isLicenseRefreshed() => IsRefreshed;
    public string readEncryptedLicense() => "<SoftwareKey>service:generated";
    public bool activateAgxLicense(int id, string password, string file)
    { ++ActivateCalls; File.WriteAllText(file, readEncryptedLicense()); return loadLicenseString(readEncryptedLicense()); }
    public bool deactivateAgxLicense() { ++DeactivateCalls; clear(); return true; }
    public bool unlock(string content) => loadLicenseString(content);
    public bool verifyAndUnlock(string content) => loadLicenseString(content);
    public bool activateEncryptedRuntime(string content, string file)
    {
      if (!RuntimeActivationResult) { Status = Failure; return false; }
      return activateAgxLicense(1, "", file);
    }
    public string encryptRuntimeActivation(int id, string password, string reference) => "RT=synthetic";
    public string generateOfflineActivationRequest(int id, string password) => "synthetic";
    public bool processOfflineActivationRequest(string content) => loadLicenseString(content);
  }
}
namespace agxIO
{
  public class Environment
  {
    public enum Type { RESOURCE_PATH }
    public static Environment instance() => new Environment();
    public Environment getFilePath(Type type) => this;
    public void pushbackPath(string path) { }
    public void removeFilePath(string path) { }
  }
}
namespace UnityEngine
{
  public class Object { }
  public class HelpURLAttribute : Attribute { public HelpURLAttribute(string url) { } }
  public class ExitGUIException : System.Exception { }
  public class UnityException : System.Exception { public UnityException(string message) : base(message) { } }
  public static class Application { public static bool isEditor = true, isBatchMode; public static string dataPath = "."; public static void OpenURL(string url) { } }
  public static class Debug
  {
    public static void Log(object message) { }
    public static void LogWarning(object message) { }
    public static void LogError(object message, Object context = null) { }
    public static void LogException(System.Exception error) { }
  }
  public struct Vector2 { public Vector2(float x, float y) { } public static Vector2 zero => default; }
  public struct Rect { public float x, y, width, height; }
  public struct Color { public static Color red, green; public static Color Lerp(Color a, Color b, float t) => a; }
  public class GUIStyleState { public object background; }
  public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle style) { } public GUIStyleState normal = new GUIStyleState(); }
  public class GUISkin { public GUIStyle[] customStyles = { null, null, null, null }; }
  public class GUIContent { public string text, tooltip; public GUIContent(string t, string tip = "") { text = t; tooltip = tip; } }
  public static class GUI
  {
    public static bool enabled = true;
    public static void FocusControl(string control) { }
    public static bool Button(Rect rect, GUIContent content, GUIStyle style) => TestUI.Button(content);
  }
  public static class GUILayout
  {
    public static object Width(float width) => null;
    public static object Height(float height) => null;
    public static void Box(object content, GUIStyle style, params object[] options) { }
    public static void Space(float space) { }
    public static void Label(GUIContent content, GUIStyle style) { }
    public static bool Button(GUIContent content, GUIStyle style) => TestUI.Button(content);
  }
  public static class GUILayoutUtility { public static Rect GetLastRect() => default; }
  public static class GUIUtility { public static void ExitGUI() => throw new ExitGUIException(); }
  public static class Resources { public static T[] FindObjectsOfTypeAll<T>() => Array.Empty<T>(); }
}
namespace UnityEditor
{
  using UnityEngine;
  public class InitializeOnLoadMethodAttribute : Attribute { }
  public enum PlayModeStateChange { ExitingEditMode, EnteredPlayMode, ExitingPlayMode, EnteredEditMode }
  public enum MessageType { Info, Warning, Error }
  public static class SessionState
  {
    public static int MainThread = Thread.CurrentThread.ManagedThreadId;
    public static Dictionary<string, bool> Values = new Dictionary<string, bool>();
    public static bool GetBool(string key, bool fallback) { CheckThread(); return Values.TryGetValue(key, out var value) ? value : fallback; }
    public static void SetBool(string key, bool value) { CheckThread(); Values[key] = value; }
    private static void CheckThread() { if (Thread.CurrentThread.ManagedThreadId != MainThread) throw new System.Exception("SessionState accessed from worker"); }
  }
  public static class EditorApplication
  {
    public static event Action update;
    public static event Action<PlayModeStateChange> playModeStateChanged;
    public static bool isCompiling, isUpdating, isPlayingOrWillChangePlaymode;
    public static void Tick() => update?.Invoke();
    public static void PlayTransition(PlayModeStateChange state) => playModeStateChanged?.Invoke(state);
  }
  public static class AssemblyReloadEvents { public static event Action beforeAssemblyReload; public static void Reload() => beforeAssemblyReload?.Invoke(); }
  public static class AssetDatabase
  {
    public static bool WorkerProcess;
    public static bool IsAssetImportWorkerProcess()
    {
      if (Thread.CurrentThread.ManagedThreadId != SessionState.MainThread)
        throw new UnityException("AssetDatabase accessed outside the editor thread");
      return WorkerProcess;
    }
    public static void Refresh() { }
  }
  public class EditorWindow : Object
  {
    public Vector2 minSize;
    public Rect position;
    public static T GetWindow<T>(bool utility, string title, bool focus) where T : new() => new T();
    public void ShowNotification(GUIContent message) { }
    public void RemoveNotification() { }
    public void Repaint() { }
  }
  public static class EditorGUIUtility { public static float standardVerticalSpacing; }
  public static class EditorGUI { public static Rect IndentedRect(Rect rect) => rect; }
  public static class EditorGUILayout
  {
    public sealed class VerticalScope : IDisposable { public VerticalScope(GUIStyle style) { } public void Dispose() { } }
    public static void LabelField(string text, GUIStyle style) { }
    public static void HelpBox(string text, MessageType type, bool wide) => TestUI.Messages.Add((text, type));
    public static Vector2 BeginScrollView(Vector2 value) => value;
    public static void EndScrollView() { }
    public static string TextField(GUIContent label, string value, GUIStyle style) => value;
    public static string PasswordField(GUIContent label, string value) => value;
    public static Rect GetControlRect() => default;
    public static void EnumFlagsField(GUIContent label, Enum value, bool includeObsolete, GUIStyle style) { }
  }
  public static class EditorUtility
  {
    public static string OpenFilePanel(string title, string directory, string extension) => TestUI.ImportFile;
    public static bool DisplayDialog(string title, string text, string ok, string cancel)
    { TestUI.Dialogs.Add(string.Join(" | ", title, text, ok, cancel)); return TestUI.ConfirmDialog; }
    public static int DisplayDialogComplex(string title, string text, string ok, string cancel, string alternate)
    { TestUI.Dialogs.Add(string.Join(" | ", title, text, ok, cancel, alternate)); return TestUI.DialogChoice; }
  }
}
namespace AGXUnity
{
  public class Exception : System.Exception { public Exception(string text) : base(text) { } }
  public class NativeHandler
  {
    public static bool HasInstance = true;
    public static NativeHandler Instance = new NativeHandler();
    public bool Initialized = true, HasValidLicense;
    public void ValidateLicense() => HasValidLicense = agx.Runtime.instance().isValid();
  }
}
namespace AGXUnity.IO
{
  public static class Environment
  {
    public static string FindUniqueFilename(string file) => File.Exists(file) ? file + ".copy" + Path.GetExtension(file) : file;
    public static string GetPlayerPluginPath(string path) => path;
    public static bool CanWriteToExisting(string file) => true;
  }
}
namespace AGXUnity.Utils
{
  using UnityEngine;
  public static class Extensions
  {
    public static string PrettyPath(this string path) => path.Replace('\\', '/');
    public static string MakeRelative(this string path, string root, bool unused) => Path.GetRelativePath(root, path);
    public static string SplitCamelCase(this string text) => text;
    public static string Color(this string text, Color color) => text;
  }
  public static class GUI
  {
    public sealed class EnabledBlock : IDisposable
    {
      private readonly bool previous = UnityEngine.GUI.enabled;
      public EnabledBlock(bool value) { UnityEngine.GUI.enabled = value; }
      public void Dispose() { UnityEngine.GUI.enabled = previous; }
    }
    public sealed class Scope : IDisposable { public void Dispose() { } }
    public static class AlignBlock { public static Scope Center => new Scope(); }
    public static GUISkin Skin = new GUISkin();
    public static GUIContent MakeLabel(string text, bool bold = false, string tooltip = "") => new GUIContent(text, tooltip);
    public static object CreateColoredTexture(int w, int h, Color color) => new object();
  }
}
namespace AGXUnityEditor
{
  using UnityEngine;
  public enum MiscIcon { Locate, Update, EntryRemove }
  public static class IconManager { public static object GetAGXUnityLogo() => null; }
  public static class InspectorEditor
  {
    public static SkinData Skin = new SkinData();
    public class SkinData { public GUIStyle Label, LabelMiddleCenter, TextField, Button, Popup; }
  }
  public static class InspectorGUISkin { public static Color BrandColorBlue; }
  public static class InspectorGUI
  {
    public static Color BackgroundColor;
    public static class IndentScope { public static AGXUnity.Utils.GUI.Scope Single => new AGXUnity.Utils.GUI.Scope(); }
    public struct MiscButtonData
    {
      public MiscIcon Icon;
      public Action Action;
      public bool Enabled;
      public string Tooltip;
      public static MiscButtonData Create(MiscIcon icon, Action action, bool enabled, string tooltip) => new MiscButtonData { Icon = icon, Action = action, Enabled = enabled, Tooltip = tooltip };
    }
    public static void BrandSeparator(int a, int b) { }
    public static void Separator(int a, int b) { }
    public static void Separator(int a, int b, Color c) { }
    public static void LicenseEndDateField(AGXUnity.LicenseInfo info) { }
    public static bool Link(GUIContent content) => false;
    public static bool Button(Rect rect, MiscIcon icon, bool enabled, string tooltip) => enabled && TestUI.ImportFile != null;
    public static void SelectFolder(GUIContent label, string path, string title, Action<string> selected) { }
    public static void SelectableTextField(GUIContent label, string value, params MiscButtonData[] buttons)
    {
      TestUI.Fields.Add((label.text, value));
      TestUI.MiscButtons.AddRange(buttons);
    }
  }
  public class EditorDataEntry { public string String; }
  public class EditorData
  {
    public static EditorData Instance = new EditorData();
    public EditorDataEntry Entry = new EditorDataEntry { String = "Assets" };
    public EditorDataEntry GetStaticData(string key, Action<EditorDataEntry> init) => Entry;
  }
}
namespace AGXUnityEditor.IO { public static class Utils { public static bool IsValidProjectFolder(string path) => true; } }
public static class TestUI
{
  public static List<(string Text, bool Enabled, string Tooltip)> Buttons = new List<(string, bool, string)>();
  public static List<(string Text, UnityEditor.MessageType Type)> Messages = new List<(string, UnityEditor.MessageType)>();
  public static List<(string Label, string Value)> Fields = new List<(string, string)>();
  public static List<AGXUnityEditor.InspectorGUI.MiscButtonData> MiscButtons = new List<AGXUnityEditor.InspectorGUI.MiscButtonData>();
  public static List<string> Dialogs = new List<string>();
  public static bool ConfirmDialog;
  public static int DialogChoice = 1;
  public static string PressButton, ImportFile;
  public static bool Button(UnityEngine.GUIContent content)
  {
    Buttons.Add((content.text, UnityEngine.GUI.enabled, content.tooltip));
    if (UnityEngine.GUI.enabled && PressButton == content.text) { PressButton = null; return true; }
    return false;
  }
  public static void Reset()
  {
    Buttons.Clear(); Messages.Clear(); Fields.Clear(); MiscButtons.Clear(); Dialogs.Clear();
    ConfirmDialog = false; DialogChoice = 1; PressButton = null; ImportFile = null; UnityEngine.GUI.enabled = true;
  }
}
