using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnityEditor.Tools;

using Tool = AGXUnityEditor.Tools.Tool;

namespace AGXUnityEditor
{
  [InitializeOnLoad]
  public static class ToolManager
  {
    private static List<CustomTargetTool> m_activeTools = new List<CustomTargetTool>();
    private static BuiltInToolsTool m_builtInTools = new BuiltInToolsTool();
    private static Dictionary<Type, Type> m_cachedCustomToolTypeMap = new Dictionary<Type, Type>();

    /// <summary>
    /// All current, active tools (parents).
    /// </summary>
    public static Tool[] ActiveTools { get { return m_activeTools.ToArray(); } }

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
    /// Traverse (depth-first) active tools and their children.
    /// </summary>
    /// <param name="visitor">Visitor.</param>
    public static void Traverse( Action<Tool> visitor )
    {
      foreach ( var tool in m_activeTools )
        Traverse( tool, visitor );
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

    public static void OnTargetEditorEnable<T>( T target )
      where T : UnityEngine.Object
    {
      if ( target == null )
        return;

      Utils.KeyHandler.HandleDetectKeyOnEnable( target );

      var toolType = FindCustomToolType( typeof( T ) );
      if ( toolType == null )
        return;

      foreach ( var tool in m_activeTools )
        if ( tool.GetType() == toolType && tool.Target == target )
          return;

      // Create, Add and invoke OnAdd.
    }

    public static bool OnTargetEditorInspectorGUI<T>( T target )
      where T : UnityEngine.Object
    {
      if ( target == null )
        return false;

      return Utils.KeyHandler.HandleDetectKeyOnGUI( target, Event.current );
    }

    public static void OnTargetEditorDisable<T>( T target )
      where T : UnityEngine.Object
    {
      if ( target == null )
        return;

      Utils.KeyHandler.HandleDetectKeyOnDisable( target );
    }

    /// <summary>
    /// Recursive depth-first visit of tool and its children.
    /// </summary>
    /// <param name="tool">Current parent tool.</param>
    /// <param name="visitor">Visitor.</param>
    private static void Traverse( Tool tool, Action<Tool> visitor )
    {
      if ( tool == null || visitor == null )
        return;

      visitor( tool );

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
      if ( targetType == null )
        return null;

      Type customToolType = null;
      if ( !m_cachedCustomToolTypeMap.TryGetValue( targetType, out customToolType ) ) {
        var types = Assembly.Load( Manager.AGXUnityEditorAssemblyName ).GetTypes();
        var customToolTypes = new List<Type>();
        foreach ( var type in types ) {
          // CustomTool attribute can only be used with tools
          // inheriting from CustomTargetTool.
          if ( !typeof( CustomTargetTool ).IsAssignableFrom( type ) )
            continue;

          var customToolAttribute = type.GetCustomAttribute<CustomTool>( false );
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

      return customToolType;
    }
  }
}
