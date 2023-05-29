using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Scene or game view window handled given a GUI callback method in any class.
  /// </summary>
  /// <example>
  /// public class MyEditorClass
  /// {
  ///   public void ShowWindow()
  ///   {
  ///     // Create window given RenderWindow method.
  ///     var data = GUIWindowHandler.Show( RenderWindow, new Vector2( 300, 70 ), new Vector2( 300, 300 ), "My window" );
  ///     // Assign/change data for the window, e.g., if the user may drag the window.
  ///     data.Movable = true;
  ///   }
  ///   
  ///   public void CloseWindow()
  ///   {
  ///     // Close the window (if open), given our RenderWindow method.
  ///     GUIWindowHandler.Close( RenderWindow );
  ///   }
  ///   
  ///   private void RenderWindow( EventType eventType )
  ///   {
  ///     GUILayout.Label( "Hello world" );
  ///   }
  /// }
  /// </example>
  [DoNotGenerateCustomEditor]
  public class GUIWindowHandler
  {
    /// <summary>
    /// Window data object, holding callback, size, position etc. of the window.
    /// </summary>
    public class Data
    {
      private Rect m_rect = new Rect();

      /// <returns>Rect of the window.</returns>
      public Rect GetRect() { return m_rect; }

      /// <summary>
      /// Hack to compensate for Unity "window title" offset. Off by 20 pixels.
      /// </summary>
      /// <param name="position"></param>
      /// <returns></returns>
      public bool Contains( Vector2 position )
      {
        var rect = new Rect( m_rect );
        rect.position += new Vector2( 0, -20 );
        return rect.Contains( position );
      }

      /// <summary>
      /// GUI callback method: void MyCallback( EventType eventType ) where
      /// the eventType is the current event in the window.
      /// </summary>
      public Action<EventType> Callback { get; private set; }

      /// <summary>
      /// Size of the window.
      /// </summary>
      public Vector2 Size
      {
        get
        {
          return m_rect.size;
        }
        set
        {
          m_rect.size = value;
        }
      }

      /// <summary>
      /// Position of the window.
      /// </summary>
      public Vector2 Position
      {
        get
        {
          return m_rect.position;
        }
        set
        {
          value.x = (int)value.x;
          value.y = (int)value.y;

          m_rect.position = value;
        }
      }

      /// <summary>
      /// Title of the window.
      /// </summary>
      public string Title { get; set; }

      /// <summary>
      /// If true the window is possible to move. Default false.
      /// </summary>
      public bool Movable { get; set; }

      /// <summary>
      /// Request focus to this window. Default false.
      /// </summary>
      public bool RequestFocus { get; set; }

      /// <summary>
      /// Window GUI style.
      /// </summary>
      public GUIStyle Style { get; set; } = null;

      public enum CloseEventType
      {
        None,
        ClickAndMiss,
        KeyEscape
      }

      /// <summary>
      /// </summary>
      public Func<CloseEventType, bool> CloseEventListener = delegate { return false; };

      /// <summary>
      /// This flag is true if Movable == true and left mouse button is
      /// down in the window.
      /// </summary>
      public bool IsMoving { get; set; }

      /// <summary>
      /// Window id, automatically assigned.
      /// </summary>
      public int Id { get; private set; }

      /// <summary>
      /// Construct given callback.
      /// </summary>
      /// <param name="callack"></param>
      public Data( Action<EventType> callack )
      {
        Callback     = callack;
        Id           = GUIUtility.GetControlID( FocusType.Passive );
        Movable      = false;
        IsMoving     = false;
        RequestFocus = false;
      }
    }

    private static GUIWindowHandler s_instance = null;

    public static GUIWindowHandler Instance
    {
      get
      {
        if ( !Application.isPlaying )
          throw new NotSupportedException( "GUIWindowHandler may not be instantiated in edit mode. Use Instance between Awake and Destroy." );
        if ( s_instance == null ) {
          s_instance = new GUIWindowHandler();
          var go = new GameObject( "AGXUnity.GUIWindowHandler" );
          var component = go.AddComponent<OnGUICallbackComponent>();
          component.OnGUICallback += s_instance.OnGUI;
          component.OnDestroyCallback += s_instance.OnDestroy;
        }
        return s_instance;
      }
    }

    public static bool HasInstance { get { return s_instance != null && Application.isPlaying; } }

    public int NumActiveWindows { get { return m_activeWindows.Count; } }

    /// <summary>
    /// Show a new window in scene view given callback (to render GUI), size, position and title.
    /// </summary>
    /// <param name="guiCallback">GUI callback.</param>
    /// <param name="size">Size of the window.</param>
    /// <param name="position">Position of the window.</param>
    /// <param name="title">Title of the window.</param>
    /// <param name="requestFocus">Request focus to the window.</param>
    /// <returns>Window data.</returns>
    public Data Show( Action<EventType> guiCallback,
                      Vector2 size,
                      Vector2 position,
                      string title,
                      bool requestFocus = false,
                      GUIStyle style = null )
    {
      if ( guiCallback == null )
        throw new ArgumentNullException( "guiCallback" );

      Data data = null;
      if ( !m_activeWindows.TryGetValue( guiCallback, out data ) ) {
        data = new Data( guiCallback );
        m_activeWindows.Add( guiCallback, data );
      }

      data.Size         = size;
      data.Position     = position;
      data.Title        = title;
      data.RequestFocus = requestFocus;
      data.Style        = style ?? GUI.Skin.window;

      return data;
    }

    /// <summary>
    /// Close window given the GUI callback.
    /// </summary>
    /// <param name="guiCallback">GUI callback associated to the window.</param>
    public void Close( Action<EventType> guiCallback )
    {
      m_activeWindows.Remove( guiCallback );
    }

    /// <summary>
    /// Close all windows with callbacks associated to <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">Object with GUI callbacks.</param>
    public void CloseAllWindows( object obj )
    {
      if ( obj == null )
        return;

      var windowsToRemove = from data in m_activeWindows.Values where data.Callback.Target == obj select data;
      windowsToRemove.ToList().ForEach( data => Close( data.Callback ) );
    }

    /// <summary>
    /// Finds window data for the given GUI callback.
    /// </summary>
    /// <param name="guiCallback">GUI callback associated to the window.</param>
    /// <returns>Window data.</returns>
    public Data GetWindowData( Action<EventType> guiCallback )
    {
      Data data = null;
      m_activeWindows.TryGetValue( guiCallback, out data );
      return data;
    }

    /// <summary>
    /// Finds window data given predicate, e.g., data => data.Title == myTitle.
    /// </summary>
    /// <param name="predicate">Predicate for data.</param>
    /// <returns>Data if matched with given predicate.</returns>
    public Data GetWindowData( Predicate<Data> predicate )
    {
      foreach ( var data in m_activeWindows.Values )
        if ( predicate( data ) )
          return data;
      return null;
    }

    /// <summary>
    /// Finds current window the mouse pointer is over. Null if none.
    /// </summary>
    /// <param name="mousePosition">Current scene view mouse position.</param>
    /// <returns>Window data of window the mouse pointer is over - otherwise null.</returns>
    public Data GetMouseOverWindow( Vector2 mousePosition )
    {
      foreach ( var data in m_activeWindows.Values )
        if ( data.Contains( mousePosition ) )
          return data;
      return null;
    }

    public bool RenderWindows( Event current )
    {
      var windowsToClose = new List<Data>();
      foreach ( var data in m_activeWindows.Values ) {
        var rect = GUILayout.Window( data.Id,
                                     data.GetRect(),
                                     id =>
                                     {
                                       // Call to the user method.
                                       EventType windowEventType = current.GetTypeForControl( id );
                                       data.Callback( windowEventType );

                                       // Handle movable window.
                                       if ( data.Movable ) {
                                         // We'll have Repaint, Layout etc. events here as well and
                                         // GUI.DragWindow has to be called for all these other events.
                                         // Call DragWindow from mouse down to mouse up.
                                         data.IsMoving = windowEventType != EventType.MouseUp &&
                                                         ( data.IsMoving || windowEventType == EventType.MouseDown );

                                         if ( data.IsMoving )
                                           UnityEngine.GUI.DragWindow();
                                       }

                                       EatMouseEvents( data );
                                     },
                                     data.Title,
                                     data.Style,
                                     new GUILayoutOption[] { GUILayout.Width( data.Size.x ) } );

        data.Size     = rect.size;
        data.Position = rect.position;

        if ( data.RequestFocus ) {
          UnityEngine.GUI.FocusWindow( data.Id );
          UnityEngine.GUI.BringWindowToFront( data.Id );
          data.RequestFocus = false;
        }

        bool hasListener = data.CloseEventListener.GetInvocationList().Length > 1;
        if ( hasListener ) {
          var currentCloseEvent = Data.CloseEventType.None;
          if ( IsKeyEscapeDown( current ) )
            currentCloseEvent = Data.CloseEventType.KeyEscape;
          else if ( IsLeftMouseClick( current ) && !data.Contains( current.mousePosition ) )
            currentCloseEvent = Data.CloseEventType.ClickAndMiss;

          if ( currentCloseEvent != Data.CloseEventType.None && data.CloseEventListener( currentCloseEvent ) )
            windowsToClose.Add( data );
        }
      }

      foreach ( var data in windowsToClose )
        Close( data.Callback );

      // Explicit repaint when the mouse is moved so that the window
      // seems active.
      return m_activeWindows.Count > 0 && current.type == EventType.MouseMove;
    }

    /// <summary>
    /// Eating mouse events when mouse is over window. Taken from:
    /// http://answers.unity3d.com/questions/801834/guilayout-window-consume-mouse-events.html
    /// </summary>
    /// <param name="data">Window data.</param>
    public static void EatMouseEvents( Data data )
    {
      int controlID = GUIUtility.GetControlID( data.Id, FocusType.Passive, data.GetRect() );

      switch ( Event.current.GetTypeForControl( controlID ) ) {
        case EventType.MouseDown:
          if ( data.GetRect().Contains( Event.current.mousePosition ) ) {
            GUIUtility.hotControl = controlID;
            Event.current.Use();
          }
          break;

        case EventType.MouseUp:
          if ( GUIUtility.hotControl == controlID ) {
            GUIUtility.hotControl = 0;
            Event.current.Use();
          }
          break;

        case EventType.MouseDrag:
          if ( GUIUtility.hotControl == controlID )
            Event.current.Use();
          break;

        case EventType.ScrollWheel:
          if ( data.GetRect().Contains( Event.current.mousePosition ) )
            Event.current.Use();
          break;
      }
    }

    public static bool IsKeyEscapeDown( Event current )
    {
      return current != null &&
             current.isKey &&
             current.keyCode == KeyCode.Escape &&
             current.type == EventType.KeyUp;
    }

    public static bool IsLeftMouseClick( Event current )
    {
      return !current.control &&
             !current.shift &&
             !current.alt &&
              current.type == EventType.MouseDown &&
              current.button == 0;
    }

    private void OnGUI()
    {
      RenderWindows( Event.current );
    }

    private void OnDestroy()
    {
      m_activeWindows.Clear();
      s_instance = null;
    }

    private class OnGUICallbackComponent : MonoBehaviour
    {
      public delegate void VoidCallbackDef();
      public VoidCallbackDef OnGUICallback;
      public VoidCallbackDef OnDestroyCallback;
      private void OnGUI()
      {
        if ( OnGUICallback != null )
          OnGUICallback.Invoke();
      }
      private void OnDestroy()
      {
        if ( OnDestroyCallback != null )
          OnDestroyCallback.Invoke();
        OnGUICallback = null;
        OnDestroyCallback = null;
      }
    }

    private Dictionary<Action<EventType>, Data> m_activeWindows = new Dictionary<Action<EventType>, Data>();
  }
}
