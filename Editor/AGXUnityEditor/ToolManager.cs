using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnityEditor.Tools;

using Tool = AGXUnityEditor.Tools.Tool;
using Object = UnityEngine.Object;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  public static class ToolManager
  {
    private static List<CustomTargetTool> m_activeTools = new List<CustomTargetTool>();
    private static BuiltInToolsTool m_builtInTools = new BuiltInToolsTool();
    private static Dictionary<Type, Type> m_cachedCustomToolTypeMap = new Dictionary<Type, Type>();
    private static HashSet<Type> m_cachedIgnoredTypes = new HashSet<Type>();
    private static Dictionary<Object, Editor> m_recursiveEditors = new Dictionary<Object, Editor>();

    /// <summary>
    /// All current, active tools (parents).
    /// </summary>
    public static CustomTargetTool[] ActiveTools { get { return m_activeTools.ToArray(); } }

    /// <summary>
    /// True if any active tool (including children) is hiding the
    /// default tools (translate, rotate, scale) - otherwise false.
    /// </summary>
    public static bool IsHidingDefaultTools
    {
      get
      {
        foreach ( var tool in m_activeTools )
          if ( tool.IsHidingDefaultTools )
            return true;
        return false;
      }
    }

    /// <summary>
    /// Find (depth-first) active tool given predicate.
    /// </summary>
    /// <typeparam name="T">Tool type.</typeparam>
    /// <param name="predicate">Predicate given tool of type T.</param>
    /// <returns>Active tool (parent or child) of type T that fulfills given predicate.</returns>
    public static T FindActive<T>( Predicate<T> predicate )
      where T : Tool
    {
      foreach ( var tool in m_activeTools ) {
        var result = tool.FindActive( predicate );
        if ( result != null )
          return result;
      }

      return null;
    }

    /// <summary>
    /// Find active tool given targets.
    /// </summary>
    /// <param name="target">Target objects.</param>
    /// <returns>Active tool on targets - otherwise null.</returns>
    public static CustomTargetTool FindActive( Object[] targets )
    {
      return m_activeTools.FirstOrDefault( tool => tool.Targets.SequenceEqual( targets ) );
    }

    /// <summary>
    /// Traverse (depth-first) active tools and their children.
    /// </summary>
    /// <param name="visitor">Visitor.</param>
    public static void Traverse<T>( Action<T> visitor )
      where T : Tool
    {
      foreach ( var tool in m_activeTools )
        Traverse( tool, visitor );
    }

    /// <summary>
    /// Callback from Tool.AddChild to try to handle other
    /// active tools - trying to disable them.
    /// </summary>
    /// <param name="child">Child that has just been added.</param>
    public static void OnChildAdded( Tool child )
    {
      if ( child == null )
        return;

      var singleInstanceTools = new List<Tool>();
      Func<Tool, bool> visitor = tool =>
      {
        // We don't want to remove children to our tool that's being added.
        // I.e., return true to not visit 'child' children.
        if ( tool == child )
          return true;

        if ( tool.HasChild( child ) )
          return false;

        // Route node tool is managing its children.
        if ( tool.HasParent<RouteNodeTool>() )
          return false;

        if ( !tool.IsSingleInstanceTool )
          return false;

        if ( tool is CustomTargetTool )
          return false;

        singleInstanceTools.Add( tool );

        return true;
      };
      foreach ( var activeTool in ActiveTools )
        Traverse( activeTool, visitor );

      foreach ( var toolToRemove in singleInstanceTools )
        toolToRemove.PerformRemoveFromParent();
    }

    /// <summary>
    /// Callback from Manager at OnSceneView event.
    /// </summary>
    /// <param name="sceneView">Scene view.</param>
    public static void HandleOnSceneViewGUI( SceneView sceneView )
    {
      HandleOnSceneView( m_builtInTools, sceneView );

      var activeTools = ActiveTools;
      foreach ( var tool in activeTools )
        HandleOnSceneView( tool, sceneView );
    }

    /// <summary>
    /// Callback from Editor OnEnable. Checks for classes with
    /// CustomToolAttribute matching <paramref name="targets"/> type.
    /// </summary>
    /// <param name="targets">Target objects.</param>
    /// <param name="editor">Editor for the targets.</param>
    public static void OnTargetEditorEnable( Object[] targets, Editor editor )
    {
      if ( targets.Length == 0 )
        return;

      // The target type has our custom editor, register the
      // assembly of the editor for possible custom target tools.
      if ( editor != null )
        RegisterCustomToolsAssembly( editor.GetType().Assembly.GetName().Name );

      Utils.KeyHandler.HandleDetectKeyOnEnable( targets );

      var toolType = FindCustomToolType( targets[ 0 ].GetType() );
      if ( toolType == null )
        return;

      CustomTargetTool tool = null;
      try {
        tool = (CustomTargetTool)Activator.CreateInstance( toolType, new object[] { targets } );
      }
      catch ( Exception e ) {
        Debug.LogException( e );
        return;
      }

      m_activeTools.Add( tool );

      tool.OnAdd();
    }

    public static void OnPreTargetMembers( Object[] targets )
    {
      var tool = FindActive( targets );
      if ( tool == null )
        return;

      tool.OnPreTargetMembersGUI();
    }

    public static void OnPostTargetMembers( Object[] targets )
    {
      var tool = FindActive( targets );
      if ( tool == null )
        return;

      tool.OnPostTargetMembersGUI();
    }

    /// <summary>
    /// Callback from Editor OnDisable. If <paramref name="targets"/>
    /// has an active custom target tool - the tool will be removed.
    /// </summary>
    /// <param name="targets">Target objects.</param>
    public static void OnTargetEditorDisable( Object[] targets )
    {
      if ( targets.Length == 0 )
        return;

      // Target component has been destroyed.
      if ( targets.Any( target => target == null ) ) {
        List<CustomTargetTool> toolsToRemove = new List<CustomTargetTool>();
        foreach ( var activeTool in m_activeTools )
          if ( activeTool.HasInvalidTargets )
            toolsToRemove.Add( activeTool );
        foreach ( var toolToRemove in toolsToRemove ) {
          toolToRemove.Remove();
          m_activeTools.Remove( toolToRemove );
        }

        return;
      }

      Utils.KeyHandler.HandleDetectKeyOnDisable( targets );

      var tool = FindActive( targets );
      if ( tool == null )
        return;

      tool.Remove();

      m_activeTools.Remove( tool );
    }

    /// <summary>
    /// Handle caching of recursive editors and returns a previously
    /// created editor of given target if it exists. This prevents
    /// escalating creations of CustomTargetTool instances (and more).
    /// </summary>
    /// <param name="target">Target object.</param>
    /// <returns>Cached editor or newly created one if it exists. Otherwise null.</returns>
    public static Editor TryGetOrCreateRecursiveEditor( Object target )
    {
      if ( target == null )
        return null;

      Editor editor = null;
      if ( m_recursiveEditors.TryGetValue( target, out editor ) ) {
        // Old editor with destroyed target, e.g., when entering
        // edit coming from play mode.
        if ( editor.target == null )
          ReleaseRecursiveEditor( target );
        else
          return editor;
      }

      editor = InspectorEditor.CreateRecursive( target );
      if ( editor != null )
        m_recursiveEditors.Add( target, editor );
      return editor;
    }

    /// <summary>
    /// Removes created recursive editor if it exists for the given target.
    /// </summary>
    /// <param name="target">Target object.</param>
    public static void ReleaseRecursiveEditor( Object target )
    {
      Editor editor = null;
      if ( target != null && m_recursiveEditors.TryGetValue( target, out editor ) ) {
        m_recursiveEditors.Remove( target );
        Object.DestroyImmediate( editor );
      }
    }

    /// <summary>
    /// Releases all recursive editors created, e.g., when Selected.objects
    /// is empty.
    /// </summary>
    public static void ReleaseAllRecursiveEditors()
    {
      foreach ( var editor in m_recursiveEditors.Values )
        Object.DestroyImmediate( editor );
      m_recursiveEditors.Clear();
    }

    /// <summary>
    /// Register additional assembly where CustomTargetTool is used.
    /// Manager.AGXUnityEditorAssemblyName is added by default.
    /// </summary>
    /// <param name="assemblyName">Name of the assembly.</param>
    public static void RegisterCustomToolsAssembly( string assemblyName )
    {
      if ( !m_assembliesWithCustomTools.Contains( assemblyName ) )
        m_assembliesWithCustomTools.Add( assemblyName );
    }

    /// <summary>
    /// Recursive depth-first visit of tool and its children.
    /// </summary>
    /// <param name="tool">Current parent tool.</param>
    /// <param name="visitor">Visitor.</param>
    private static void Traverse<T>( Tool tool, Action<T> visitor )
      where T : Tool
    {
      if ( tool == null || visitor == null )
        return;

      if ( tool is T )
        visitor( tool as T );

      foreach ( var child in tool.GetChildren() )
        Traverse( child, visitor );
    }

    /// <summary>
    /// Depth-first visit with optional break.
    /// </summary>
    /// <param name="tool">Tool to traverse.</param>
    /// <param name="visitor">Visitor function - return true to break, false to continue.</param>
    private static void Traverse( Tool tool, Func<Tool, bool> visitor )
    {
      if ( tool == null || visitor == null )
        return;

      if ( visitor( tool ) )
        return;

      foreach ( var child in tool.GetChildren() )
        Traverse( child, visitor );
    }

    /// <summary>
    /// Depth-first recursive Tool.OnSceneViewGUI calls, including
    /// update of a tools key handlers with current event.
    /// </summary>
    /// <param name="tool">Current parent tool.</param>
    /// <param name="sceneView">Scene view.</param>
    private static void HandleOnSceneView( Tool tool, SceneView sceneView )
    {
      if ( tool == null )
        return;

      // Previously we had:
      //   1. HandleOnSceneView for all children.
      //   2. Update all my key handlers.
      //   3. HandleOnSceneView for 'tool'.

      // Update 'tool' key handlers, so they're up to date when OnSceneView is called.
      foreach ( var keyHandler in tool.KeyHandlers )
        keyHandler.Update( Event.current );

      Tool.ClearTemporaries( tool );

      tool.OnSceneViewGUI( sceneView );

      // Depth first traverse of children.
      foreach ( var child in tool.GetChildren() )
        HandleOnSceneView( child, sceneView );
    }

    /// <summary>
    /// Finds custom tool type with CustomToolAttribute matching <paramref name="targetType"/>.
    /// </summary>
    /// <param name="targetType">Current target type.</param>
    /// <returns>Type of tool matching <paramref name="targetType"/>.</returns>
    private static Type FindCustomToolType( Type targetType )
    {
      if ( targetType == null || m_cachedIgnoredTypes.Contains( targetType ) )
        return null;

      Type customToolType = null;
      if ( !m_cachedCustomToolTypeMap.TryGetValue( targetType, out customToolType ) ) {
        Type[] types = null;
        try {
          types = m_assembliesWithCustomTools.SelectMany( name => Assembly.Load( name ).GetTypes() ).ToArray();
        }
        catch ( Exception ) {
        }

        if ( types == null )
          types = Assembly.Load( Manager.AGXUnityEditorAssemblyName ).GetTypes();

        var customToolTypes = new List<Type>();
        foreach ( var type in types ) {
          // CustomTool attribute can only be used with tools
          // inheriting from CustomTargetTool.
          if ( !typeof( CustomTargetTool ).IsAssignableFrom( type ) )
            continue;

          var customToolAttribute = type.GetCustomAttribute<CustomToolAttribute>( false );
          if ( customToolAttribute == null )
            continue;

          // Exact match - break search.
          if ( customToolAttribute.Type == targetType ) {
            customToolTypes.Clear();
            customToolTypes.Add( type );
            break;
          }
          // Type of custom tool desired type is assignable from current
          // target type. Store this if an exact match comes later.
          // E.g.: CustomTool( typeof( Shape ) ) and CustomTool( typeof( Box ) ).
          else if ( customToolAttribute.Type.IsAssignableFrom( targetType ) )
            customToolTypes.Add( type );
        }

        customToolType = customToolTypes.FirstOrDefault();
        if ( customToolType != null )
          m_cachedCustomToolTypeMap.Add( targetType, customToolType );
      }

      if ( customToolType == null )
        m_cachedIgnoredTypes.Add( targetType );

      return customToolType;
    }

    private static List<string> m_assembliesWithCustomTools = new List<string>() { Manager.AGXUnityEditorAssemblyName };
  }
}
