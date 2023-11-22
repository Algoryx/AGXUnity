using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AGXUnity.Utils;
using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  /// <summary>
  /// Manager object, initialized when the Unity editor is loaded, to handle
  /// all tools, behavior related etc. objects while in edit mode.
  /// </summary>
  [InitializeOnLoad]
  public static class Manager
  {
    /// <summary>
    /// The game object mouse is currently over in scene view. Hidden objects,
    /// e.g., VisualPrimitive isn't included in this.
    /// </summary>
    public static GameObject MouseOverObject { get; private set; }

    /// <summary>
    /// True if the current event is left mouse down.
    /// </summary>
    public static bool LeftMouseClick { get; private set; }

    /// <summary>
    /// True if the current event is right mouse down.
    /// </summary>
    public static bool RightMouseClick { get; private set; }

    /// <summary>
    /// True if the right mouse button is pressed (and hold).
    /// </summary>
    public static bool RightMouseDown { get; private set; }

    /// <summary>
    /// True if keyboard escape key is down.
    /// </summary>
    public static bool KeyEscapeDown { get; private set; }

    /// <summary>
    /// True if mouse + key combo is assumed to be a camera control move.
    /// </summary>
    public static bool IsCameraControl { get; private set; }

    /// <summary>
    /// Name of assembly AGXUnity is built in to.
    /// </summary>
    public const string AGXUnityAssemblyName = "AGXUnity";

    /// <summary>
    /// Name of assembly AGXUnityEditor is built in to.
    /// </summary>
    public const string AGXUnityEditorAssemblyName = "AGXUnityEditor";

    /// <summary>
    /// Scene view window handler, i.e., GUI windows rendered in Scene View.
    /// </summary>
    public static GUIWindowHandler SceneViewGUIWindowHandler { get; private set; } = new GUIWindowHandler();

    /// <summary>
    /// Constructor called when the Unity editor is initialized.
    /// </summary>
    static Manager()
    {
      IO.Utils.VerifyDirectories();

      GetRequestScriptReloadData().Float = -1;

      m_environmentState = ConfigureEnvironment();
      if ( m_environmentState == EnvironmentState.Updating )
        return;

      // If compatibility issues, this method will try to fix them and this manager
      // will probably be loaded again after the fix.
      if ( m_environmentState == EnvironmentState.Initialized && !VerifyCompatibility() )
        return;

#if UNITY_2019_1_OR_NEWER
      SceneView.duringSceneGui += OnSceneView;
#else
      SceneView.onSceneGUIDelegate += OnSceneView;
#endif

#if UNITY_2018_1_OR_NEWER
      EditorApplication.hierarchyChanged += OnHierarchyWindowChanged;
#else
      EditorApplication.hierarchyWindowChanged += OnHierarchyWindowChanged;
#endif

      Selection.selectionChanged += OnSelectionChanged;

      DragDropListener.OnPrefabsDroppedInScene += OnPrefabsDroppedInScene;

      while ( VisualsParent != null && VisualsParent.transform.childCount > 0 )
        GameObject.DestroyImmediate( VisualsParent.transform.GetChild( 0 ).gameObject );

      MouseOverObject = null;

      Undo.undoRedoPerformed += UndoRedoPerformedCallback;

      // Focus on scene view for events to be properly handled. E.g.,
      // holding one key and click in scene view is not working until
      // scene view is focused since some other event is taking the
      // key event.
      RequestSceneViewFocus();

      CreateDefaultAssets();

      PrefabUtility.prefabInstanceUpdated += AssetPostprocessorHandler.OnPrefabCreatedFromScene;
    }

    public enum EditorWindowType
    {
      InspectorWindow,
      SceneHierarchyWindow,
      SceneView,
      GameView,
      ProjectBrowser
    }

    /// <summary>
    /// Finds which window the mouse is currently hovering given type name
    /// of the window class (including name space). E.g., "UnityEngine.InspectorWindow".
    /// </summary>
    /// <param name="windowClassName">Window class name, including name space.</param>
    /// <returns>True if the mouse is hovering given window type name, otherwise false.</returns>
    public static bool IsMouseOverWindow( string windowClassName )
    {
      try {
        var mouseOverWindow = EditorWindow.mouseOverWindow;
        return mouseOverWindow != null && mouseOverWindow.GetType().FullName == windowClassName;
      }
      catch ( Exception ) {
        // EditorWindow.mouseOverWindow can throw null pointer exceptions at random.
        return false;
      }
    }

    /// <summary>
    /// Finds if mouse is hovering given editor window type.
    /// </summary>
    /// <param name="windowType">Editor window type.</param>
    /// <returns>True if the mouse is hovering given window type, otherwise false.</returns>
    public static bool IsMouseOverWindow( EditorWindowType windowType )
    {
      return IsMouseOverWindow( "UnityEditor." + windowType.ToString() );
    }

    /// <summary>
    /// Finds if mouse is hovering given editor window instance.
    /// </summary>
    /// <param name="editorWindow">Editor window instance.</param>
    /// <returns>True if the mouse is hovering given editor window instance, otherwise false.</returns>
    public static bool IsMouseOverWindow( EditorWindow editorWindow )
    {
      try {
        return editorWindow != null && EditorWindow.mouseOverWindow == editorWindow;
      }
      catch ( Exception ) {
        return false;
      }
    }

    /// <summary>
    /// Data that tracks certain events when we're hijacking left mouse button.
    /// </summary>
    private class HijackLeftMouseClickData
    {
      public bool AltPressed { get; set; }

      public HijackLeftMouseClickData()
      {
        AltPressed = false;
      }
    };

    /// <summary>
    /// Hijacks left mouse down from the editor and returns true when the button
    /// is released. This is the default behavior of the editor (select @ mouse up)
    /// and it's, without this method, impossible to detect mouse up events.
    /// </summary
    /// <remarks>
    /// Using this method disables the editor default selection behavior.
    /// </remarks>
    /// <returns>True when the hijacked mouse down button is released (i.e., EventType.MouseUp).</returns>
    public static bool HijackLeftMouseClick()
    {
      Event current = Event.current;
      if ( current == null ) {
        Debug.LogError( "Hijack Left Mouse Click can only be used in the GUI event loop." );
        return false;
      }

      EventType currentMouseEventType = current.GetTypeForControl( GUIUtility.GetControlID( FocusType.Passive ) );
      bool hijackMouseDown = currentMouseEventType == EventType.MouseDown &&
                             current.button == 0 &&
                            !RightMouseDown &&                                // button 1 is FPS camera movement
                            !current.alt;                                     // alt down is track ball camera movement
      if ( hijackMouseDown ) {
        m_hijackLeftMouseClickData = new HijackLeftMouseClickData();
        GUIUtility.hotControl = 0;
        Event.current.Use();
        return false;
      }

      if ( m_hijackLeftMouseClickData != null ) {
        m_hijackLeftMouseClickData.AltPressed |= current.alt;

        bool leftMouseUp = !m_hijackLeftMouseClickData.AltPressed &&
                            currentMouseEventType == EventType.MouseUp &&
                            Event.current.button == 0;

        if ( currentMouseEventType == EventType.MouseUp )
          m_hijackLeftMouseClickData = null;

        return leftMouseUp;
      }

      return false;
    }

    /// <summary>
    /// Call this method to reset key escape flag, i.e, KeyEscapeDown == false after the call.
    /// </summary>
    public static void UseKeyEscapeDown()
    {
      KeyEscapeDown = false;
    }

    public static bool IsKeyEscapeDown( Event current )
    {
      return current != null && current.isKey && current.keyCode == KeyCode.Escape && current.type == EventType.KeyUp;
    }

    /// <summary>
    /// Request focus of the scene view window. E.g., when a button is pressed
    /// in the inspector tab and objects in the scene view should respond.
    /// </summary>
    public static void RequestSceneViewFocus()
    {
      m_requestSceneViewFocus = true;
    }

    /// <summary>
    /// Routes current object to the desired object when e.g., selected.
    /// This method uses OnSelectionProxy to find the desired object.
    /// </summary>
    /// <returns>Input object if the object doesn't contains an OnSelectionProxy route.</returns>
    public static UnityEngine.Object RouteObject( UnityEngine.Object obj )
    {
      GameObject gameObject = obj as GameObject;
      OnSelectionProxy proxy = null;
      if ( gameObject != null )
        proxy = gameObject.GetComponents<OnSelectionProxy>().FirstOrDefault();
      // If proxy target is null we're ignoring it.
      var result = proxy != null &&
                  !GetSelectedInHierarchyData( proxy ).Bool &&
                   proxy.Target != null ?
                     proxy.Target :
                     obj;
      return result;
    }

    /// <summary>
    /// Routes given object to the game object of an AGXUnity.Collide.Shape if
    /// the connection is given using OnSelectionProxy.
    /// </summary>
    /// <returns>Shape game object if found - otherwise null.</returns>
    public static GameObject RouteToShape( UnityEngine.Object obj )
    {
      GameObject gameObject = obj as GameObject;
      OnSelectionProxy selectionProxy = null;
      if ( gameObject == null || ( selectionProxy = gameObject.GetComponent<OnSelectionProxy>() ) == null )
        return null;

      if ( selectionProxy.Target != null && selectionProxy.Target.GetComponent<AGXUnity.Collide.Shape>() != null )
        return selectionProxy.Target;

      return null;
    }

    /// <summary>
    /// Get or create default shape visuals material.
    /// </summary>
    /// <returns>Material asset.</returns>
    public static Material GetOrCreateShapeVisualDefaultMaterial()
    {
      return GetOrCreateAsset<Material>( IO.Utils.AGXUnityResourceDirectory +
                                         '/' + AGXUnity.Rendering.ShapeVisual.DefaultMaterialPathResources + ".mat",
                                         () => AGXUnity.Rendering.ShapeVisual.CreateDefaultMaterial() );
    }

    public static void OnVisualPrimitiveNodeCreate( Utils.VisualPrimitive primitive )
    {
      if ( primitive == null || primitive.Node == null )
        return;

      // TODO: Fix so that "MouseOver" works for newly created primitives.
     if ( primitive.Node.transform.parent != VisualsParent )
        VisualsParent.AddChild( primitive.Node );

      m_visualPrimitives.Add( primitive );
    }

    public static void OnVisualPrimitiveNodeDestruct( Utils.VisualPrimitive primitive )
    {
      if ( primitive == null || primitive.Node == null )
        return;

      primitive.Node.transform.parent = null;
      m_visualPrimitives.Remove( primitive );

      GameObject.DestroyImmediate( primitive.Node );
    }

    internal static bool HasPlayerNetCompatibilityIssueWarning()
    {
      return HasPlayerNetCompatibility( "warning" );
    }

    internal static bool HasPlayerNetCompatibilityIssueError()
    {
      return HasPlayerNetCompatibility( "error" );
    }

    private static bool HasPlayerNetCompatibility( string infoWarningOrError )
    {
      // WARNING INFO:
      //     Unity 2018, 2019: AGX Dynamics for Unity compiles but undefined behavior
      //                       in players with API compatibility @ .NET Standard 2.0.
      if ( PlayerSettings.GetApiCompatibilityLevel( BuildTargetGroup.Standalone ) != ApiCompatibilityLevel.NET_4_6 ) {
        var apiCompatibilityLevelName =
#if UNITY_2021_2_OR_NEWER
          ".NET Framework";
#else
          ".NET 4.x";
#endif
        string prefix = string.Empty;
        if ( infoWarningOrError == "info" )
          prefix = AGXUnity.Utils.GUI.AddColorTag( "<b>INFO:</b> ", Color.white );
        else if ( infoWarningOrError == "warning" )
          prefix = AGXUnity.Utils.GUI.AddColorTag( "<b>WARNING:</b> ", Color.yellow );
        else
          prefix = AGXUnity.Utils.GUI.AddColorTag( "<b>ERROR:</b> ", Color.red );

        var message = prefix +
                      $"AGX Dynamics for Unity requires .NET API compatibility level: {apiCompatibilityLevelName}.\n" +
                      $"<b>AGXUnity -> Settings -> .NET Compatibility Level</b>";
        if ( infoWarningOrError == "info" )
          Debug.Log( message );
        else if ( infoWarningOrError == "warning" )
          Debug.LogWarning( message );
        else
          Debug.LogError( message );

        return false;
      }

      return true;
    }

    private static string m_currentSceneName = string.Empty;
    private static int m_numScenesLoaded = 0;
    private static bool m_requestSceneViewFocus = false;
    private static HijackLeftMouseClickData m_hijackLeftMouseClickData = null;

    private static string m_visualParentName = "Manager".To32BitFnv1aHash().ToString();
    private static GameObject m_visualsParent = null;
    private static HashSet<Utils.VisualPrimitive> m_visualPrimitives = new HashSet<Utils.VisualPrimitive>();

    private static EnvironmentState m_environmentState = EnvironmentState.Unknown;

    public static GameObject VisualsParent
    {
      get
      {
        if ( m_visualsParent == null ) {
          m_visualsParent = GameObject.Find( m_visualParentName ) ?? new GameObject( m_visualParentName );
          m_visualsParent.hideFlags = HideFlags.HideAndDontSave;
        }

        PrefabUtils.PlaceInCurrentStange( m_visualsParent );

        return m_visualsParent;
      }
    }

    /// <summary>
    /// Callback when undo or redo has been performed. There's a significant
    /// delay to e.g., Inspector update when this happens so we're explicitly
    /// telling Unity to update selected object (if ScriptComponent).
    /// </summary>
    private static void UndoRedoPerformedCallback()
    {
      // Trigger repaint of inspector GUI for our targets.
      var targets = ToolManager.ActiveTools.SelectMany( tool => tool.Targets );
      foreach ( var target in targets )
        if ( target != null )
          EditorUtility.SetDirty( target );

      // Collecting scripts that may require synchronize of
      // data post undo/redo where the private serialized
      // field has been changed but the public property with
      // native synchronizations isn't touched.
      if ( EditorApplication.isPlaying ) {
        var objectsToSynchronize = new List<Object>();
        Action<Object> addUnique = obj =>
        {
          if ( !objectsToSynchronize.Contains( obj ) )
            objectsToSynchronize.Add( obj );
        };
        foreach ( var obj in Selection.objects ) {
          if ( obj is AGXUnity.ScriptAsset )
            addUnique( obj );
          else if ( obj is GameObject ) {
            var scripts = ( obj as GameObject ).GetComponents<AGXUnity.ScriptComponent>();
            foreach ( var script in scripts )
              addUnique( script );
          }
        }

        foreach ( var obj in objectsToSynchronize )
          PropertySynchronizer.Synchronize( obj );
      }

      // Shapes or bodies doesn't have to be selected when having their
      // size updated, due to the recursive editors. We know that one of
      // their parent is though (since selection is included in undo/redo),
      // so find shapes in children of the selection and access bodies
      // from there as well.
      //
      // Note that UpdateMassProperties is a time consuming operation. Ideally
      // we would like to know if operations has been made that affects the
      // mass properties of bodies.
      var shapesInSelection = Selection.GetFiltered<GameObject>( SelectionMode.TopLevel |
                                                                 SelectionMode.Editable )
                                       .SelectMany( go => go.GetComponentsInChildren<AGXUnity.Collide.Shape>() );
      foreach ( var shape in shapesInSelection ) {
        var visual = AGXUnity.Rendering.ShapeVisual.Find( shape );
        if ( visual != null )
          visual.OnSizeUpdated();
      }

      // Looking at the number of shapes to begin with because it can
      // be one rigid body with many shapes. The thing is that the
      // undo/redo operation is highly unlikely to be affecting the
      // mass properties. And note that the mass properties won't be
      // wrong in the simulation due to this, only displayed wrong
      // until simulation.
      var updateMassProperties = shapesInSelection.Count() < 32;
      if ( updateMassProperties ) {
        var bodiesInSelection = Selection.GetFiltered<GameObject>( SelectionMode.TopLevel |
                                                                   SelectionMode.Editable )
                                         .SelectMany( go => go.GetComponentsInChildren<AGXUnity.RigidBody>() );
        foreach ( var rb in bodiesInSelection )
          rb.UpdateMassProperties();
      }

      foreach ( var customTargetTool in ToolManager.ActiveTools )
        customTargetTool.OnUndoRedo();

      if ( targets.Count() > 0 )
        SceneView.RepaintAll();

      // When a prefab is drag-dropped into a scene we receive a
      // callback to this method for unknown reasons. Event.current == null
      // when that's the case, i.e., not an actual undo/redo.
      if ( Event.current != null )
        s_lastUndoRedoTime = EditorApplication.timeSinceStartup;
    }

    private static double s_lastUndoRedoTime = 0;

    /// <summary>
    /// Callback from InspectorEditor when in OnDisable and target == null,
    /// meaning the target has been deleted.
    /// </summary>
    public static void OnEditorTargetsDeleted()
    {
      var undoGroupId = Undo.GetCurrentGroup();

      // Deleted RigidBody component leaves dangling MassProperties
      // so we've to delete them explicitly.
      var mps = Object.FindObjectsOfType<AGXUnity.MassProperties>();
      foreach ( var mp in mps ) {
        if ( mp.RigidBody == null ) {
          Undo.DestroyObjectImmediate( mp );
        }
      }

      Undo.CollapseUndoOperations( undoGroupId );
    }

    private static void OnSceneView( SceneView sceneView )
    {
      if ( m_environmentState != EnvironmentState.Initialized ) {
        var prev = m_environmentState;
        m_environmentState = ConfigureEnvironment();
        if ( prev != m_environmentState ) {
          Debug.LogWarning( $"Environment state changed from {prev} to {m_environmentState}." );
        }
        if ( m_environmentState != EnvironmentState.Initialized )
          return;
      }

      if ( m_requestSceneViewFocus ) {
        sceneView.Focus();
        m_requestSceneViewFocus = false;
      }

      Event current = Event.current;
      LeftMouseClick = !current.control && !current.shift && !current.alt && current.type == EventType.MouseDown && current.button == 0;
      KeyEscapeDown = IsKeyEscapeDown( current );
      RightMouseClick = current.type == EventType.MouseDown && current.button == 1;

      if ( RightMouseClick )
        RightMouseDown = true;
      if ( current.type == EventType.MouseUp && current.button == 1 )
        RightMouseDown = false;

      IsCameraControl = current.alt || RightMouseDown;

      foreach ( var primitive in m_visualPrimitives )
        primitive.OnSceneView( sceneView );

      UpdateMouseOverPrimitives( current );

      ToolManager.HandleOnSceneViewGUI( sceneView );

      HandleWindowsGUI( sceneView );

      LeftMouseClick = false;

      if ( EditorData.Instance.SecondsSinceLastGC > 5.0 * 60 )
        EditorData.Instance.GC();
    }

#if UNITY_2020_1_OR_NEWER
    private static double s_lastPickGameObjectTime = 0.0;
#endif

    private static bool TimeToPick()
    {
      // We receive many calls to UpdateMouseOverPrimitives from
      // OnSceneView in Unity 2020 when moving the mouse over the
      // scene view. HandleUtility.PickGameObject will update gizmos
      // and that takes a lot of time (e.g., a scene with many
      // constraints). With this we're only calling PickGameObject
      // in at maximum 10 times per second.
#if UNITY_2020_1_OR_NEWER
      if ( EditorApplication.timeSinceStartup - s_lastPickGameObjectTime > 0.1 ) {
        s_lastPickGameObjectTime = EditorApplication.timeSinceStartup;
        return true;
      }
      return false;
#else
      return true;
#endif
    }

    public static void UpdateMouseOverPrimitives( Event current, bool forced = false )
    {
      // Can't perform picking during repaint event.
      if ( current == null || !( current.isMouse || current.isKey || forced ) )
        return;

      // Update mouse over before we reveal the VisualPrimitives.
      // NOTE: We're putting our "visual primitives" in the ignore list.
      if ( current.isMouse || forced ) {
        var ignoreList = new List<GameObject>();
        foreach ( var primitive in m_visualPrimitives ) {
          if ( !primitive.Visible )
            continue;

          var primitiveFilters = primitive.Node.GetComponentsInChildren<MeshFilter>();
          ignoreList.AddRange( primitiveFilters.Select( pf => { return pf.gameObject; } ) );
        }

        // If the mouse is hovering a scene view window - MouseOverObject should be null.
        if ( SceneViewGUIWindowHandler.GetMouseOverWindow( current.mousePosition ) != null )
          MouseOverObject = null;
        else if ( forced || TimeToPick() ) {
          MouseOverObject = RouteObject( HandleUtility.PickGameObject( current.mousePosition,
                                                                       false,
                                                                       ignoreList.ToArray() ) ) as GameObject;
        }
      }

      // Early exit if we haven't any active visual primitives.
      if ( m_visualPrimitives.Count == 0 )
        return;

      var primitiveHitList = new[] { new { Primitive = (Utils.VisualPrimitive)null, RaycastResult = Utils.Raycast.Result.Invalid } }.ToList();
      primitiveHitList.Clear();

      var mouseRay = HandleUtility.GUIPointToWorldRay( current.mousePosition );
      foreach ( var primitive in m_visualPrimitives ) {
        primitive.MouseOver = false;

        if ( !primitive.Pickable )
          continue;

        var result = Utils.Raycast.Intersect( mouseRay, primitive.Node, true );
        if ( result )
          primitiveHitList.Add( new { Primitive = primitive, RaycastResult = result } );
      }

      if ( primitiveHitList.Count == 0 )
        return;

      var bestResult = primitiveHitList[ 0 ];
      for ( int i = 1; i < primitiveHitList.Count; ++i )
        if ( primitiveHitList[ i ].RaycastResult.Distance < bestResult.RaycastResult.Distance )
          bestResult = primitiveHitList[ i ];

      bestResult.Primitive.MouseOver = true;
      if ( HijackLeftMouseClick() )
        bestResult.Primitive.OnMouseClick( bestResult.RaycastResult, bestResult.Primitive );
    }

    private static void OnHierarchyWindowChanged()
    {
      var scene = EditorSceneManager.GetActiveScene();
      var currNumScenesLoaded =
#if UNITY_2022_2_OR_NEWER
                                UnityEngine.SceneManagement.SceneManager.loadedSceneCount;
#else
                                EditorSceneManager.loadedSceneCount;
#endif
      var isSceneLoaded = scene.name != m_currentSceneName ||
                          // Drag drop of scene into hierarchy.
                          currNumScenesLoaded > m_numScenesLoaded;

      if ( isSceneLoaded ) {
        EditorData.Instance.GC();

        m_currentSceneName = scene.name;

        AutoUpdateSceneHandler.HandleUpdates( scene );
      }

      m_numScenesLoaded = currNumScenesLoaded;
    }

    /// <summary>
    /// Previous selection used to reset used EditorDataEntry entries.
    /// </summary>
    private static Object[] m_previousSelection = new Object[] { };

    /// <summary>
    /// Editor data entry for "SelectedInHierarchy" property.
    /// </summary>
    /// <param name="proxy">OnSelectionProxy instance. Invalid if null.</param>
    /// <returns>EditorDataEntry for given <paramref name="proxy"/>.</returns>
    private static EditorDataEntry GetSelectedInHierarchyData( OnSelectionProxy proxy )
    {
      return EditorData.Instance.GetData( proxy, "SelectedInHierarchy" );
    }

    /// <summary>
    /// Callback when selection has been changed in the editor. Mainly used to
    /// catch when the user selects an OnSelectionProxy route in the hierarchy
    /// tab, i.e., such that it shouldn't be routed when clicking in hierarchy.
    /// </summary>
    private static void OnSelectionChanged()
    {
      // If the active tool is hiding the position/rotation/scale handles
      // we ignore this 'auto-hiding'.
      if ( !ToolManager.IsHidingDefaultTools ) {
        bool mouseOverHierarchy = EditorWindow.mouseOverWindow != null &&
                                  EditorWindow.mouseOverWindow.GetType().FullName == "UnityEditor.SceneHierarchyWindow";

        // Assigns and saves 'state' in editor data for game object with OnSelectionProxy.
        // If OnSelectionProxy is present the given state is returned.
        Func<GameObject, bool, bool> setOnSelectionProxyState = ( go, state ) =>
        {
          var proxy = go != null ? go.GetComponent<OnSelectionProxy>() : null;
          if ( proxy != null )
            return GetSelectedInHierarchyData( proxy ).Bool = state;
          return false;
        };

        // Reset previously selected as "not selected in hierarchy".
        foreach ( var prevSelected in m_previousSelection ) {
          // Could be deleted - only valid to check if null.
          if ( prevSelected == null )
            continue;

          setOnSelectionProxyState( prevSelected as GameObject, false );
        }

        bool toolsHidden = false;
        // If newly selected object(s) are selected in the hierarchy window we shouldn't
        // route it in this.RouteObject.
        foreach ( var selected in Selection.objects )
          toolsHidden = setOnSelectionProxyState( selected as GameObject, mouseOverHierarchy ) || toolsHidden;

        // Hides transform tool when e.g., DebugRenderManager is selected.
        if ( !toolsHidden &&
             Selection.activeGameObject != null &&
             ( Selection.activeGameObject.transform.hideFlags & HideFlags.NotEditable ) != 0 )
          toolsHidden = true;

        UnityEditor.Tools.hidden = toolsHidden;
      }

      m_previousSelection = Selection.objects;

      if ( Selection.objects.Length == 0 )
        ToolManager.ReleaseAllRecursiveEditors();
    }

    private static void OnPrefabsDroppedInScene( GameObject[] instances )
    {
      var ourInstances = instances.Where( instance =>
                                            instance != null &&
                                            ( instance.GetComponentInChildren<AGXUnity.IO.RestoredAGXFile>() != null ||
                                              instance.GetComponentInChildren<AGXUnity.IO.SavedPrefabLocalData>() != null ) );
      if ( ourInstances.Count() == 0 )
        return;

      foreach ( var instance in ourInstances )
        Undo.ClearUndo( instance );

      Undo.SetCurrentGroupName( "Adding prefab instance(s) to scene." );
      var groupId = Undo.GetCurrentGroup();
      foreach ( var instance in ourInstances ) {
        AssetPostprocessorHandler.OnPrefabAddedToScene( instance );
      }
      Undo.CollapseUndoOperations( groupId );
    }


    private static void HandleWindowsGUI( SceneView sceneView )
    {
      if ( SceneViewGUIWindowHandler.RenderWindows( Event.current ) )
        SceneView.RepaintAll();
    }

    internal enum EnvironmentState
    {
      Unknown,
      Updating,
      Uninitialized,
      Initialized
    }

    internal static EnvironmentState ConfigureEnvironment()
    {
#if AGXUNITY_UPDATING
      Debug.LogWarning( "AGX Dynamics for Unity is currently updating..." );
      return EnvironmentState.Updating;
#else
      // Running from within the editor - two options:
      //   1. Unity has been started from an AGX environment => do nothing.
      //   2. AGX Dynamics dll's are present in the plugins directory => setup
      //      environment file paths.
      var binariesInProject = IO.Utils.AGXDynamicsInstalledInProject;
      if ( binariesInProject ) {
        // This is necessary when e.g., terrain dynamically loads dll's.
        AGXUnity.IO.Environment.AddToPath( IO.Utils.AGXUnityPluginDirectoryFull );

        var initSuccess = false;
        try {
          AGXUnity.NativeHandler.Instance.Register( null );

          initSuccess = true;
        }
        catch ( Exception ) {
        }

        if ( !HandleScriptReload( initSuccess ) )
          return EnvironmentState.Uninitialized;

        if ( !AGXUnity.IO.Environment.IsSet( AGXUnity.IO.Environment.Variable.AGX_DIR ) )
          AGXUnity.IO.Environment.Set( AGXUnity.IO.Environment.Variable.AGX_DIR,
                                       IO.Utils.AGXUnityPluginDirectoryFull );

        if ( !AGXUnity.IO.Environment.IsSet( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH ) )
          AGXUnity.IO.Environment.Set( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH,
                                       IO.Utils.AGXUnityPluginDirectoryFull + Path.DirectorySeparatorChar + "agx" );

        var envInstance = agxIO.Environment.instance();
        for ( int i = 0; i < (int)agxIO.Environment.Type.NUM_TYPES; ++i )
          envInstance.getFilePath( (agxIO.Environment.Type)i ).clear();

        // Adding Plugins/x86_64/agx to RESOURCE_PATH (for additional data) and
        // to RUNTIME_PATH (for entities and components). The license file is
        // searched for by the license manager.
        var dataAndRuntimePath = AGXUnity.IO.Environment.Get( AGXUnity.IO.Environment.Variable.AGX_PLUGIN_PATH );
        envInstance.getFilePath( agxIO.Environment.Type.RESOURCE_PATH ).pushbackPath( dataAndRuntimePath );
        envInstance.getFilePath( agxIO.Environment.Type.RUNTIME_PATH ).pushbackPath( dataAndRuntimePath );
      }
      // Check if user would like to initialize AGX Dynamics with an
      // installed (or Algoryx developer) version.
      else {
        if ( !HandleScriptReload( ExternalAGXInitializer.Initialize() ) )
          return EnvironmentState.Uninitialized;
      }

      // This validate is only for "license status" window so
      // the user will be noticed when something is wrong.
      try {
        AGXUnity.LicenseManager.LoadFile();

        AGXUnity.NativeHandler.Instance.ValidateLicense();
      }
      catch ( Exception ) {
        return EnvironmentState.Uninitialized;
      }

      HasPlayerNetCompatibilityIssueWarning();

      return EnvironmentState.Initialized;
#endif
      }

    private static bool HandleScriptReload( bool success )
    {
      var lastRequestData = GetRequestScriptReloadData();
      if ( success ) {
        if ( lastRequestData.Bool )
          Debug.Log( "AGX Dynamics successfully loaded.".Color( Color.green ) );
        lastRequestData.Bool = false;
      }
      else {
        if ( (float)EditorApplication.timeSinceStartup - lastRequestData.Float > 10.0f ) {
          lastRequestData.Float = (float)EditorApplication.timeSinceStartup;
          lastRequestData.Bool = true;
#if UNITY_2019_3_OR_NEWER
          Debug.LogWarning( "AGX Dynamics binaries aren't properly loaded into Unity - requesting Unity to reload assemblies..." );
          EditorUtility.RequestScriptReload();
#else
          Debug.LogWarning( "AGX Dynamics binaries aren't properly loaded into Unity - restart Unity manually." );
#endif
        }
      }

      return success;
    }

    private static EditorDataEntry GetRequestScriptReloadData()
    {
      return EditorData.Instance.GetStaticData( "Manager.RequestScriptReload", e => e.Float = -10.0f );
    }

    private static bool Equals( byte[] a, byte[] b )
    {
      if ( a.Length != b.Length )
        return false;
      for ( long i = 0; i < a.LongLength; ++i )
        if ( a[ i ] != b[ i ] )
          return false;
      return true;
    }

    public static bool HasBeenChanged( FileInfo v1, FileInfo v2 )
    {
      if ( v1 == null || !v1.Exists || v2 == null || !v2.Exists )
        return false;

      Func<FileInfo, byte[]> generateMd5 = fi =>
      {
        using ( var stream = fi.OpenRead() ) {
          return System.Security.Cryptography.MD5.Create().ComputeHash( stream );
        }
      };

      return !generateMd5( v1 ).SequenceEqual( generateMd5( v2 ) );
    }

    private static bool VerifyCompatibility()
    {
      // Ignore this if the editor is going into Play. We're not
      // coming here when the editor is stopped.
      if ( EditorApplication.isPlayingOrWillChangePlaymode )
        return true;

      var dotNetAssemblyNames = new string[]
      {
        "agxDotNet.dll",
        "agxMathDotNet.dll"
      };

      var result = true;
      foreach ( var dotNetAssemblyName in dotNetAssemblyNames )
        result = VerifyDotNetAssemblyCompatibility( dotNetAssemblyName ) &&
                 result;

#if UNITY_2019_4_OR_NEWER
      if ( !result ) {
        var defineSymbol = "AGX_DYNAMICS_UPDATE_REBUILD";
        if ( Build.DefineSymbols.Contains( defineSymbol ) )
          Build.DefineSymbols.Remove( defineSymbol );
        else
          Build.DefineSymbols.Add( defineSymbol );
      }
#endif

      return result;
    }

    private static bool VerifyDotNetAssemblyCompatibility( string dotNetAssemblyName )
    {
      string localDllFilename = IO.Utils.AGXUnityPluginDirectoryFull + $"/{dotNetAssemblyName}";
      var currDll = new FileInfo( localDllFilename );
      var installedDll = AGXUnity.IO.Environment.FindFile( dotNetAssemblyName );

      // Wasn't able to find any installed version of the assembly - it's up to Unity to handle this...
      if ( installedDll == null || !installedDll.Exists )
        return true;

      // Initializes AGX Dynamics. We're trying to be first to do this, preventing:
      //   - Recursive Serialization is not supported. You can't dereference a PPtr while loading.
      // when e.g., starting Unity with an empty scene and click on a (agx) restored prefab
      // in project tab, loading RestoredAGXFile. No clue why this error appears.
      AGXUnity.NativeHandler.Instance.Register( null );

      if ( !currDll.Exists || HasBeenChanged( currDll, installedDll ) ) {
        Debug.Log( $"<color=green>New version of {dotNetAssemblyName} located in: " + installedDll.Directory + ". Copying it to current project.</color>" );
        installedDll.CopyTo( localDllFilename, true );
        return false;
      }

      return true;
    }

    private static T GetOrCreateAsset<T>( string assetPath, Func<T> createFunc = null )
      where T : UnityEngine.Object
    {
      var obj = AssetDatabase.LoadAssetAtPath<T>( assetPath );
      if ( obj == null ) {
        if ( createFunc != null )
          obj = createFunc();
        else if ( typeof( AGXUnity.ScriptAsset ).IsAssignableFrom( typeof( T ) ) )
          obj = AGXUnity.ScriptAsset.Create( typeof( T ) ) as T;
        else if ( typeof( ScriptableObject ).IsAssignableFrom( typeof( T ) ) )
          obj = ScriptableObject.CreateInstance( typeof( T ) ) as T;

        if ( obj == null )
          throw new Exception( "Unable to create asset at path: " + assetPath );

        AssetDatabase.CreateAsset( obj, assetPath );
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      return obj;
    }

    private static void CreateDefaultAssets()
    {
      // Generate/synchronize custom editors.
      if ( !Directory.Exists( Utils.CustomEditorGenerator.Path ) )
        Directory.CreateDirectory( Utils.CustomEditorGenerator.Path );
      Utils.CustomEditorGenerator.Synchronize();

      // Shape visual material.
      GetOrCreateShapeVisualDefaultMaterial();

      // Merge split thresholds.
      if ( !AssetDatabase.IsValidFolder( IO.Utils.AGXUnityResourceDirectory + '/' + AGXUnity.MergeSplitThresholds.ResourceDirectory ) )
        AssetDatabase.CreateFolder( IO.Utils.AGXUnityResourceDirectory, AGXUnity.MergeSplitThresholds.ResourceDirectory );
      GetOrCreateAsset<AGXUnity.GeometryContactMergeSplitThresholds>( IO.Utils.AGXUnityResourceDirectory + '/' + AGXUnity.GeometryContactMergeSplitThresholds.ResourcePath + ".asset" );
      GetOrCreateAsset<AGXUnity.ConstraintMergeSplitThresholds>( IO.Utils.AGXUnityResourceDirectory + '/' + AGXUnity.ConstraintMergeSplitThresholds.ResourcePath + ".asset" );
    }
  }
}
