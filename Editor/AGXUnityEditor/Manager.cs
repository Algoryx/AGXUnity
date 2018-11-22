﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

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
    /// Constructor called when the Unity editor is initialized.
    /// </summary>
    static Manager()
    {
      IO.Utils.VerifyDirectories();

      // If compatibility issues, this method will try to fix them and this manager
      // will probably be loaded again after the fix.
      if ( !VerifyCompatibility() )
        return;
      
      SceneView.onSceneGUIDelegate += OnSceneView;
#if UNITY_2018_1_OR_NEWER
      EditorApplication.hierarchyChanged += OnHierarchyWindowChanged;
#else
      EditorApplication.hierarchyWindowChanged += OnHierarchyWindowChanged;
#endif
      Selection.selectionChanged += OnSelectionChanged;

      while ( VisualsParent != null && VisualsParent.transform.childCount > 0 )
        GameObject.DestroyImmediate( VisualsParent.transform.GetChild( 0 ).gameObject );

      MouseOverObject = null;

      Undo.undoRedoPerformed += UndoRedoPerformedCallback;

      // Focus on scene view for events to be properly handled. E.g.,
      // holding one key and click in scene view is not working until
      // scene view is focused since some other event is taking the
      // key event.
      RequestSceneViewFocus();

      Tools.Tool.ActivateBuiltInTools();

      CreateDefaultAssets();

      PrefabUtility.prefabInstanceUpdated += OnPrefabUpdate;
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

    private static string m_currentSceneName = string.Empty;
    private static bool m_requestSceneViewFocus = false;
    private static HijackLeftMouseClickData m_hijackLeftMouseClickData = null;

    private static string m_visualParentName = "Manager".To32BitFnv1aHash().ToString();
    private static GameObject m_visualsParent = null;
    private static HashSet<Utils.VisualPrimitive> m_visualPrimitives = new HashSet<Utils.VisualPrimitive>();

    public static GameObject VisualsParent
    {
      get
      {
        if ( m_visualsParent == null ) {
          m_visualsParent = GameObject.Find( m_visualParentName ) ?? new GameObject( m_visualParentName );
          m_visualsParent.hideFlags = HideFlags.HideAndDontSave;
        }

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
      if ( Selection.activeGameObject == null )
        return;

      var scriptComponents = Selection.activeGameObject.GetComponents<AGXUnity.ScriptComponent>();
      foreach ( var scriptComponent in scriptComponents )
        EditorUtility.SetDirty( scriptComponent );

      if ( scriptComponents.Length > 0 )
        SceneView.RepaintAll();
    }

    private static void OnSceneView( SceneView sceneView )
    {
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

      Tools.Tool.HandleOnSceneViewGUI( sceneView );

      HandleWindowsGUI( sceneView );

      LeftMouseClick = false;

      if ( EditorData.Instance.SecondsSinceLastGC > 5.0 * 60 )
        EditorData.Instance.GC();
    }

    public static void UpdateMouseOverPrimitives( Event current, bool forced = false )
    {
      // Can't perform picking during repaint event.
      if ( current == null || !( current.isMouse || current.isKey || forced ) )
        return;

      // Update mouse over before we reveal the VisualPrimitives.
      // NOTE: We're putting our "visual primitives" in the ignore list.
      if ( current.isMouse || forced ) {
        List<GameObject> ignoreList = new List<GameObject>();
        foreach ( var primitive in m_visualPrimitives ) {
          if ( !primitive.Visible )
            continue;

          MeshFilter[] primitiveFilters = primitive.Node.GetComponentsInChildren<MeshFilter>();
          ignoreList.AddRange( primitiveFilters.Select( pf => { return pf.gameObject; } ) );
        }

        // If the mouse is hovering a scene view window - MouseOverObject should be null.
        if ( SceneViewWindow.GetMouseOverWindow( current.mousePosition ) != null )
          MouseOverObject = null;
        else
          MouseOverObject = RouteObject( HandleUtility.PickGameObject( current.mousePosition,
                                                                       false,
                                                                       ignoreList.ToArray() ) ) as GameObject;
      }

      // Early exit if we haven't any active visual primitives.
      if ( m_visualPrimitives.Count == 0 )
        return;

      var primitiveHitList = new[] { new { Primitive = (Utils.VisualPrimitive)null, RaycastResult = Raycast.Hit.Invalid } }.ToList();
      primitiveHitList.Clear();

      Ray mouseRay = HandleUtility.GUIPointToWorldRay( current.mousePosition );
      foreach ( var primitive in m_visualPrimitives ) {
        primitive.MouseOver = false;

        if ( !primitive.Pickable )
          continue;

        Raycast.Hit hit = Raycast.Test( primitive.Node, mouseRay, 500f, true );
        if ( hit.Triangle.Valid )
          primitiveHitList.Add( new { Primitive = primitive, RaycastResult = hit } );
      }

      if ( primitiveHitList.Count == 0 )
        return;

      var bestResult = primitiveHitList[ 0 ];
      for ( int i = 1; i < primitiveHitList.Count; ++i )
        if ( primitiveHitList[ i ].RaycastResult.Triangle.Distance < bestResult.RaycastResult.Triangle.Distance )
          bestResult = primitiveHitList[ i ];

      bestResult.Primitive.MouseOver = true;
      if ( HijackLeftMouseClick() )
        bestResult.Primitive.OnMouseClick( bestResult.RaycastResult, bestResult.Primitive );
    }

    private static void OnHierarchyWindowChanged()
    {
      var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
      if ( scene.name != m_currentSceneName ) {
        EditorData.Instance.GC();

        m_currentSceneName = scene.name;

        AutoUpdateSceneHandler.HandleUpdates( scene );
      }
      else if ( Selection.activeGameObject != null ) {
        if ( Selection.activeGameObject.GetComponent<AGXUnity.IO.RestoredAGXFile>() != null )
          AssetPostprocessorHandler.OnPrefabAddedToScene( Selection.activeGameObject );

        var savedPrefabData = Selection.activeGameObject.GetComponent<AGXUnity.IO.SavedPrefabLocalData>();
        if ( savedPrefabData != null && savedPrefabData.DisabledGroups.Length > 0 ) {
          Undo.SetCurrentGroupName( "Adding prefab data for " + Selection.activeGameObject.name + " to scene." );
          var grouId = Undo.GetCurrentGroup();
          foreach ( var disabledGroup in savedPrefabData.DisabledGroups )
            TopMenu.GetOrCreateUniqueGameObject<AGXUnity.CollisionGroupsManager>().SetEnablePair( disabledGroup.First, disabledGroup.Second, false );
          Undo.CollapseUndoOperations( grouId );
        }
      }
    }

    /// <summary>
    /// Previous selection used to reset used EditorDataEntry entries.
    /// </summary>
    private static UnityEngine.Object[] m_previousSelection = new UnityEngine.Object[] { };

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
      var activeTool = Tools.Tool.GetActiveTool();
      // If the active tool is hiding the position/rotation/scale handles
      // we ignore this 'auto-hiding'.
      if ( activeTool == null || !activeTool.IsHidingTools ) {
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
    }

    private static void HandleWindowsGUI( SceneView sceneView )
    {
      SceneViewWindow.OnSceneView( sceneView );
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
      string localDllFilename = IO.Utils.AGXUnityPluginDirectoryFull + "/agxDotNet.dll";
      FileInfo currDll        = new FileInfo( localDllFilename );
      FileInfo installedDll   = AGXUnity.IO.Utils.GetFileInEnvironmentPath( "agxDotNet.dll" );

      // Wasn't able to find any installed agxDotNet.dll - it's up to Unity to handle this...
      if ( installedDll == null || !installedDll.Exists )
        return true;

      // Initializes AGX Dynamics. We're trying to be first to do this, preventing:
      //   - Recursive Serialization is not supported. You can't dereference a PPtr while loading.
      // when e.g., starting Unity with an empty scene and click on a (agx) restored prefab
      // in project tab, loading RestoredAGXFile. No clue why this error appears.
      AGXUnity.NativeHandler.Instance.Register( null );

      if ( !currDll.Exists || HasBeenChanged( currDll, installedDll ) ) {
        Debug.Log( "<color=green>New version of agxDotNet.dll located in: " + installedDll.Directory + ". Copying it to current project.</color>" );
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

    private class CollisionGroupEntryEqualityComparer : IEqualityComparer<AGXUnity.CollisionGroupEntry>
    {
      public bool Equals( AGXUnity.CollisionGroupEntry cg1, AGXUnity.CollisionGroupEntry cg2 )
      {
        return cg1.Tag == cg2.Tag;
      }

      public int GetHashCode( AGXUnity.CollisionGroupEntry entry )
      {
        return entry.Tag.GetHashCode();
      }
    }

    /// <summary>
    /// Callback when a prefab is created from a gameobject <paramref name="go"/>.
    /// </summary>
    private static void OnPrefabUpdate( GameObject go )
    {
      // Collecting disabled collision groups for the created prefab.
      if ( AGXUnity.CollisionGroupsManager.HasInstance ) {
#if UNITY_2018_1_OR_NEWER
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource( go ) as GameObject;
#else
        var prefab = PrefabUtility.GetPrefabParent( go ) as GameObject;
#endif
        if ( prefab != null ) {
          var allGroups = prefab.GetComponentsInChildren<AGXUnity.CollisionGroups>();
          var tags = ( from objectGroups
                       in allGroups
                       from tag
                       in objectGroups.Groups
                       select tag ).Distinct( new CollisionGroupEntryEqualityComparer() ).ToList();
          var disabledPairs = new List<AGXUnity.IO.GroupPair>();
          foreach ( var t1 in tags )
            foreach ( var t2 in tags )
              if ( !AGXUnity.CollisionGroupsManager.Instance.GetEnablePair( t1.Tag, t2.Tag ) )
                disabledPairs.Add( new AGXUnity.IO.GroupPair() { First = t1.Tag, Second = t2.Tag } );

          if ( disabledPairs.Count > 0 ) {
            var prefabLocalData = prefab.GetOrCreateComponent<AGXUnity.IO.SavedPrefabLocalData>();
            foreach ( var groupPair in disabledPairs )
              prefabLocalData.AddDisabledPair( groupPair.First, groupPair.Second );
            EditorUtility.SetDirty( prefabLocalData );
          }
        }
      }
    }
  }
}
