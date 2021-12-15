using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  public class Tool
  {
    /// <summary>
    /// Color of the x axis.
    /// </summary>
    /// <param name="alpha">Alpha value.</param>
    /// <returns>Color of the x axis.</returns>
    public static Color GetXAxisColor( float alpha = 1.0f )
    {
      return new Color( 1.0f, 0, 0, alpha );
    }

    /// <summary>
    /// Color of the y axis.
    /// </summary>
    /// <param name="alpha">Alpha value.</param>
    /// <returns>Color of the y axis.</returns>
    public static Color GetYAxisColor( float alpha = 1.0f )
    {
      return new Color( 0, 1.0f, 0, alpha );
    }

    /// <summary>
    /// Color of the z axis.
    /// </summary>
    /// <param name="alpha">Alpha value.</param>
    /// <returns>Color of the z axis.</returns>
    public static Color GetZAxisColor( float alpha = 1.0f )
    {
      return new Color( 0, 0, 1.0f, alpha );
    }

    /// <summary>
    /// Color of the center.
    /// </summary>
    /// <param name="alpha">Alpha value.</param>
    /// <returns>Color of the center.</returns>
    public static Color GetCenterColor( float alpha = 1.0f )
    {
      return new Color( 0.7f, 0.7f, 0.7f, alpha );
    }

    /// <summary>
    /// Creates a position tool given position and rotation.
    /// </summary>
    /// <param name="position">Current position.</param>
    /// <param name="rotation">Current rotation.</param>
    /// <param name="scale">Scale - default 1.0f.</param>
    /// <param name="alpha">Alpha - default 1.0f.</param>
    /// <returns>New position of the tool.</returns>
    public static Vector3 PositionTool( Vector3 position, Quaternion rotation, float scale = 1.0f, float alpha = 1.0f )
    {
      Vector3 snapSetting = new Vector3( 0.5f, 0.5f, 0.5f );

      Color orgColor = Handles.color;

      float handleSize = HandleUtility.GetHandleSize( position );
      Color color      = Handles.color;
      Handles.color    = GetXAxisColor( alpha );
      position         = Handles.Slider( position, rotation * Vector3.right, scale * handleSize, Handles.ArrowHandleCap, snapSetting.x );
      Handles.color    = GetYAxisColor( alpha );
      position         = Handles.Slider( position, rotation * Vector3.up, scale * handleSize, Handles.ArrowHandleCap, snapSetting.y );
      Handles.color    = GetZAxisColor( alpha );
      position         = Handles.Slider( position, rotation * Vector3.forward, scale * handleSize, Handles.ArrowHandleCap, snapSetting.z );

      float slider2DSize = scale * handleSize * 0.15f;
      Func<Vector3, Vector3, Vector3, Vector3> PlaneHandle = ( normal, dir1, dir2 ) =>
      {
        Vector3 offset = slider2DSize * ( rotation * dir1 + rotation * dir2 );
        Vector3 result = Handles.Slider2D( position + offset, rotation * normal, rotation * dir1, rotation * dir2, slider2DSize, Handles.RectangleHandleCap, snapSetting.x );
        result -= offset;
        return result;
      };

      Handles.color = GetXAxisColor( 0.3f );
      position      = PlaneHandle( Vector3.right,   Vector3.up,    Vector3.forward );
      Handles.color = GetYAxisColor( 0.3f );        
      position      = PlaneHandle( Vector3.up,      Vector3.right, Vector3.forward );
      Handles.color = GetZAxisColor( 0.3f );        
      position      = PlaneHandle( Vector3.forward, Vector3.right, Vector3.up );

      Handles.color = orgColor;

      return position;
    }

    /// <summary>
    /// Creates a rotation tool given position and rotation.
    /// </summary>
    /// <param name="position">Current position.</param>
    /// <param name="rotation">Current rotation.</param>
    /// <param name="scale">Scale - default 1.0f.</param>
    /// <param name="alpha">Alpha - default 1.0f.</param>
    /// <returns>New rotation of the tool.</returns>
    public static Quaternion RotationTool( Vector3 position, Quaternion rotation, float scale = 1.0f, float alpha = 1.0f )
    {
      float snapSetting = 0.5f;

      Color orgColor = Handles.color;

      float handleSize = HandleUtility.GetHandleSize( position );
      Color color = Handles.color;
      Handles.color = GetXAxisColor( alpha );
      rotation = Handles.Disc( rotation, position, rotation * Vector3.right, scale * handleSize, true, snapSetting );
      Handles.color = GetYAxisColor( alpha );
      rotation = Handles.Disc( rotation, position, rotation * Vector3.up, scale * handleSize, true, snapSetting );
      Handles.color = GetZAxisColor( alpha );
      rotation = Handles.Disc( rotation, position, rotation * Vector3.forward, scale * handleSize, true, snapSetting );
      Handles.color = GetCenterColor( 0.6f * alpha );
      rotation = Handles.Disc( rotation, position, Camera.current.transform.forward, scale * handleSize * 1.1f, false, 0f );
      rotation = Handles.FreeRotateHandle( rotation, position, scale * handleSize );

      Handles.color = orgColor;

      return rotation;
    }

    /// <summary>
    /// Creates single direction slider tool.
    /// </summary>
    /// <param name="position">Position of the slider.</param>
    /// <param name="direction">Direction of the slider.</param>
    /// <param name="color">Color of the slider.</param>
    /// <param name="scale">Scale - default 1.0.</param>
    /// <returns>New position of the slider.</returns>
    public static Vector3 SliderTool( Vector3 position, Vector3 direction, Color color, float scale = 1.0f )
    {
      float snapSetting = 0.001f;

      Color prevColor = Handles.color;
      Handles.color = color;
      float handleSize = HandleUtility.GetHandleSize( position );
      position = Handles.Slider( position, direction, scale * handleSize, Handles.ArrowHandleCap, snapSetting );
      Handles.color = prevColor;

      return position;
    }

    /// <summary>
    /// Creates a slider tool but instead of the new position this function returns the movement.
    /// </summary>
    /// <param name="position">Position of the slider.</param>
    /// <param name="direction">Direction of the slider.</param>
    /// <param name="color">Color of the slider.</param>
    /// <param name="scale">Scale - default 1.0.</param>
    /// <returns>How much the slider has been moved.</returns>
    public static Vector3 DeltaSliderTool( Vector3 position, Vector3 direction, Color color, float scale = 1.0f )
    {
      Vector3 newPosition = SliderTool( position, direction, color, scale );
      return newPosition - position;
    }

    /// <summary>
    /// Searches active tool from top level, depth first, given predicate.
    /// </summary>
    /// <typeparam name="T">Type of the tool.</typeparam>
    /// <param name="tool">Parent tool to start from.</param>
    /// <param name="pred">Tool predicate.</param>
    /// <returns>Tool given type and predicate if active - otherwise null.</returns>
    public T FindActive<T>( Predicate<T> pred ) where T : Tool
    {
      if ( this is T && pred( this as T ) )
        return this as T;

      foreach ( var child in m_children ) {
        var result = child.FindActive( pred );
        if ( result != null )
          return result;
      }

      return null;
    }

    /// <summary>
    /// True if this tool only supports living on its own, meaning
    /// no other single instance tools can be enabled with this tool.
    /// 
    /// False if this tool supports having multiple instances or other
    /// types of tools active at once, e.g., CustomTargetTool.
    /// </summary>
    public bool IsSingleInstanceTool { get; set; } = true;

    /// <summary>
    /// Debug render cylinder or arrow.
    /// </summary>
    /// <remarks>
    /// This method is only valid to use during OnSceneViewGUI.
    /// </remarks>
    /// <param name="start">Cylinder/arrow start position.</param>
    /// <param name="end">Cylinder/arrow end position.</param>
    /// <param name="radius">Size of the cylinder/arrow.</param>
    /// <param name="color">Color of the cylinder/arrow.</param>
    /// <param name="arrow">True to render as arrow, false to render as cylinder.</param>
    public void DebugRender( Vector3 start, Vector3 end, float radius, Color color, bool arrow = false )
    {
      // Getting strange errors if we do this when the editor is
      // going in or out of play mode.
      if ( EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying )
        return;

      CreateDefaultDebugRenderable<Utils.VisualPrimitiveArrow>( color ).SetTransformEx( start,
                                                                                        end,
                                                                                        radius,
                                                                                        arrow );
    }

    /// <summary>
    /// Debug render sphere.
    /// </summary>
    /// <remarks>
    /// This method is only valid to use during OnSceneViewGUI.
    /// </remarks>
    /// <param name="center">Center of sphere.</param>
    /// <param name="radius">Radius of sphere.</param>
    /// <param name="color">Color of sphere.</param>
    public void DebugRender( Vector3 center, float radius, Color color )
    {
      if ( EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying )
        return;

      CreateDefaultDebugRenderable<Utils.VisualPrimitiveSphere>( color ).SetTransform( center,
                                                                                       Quaternion.identity,
                                                                                       radius,
                                                                                       false );
    }

    private T CreateDefaultDebugRenderable<T>( Color color )
      where T : Utils.VisualPrimitive
    {
      var primitive = GetOrCreateVisualPrimitive<T>( s_debugRenderVisualPrefix + m_visualPrimitives.Count.ToString(),
                                                     "GUI/Text Shader" );
      primitive.Color = primitive.MouseOverColor = color;
      primitive.Pickable = false;
      primitive.Visible = true;
      return primitive;
    }

    /// <summary>
    /// Clears temporary data. This method is called before OnSceneViewGUI.
    /// </summary>
    /// <param name="tool">Tool to clear temporary data for.</param>
    public static void ClearTemporaries( Tool tool )
    {
      List<string> debugRenderablesToRemove = new List<string>();
      foreach ( var name in tool.m_visualPrimitives.Keys ) {
        if ( name.StartsWith( s_debugRenderVisualPrefix ) )
          debugRenderablesToRemove.Add( name );
      }
      foreach ( var name in debugRenderablesToRemove )
        tool.RemoveVisualPrimitive( name );
    }

    /// <summary>
    /// Construct given the implemented tool supports multiple
    /// instances of the same tool enabled at the same time. E.g.,
    /// a tool that catches key and/or mouse events in the scene view
    /// is single instance and tools with multiple child tools and
    /// Inspector GUI is not single instance (CustomTargetTool etc.).
    /// 
    /// Tools that are single instance will be removed by the ToolManager
    /// when another single instance tool is enabled.
    /// </summary>
    /// <param name="isSingleInstanceTool"></param>
    protected Tool( bool isSingleInstanceTool )
    {
      IsSingleInstanceTool = isSingleInstanceTool;
    }

    public virtual void OnSceneViewGUI( SceneView sceneView ) { }

    public virtual void OnPreTargetMembersGUI() { }

    public virtual void OnPostTargetMembersGUI() { }

    public virtual void OnAdd() { }

    public virtual void OnRemove() { }

    public Tool GetParent()
    {
      return m_parent;
    }

    public Tool GetRoot()
    {
      var root = GetParent();
      while ( root != null && root.GetParent() != null )
        root = root.GetParent();
      return root;
    }

    public bool HasParent( Tool parent )
    {
      var currParent = GetParent();
      while ( currParent != null && currParent != parent )
        currParent = currParent.GetParent();
      return currParent != null;
    }

    public bool HasParent<T>()
      where T : Tool
    {
      var currParent = GetParent();
      while ( currParent != null && !( currParent is T ) )
        currParent = currParent.GetParent();
      return currParent != null;
    }

    public bool HasChild( Tool tool )
    {
      if ( this == tool )
        return true;

      foreach ( var child in m_children ) {
        if ( child.HasChild( tool ) )
          return true;
      }

      return false;
    }

    public T GetChild<T>() where T : Tool
    {
      for ( int i = 0; i < m_children.Count; ++i )
        if ( m_children[ i ] is T )
          return m_children[ i ] as T;
      return null;
    }

    public T[] GetChildren<T>() where T : Tool
    {
      return ( from child in m_children where child.GetType() == typeof( T ) select child as T ).ToArray();
    }

    public Tool[] GetChildren()
    {
      return m_children.ToArray();
    }

    public Utils.KeyHandler[] KeyHandlers { get { return m_keyHandlers.Values.ToArray(); } }

    public void PerformRemoveFromParent()
    {
      PerformRemove();
    }

    public void Remove()
    {
      PerformRemoveFromParent();
    }

    public Editor GetOrCreateEditor( Object target )
    {
      // We get null reference exception when we destroy a
      // GameObjectInspector and OnInspectorGUI doesn't show
      // anything, it probably requires more, but still it's
      // not desired to recurse the whole GameObject.
      if ( target is GameObject )
        return null;

      Editor editor = null;
      if ( m_editors.TryGetValue( target, out editor ) )
        return editor;
      editor = InspectorEditor.CreateRecursive( target );
      m_editors.Add( target, editor );
      return editor;
    }

    public bool HasEditor( Object target )
    {
      return m_editors.ContainsKey( target );
    }

    public void RemoveEditor( Object target )
    {
      Editor editor = null;
      if ( !m_editors.TryGetValue( target, out editor ) )
        return;

      m_editors.Remove( target );
      Object.DestroyImmediate( editor );
    }

    public void RemoveEditors( Object[] targets )
    {
      foreach ( var target in targets )
        RemoveEditor( target );
    }

    protected void AddChild( Tool child )
    {
      if ( child == null || m_children.Contains( child ) )
        return;

      m_children.Add( child );
      child.m_parent = this;
      child.OnAdd();

      ToolManager.OnChildAdded( child );
    }

    protected void RemoveChild( Tool child )
    {
      if ( child == null || !m_children.Contains( child ) )
        return;

      child.PerformRemoveFromParent();
    }

    protected void RemoveAllChildren()
    {
      while ( m_children.Count > 0 )
        m_children[ m_children.Count - 1 ].PerformRemoveFromParent();
    }

    protected T GetOrCreateVisualPrimitive<T>( string name, string shader = "Unlit/Color" ) where T : Utils.VisualPrimitive
    {
      T primitive = GetVisualPrimitive<T>( name );
      if ( primitive != null )
        return primitive;

      primitive = (T)Activator.CreateInstance( typeof( T ), new object[] { shader } );
      m_visualPrimitives.Add( name, primitive );

      return primitive;
    }

    protected T GetVisualPrimitive<T>( string name ) where T : Utils.VisualPrimitive
    {
      // C-cast style cast to throw if the type isn't matching.
      return (T)GetVisualPrimitive( name );
    }

    protected Utils.VisualPrimitive GetVisualPrimitive( string name )
    {
      Utils.VisualPrimitive primitive = null;
      if ( m_visualPrimitives.TryGetValue( name, out primitive ) )
        return primitive;
      return null;
    }

    protected void RemoveVisualPrimitive( string name )
    {
      Utils.VisualPrimitive primitive = null;
      if ( m_visualPrimitives.TryGetValue( name, out primitive ) ) {
        primitive.Destruct();
        m_visualPrimitives.Remove( name );
      }
    }

    protected void RemoveVisualPrimitive( Utils.VisualPrimitive primitive )
    {
      if ( m_visualPrimitives.ContainsValue( primitive ) )
        RemoveVisualPrimitive( m_visualPrimitives.First( kvp => kvp.Value == primitive ).Key );
    }

    protected GameObject HighlightObject
    {
      get { return m_highlightedObject; }
      set
      {
        if ( m_highlightedObject == value )
          return;

        SceneViewHighlight.Remove( m_highlightedObject );
        m_highlightedObject = value;
        SceneViewHighlight.Add( m_highlightedObject );
      }
    }

    private struct CallEveryData
    {
      public double LastTime;
      public int NumCalls;
    }

    protected void CallEvery( float time, Action<int> callback )
    {
      if ( m_callEveryData.LastTime == 0.0 ) {
        m_callEveryData.LastTime = EditorApplication.timeSinceStartup;
        return;
      }

      if ( ( EditorApplication.timeSinceStartup - m_callEveryData.LastTime ) >= time ) {
        callback( ++m_callEveryData.NumCalls );
        m_callEveryData.LastTime = EditorApplication.timeSinceStartup;
      }
    }

    protected string AwaitingUserActionDots()
    {
      CallEvery( 0.35f, numCalls => m_awaitingUserActionDots = new string( '.', numCalls % 4 ) );
      return m_awaitingUserActionDots;
    }

    protected void AddKeyHandler( string name, Utils.KeyHandler keyHandler )
    {
      if ( m_keyHandlers.ContainsKey( name ) ) {
        Debug.Log( "Trying to add KeyHandler with non-unique name: " + name + ". KeyHandler ignored." );
        return;
      }

      m_keyHandlers.Add( name, keyHandler );
    }

    protected void RemoveKeyHandler( string name )
    {
      RemoveKeyHandler( GetKeyHandler( name ) );
    }

    protected void RemoveKeyHandler( Utils.KeyHandler keyHandler )
    {
      if ( keyHandler == null || !m_keyHandlers.ContainsValue( keyHandler ) )
        return;

      keyHandler.OnRemove();

      m_keyHandlers.Remove( m_keyHandlers.First( kvp => kvp.Value == keyHandler ).Key );
    }

    protected Utils.KeyHandler GetKeyHandler( string name )
    {
      Utils.KeyHandler keyHandler = null;
      if ( m_keyHandlers.TryGetValue( name, out keyHandler ) )
        return keyHandler;
      return null;
    }

    /// <summary>
    /// Depth first update of children.
    /// </summary>
    /// <param name="sceneView">Current scene view.</param>
    private void HandleOnSceneView( SceneView sceneView )
    {
      foreach ( var keyHandler in m_keyHandlers.Values )
        keyHandler.Update( Event.current );

      OnSceneViewGUI( sceneView );

      foreach ( var child in m_children.ToList() )
        child.HandleOnSceneView( sceneView );
    }

    public class HideDefaultState
    {
      private bool m_toolWasHidden = false;

      public HideDefaultState()
      {
        m_toolWasHidden = UnityEditor.Tools.hidden;
        UnityEditor.Tools.hidden = true;
      }

      public void OnRemove()
      {
        UnityEditor.Tools.hidden = m_toolWasHidden;
      }
    }

    private HideDefaultState m_hideDefaultState = null;

    public bool IsHidingDefaultTools
    {
      get
      {
        if ( m_hideDefaultState != null )
          return true;

        for ( int i = 0; i < m_children.Count; ++i )
          if ( m_children[ i ].IsHidingDefaultTools )
            return true;

        return false;
      }
    }

    protected void HideDefaultHandlesEnableWhenRemoved()
    {
      m_hideDefaultState = new HideDefaultState();
    }

    private void PerformRemove()
    {
      // OnRemove virtual callback.
      OnRemove();

      // Remove all windows that hasn't been closed.
      Manager.SceneViewGUIWindowHandler.CloseAllWindows( this );

      // Clear any highlighted objects.
      HighlightObject = null;

      // Remove all key handlers that hasn't been removed.
      var keyHandlerNames = m_keyHandlers.Keys.ToArray();
      foreach ( var keyHandlerName in keyHandlerNames )
        RemoveKeyHandler( keyHandlerName );

      // Remove all visual primitives that hasn't been removed.
      var visualPrimitiveNames = m_visualPrimitives.Keys.ToArray();
      foreach ( var visualPrimitiveName in visualPrimitiveNames )
        RemoveVisualPrimitive( visualPrimitiveName );

      // Remove all editors that hasn't been removed.
      var editorTargets = m_editors.Keys.ToArray();
      foreach ( var editorTarget in editorTargets )
        RemoveEditor( editorTarget );

      // Remove us from our parent.
      if ( m_parent != null )
        m_parent.m_children.Remove( this );
      m_parent = null;

      // Remove children.
      var children = m_children.ToArray();
      foreach ( var child in children )
        child.PerformRemove();

      if ( m_hideDefaultState != null )
        m_hideDefaultState.OnRemove();
    }

    private List<Tool> m_children = new List<Tool>();
    private Tool m_parent = null;

    private Dictionary<string, Utils.VisualPrimitive> m_visualPrimitives = new Dictionary<string, Utils.VisualPrimitive>();
    private Dictionary<string, Utils.KeyHandler> m_keyHandlers = new Dictionary<string, Utils.KeyHandler>();
    private Dictionary<Object, Editor> m_editors = new Dictionary<Object, Editor>();

    private CallEveryData m_callEveryData;
    private string m_awaitingUserActionDots = "";

    private GameObject m_highlightedObject = null;

    private static readonly string s_debugRenderVisualPrefix = "dr_prim_";
  }
}
