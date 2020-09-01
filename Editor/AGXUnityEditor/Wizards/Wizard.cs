using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Wizards
{
  /// <summary>
  /// Class handling windows to be shown in consecutive order - a wizard. The name and order
  /// of the windows is taken from an enum and this class uses reflection to
  /// call methods with the name of the enum value + "On" in the beginning.
  /// E.g., enum value is named "ChooseCountry" then the callback method is named
  /// "void OnChooseCounty( EventType eventType )".
  /// </summary>
  /// <example>
  /// public class ConfigureCharacterWizard : Wizards.Wizard
  /// {
  ///   public enum Windows
  ///   {
  ///     ChooseName,
  ///     ChooseAge,
  ///     Done
  ///   }
  ///   
  ///   public ConfigureCharacterWindows( Vector2 windowSize, Vector2 windowPosition )
  ///     : base( typeof( Windows ), windowSize, windowPosition )
  ///   {
  ///   }
  ///   
  ///   private void OnChooseName( EventType eventType )
  ///   {
  ///     m_name = GUILayout.TextField( m_name, Utils.GUI.Skin.textField );
  ///     // When the user presses button "Next" or "Previous" the state
  ///     // will change.
  ///     PrevNextButtons();
  ///   }
  ///   
  ///   private void OnChooseAge( EventType eventType )
  ///   {
  ///     // When the user has pressed "Next" from Choose Name we'll end up here.
  ///     ...
  ///   }
  ///   
  ///   private void OnDone( EventType eventType )
  ///   {
  ///     // Create character.
  ///   }
  /// }
  /// </example>
  public class Wizard
  {
    private int m_windowId                                = -1;
    private Rect m_windowRect                             = new Rect();
    private Type m_enumType                               = null;
    private Dictionary<int, MethodInfo> m_windowCallbacks = new Dictionary<int, MethodInfo>();

    /// <summary>
    /// Current active window to be compared with the enum, e.g.,
    /// if ( windowState.CurrentWindow == MyWindows.ShowTypeCharacterName ).
    /// </summary>
    public int CurrentWindow { get; private set; }

    /// <summary>
    /// Size of the window.
    /// </summary>
    public Vector2 WindowSize { get; set; }

    /// <returns>Number of windows.</returns>
    public int GetNumWindows()
    {
      return Enum.GetValues( m_enumType ).Length;
    }

    /// <summary>
    /// Construct given object to receive callbacks and type of the enum (containing values,
    /// Enum.GetValues( enumType )).
    /// </summary>
    /// <param name="obj">Object to receive callbacks.</param>
    /// <param name="enumType">Enum type with enum values.</param>
    public Wizard( Type enumType, Vector2 windowSize, Vector2 windowPosition )
    {
      if ( enumType == null )
        throw new ArgumentNullException( "enumType" );

      m_enumType = enumType;

      Type type = GetType();
      foreach ( var window in Enum.GetValues( m_enumType ) ) {
        string methodName = "On" + window.ToString();
        MethodInfo methodInfo = type.GetMethod( methodName, BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic );

        if ( methodInfo == null )
          Debug.Log( "Unable to bind method: " + methodName + " to object type: " + type.Name );
        else {
          ParameterInfo[] args = methodInfo.GetParameters();
          if ( args.Length != 1 || args[ 0 ].ParameterType != typeof( EventType ) ) {
            Debug.Log( "Arguments to window callback mismatch, should be \"void method( EventType eventType )\"." );
            methodInfo = null;
          }
        }

        m_windowCallbacks.Add( (int)window, methodInfo );
      }

      m_windowId   = GUIUtility.GetControlID( FocusType.Passive );
      WindowSize   = windowSize;
      m_windowRect = new Rect( windowPosition, WindowSize );
    }

    /// <summary>
    /// Update method. Call this on SceneView update.
    /// </summary>
    public void Update()
    {
      if ( CurrentWindow >= GetNumWindows() )
        return;

      MethodInfo windowMethod = m_windowCallbacks[ CurrentWindow ];
      if ( windowMethod != null ) {
        GUILayout.Window( m_windowId,
                          m_windowRect,
                          id =>
                          {
                            EventType windowEventType = Event.current.GetTypeForControl( id );
                            windowMethod.Invoke( this, new object[] { windowEventType } );
                          },
                          Enum.GetName( m_enumType, CurrentWindow ).ToString().SplitCamelCase(),
                          GUI.Skin.window,
                          new GUILayoutOption[] { GUILayout.Width( WindowSize.x ) } );
      }
      else
        NextWindow();

      HandleUtility.Repaint();
    }

    /// <summary>
    /// On scene view GUI update, called after each window has been updated.
    /// </summary>
    public virtual void OnSceneViewGUI( SceneView sceneView ) { }
    
    /// <summary>
    /// Renders two buttons named "Previous" and "Next". The
    /// "Previous" button isn't rendered in the first window.
    /// If button "Previous" is pressed, PrevWindow() is called.
    /// If button "Next" is pressed (or acceptEnterAsNext == true
    /// && key enter is released), NextWindow() is called.
    /// </summary>
    /// <param name="acceptEnterAsNext">True to accept key "enter" to behave button "Next" is pressed.</param>
    public void PrevNextButtons( bool acceptEnterAsNext = false )
    {
      bool buttonPrevPressed = false;
      bool buttonNextPressed = false;
      bool keyEnterIsReleased = acceptEnterAsNext &&
                                Event.current != null &&
                                Event.current.isKey &&
                                Event.current.type == EventType.KeyUp &&
                                ( Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter );

      GUILayout.BeginHorizontal();
      {
        // Disable button "Previous" if we're in the first window.
        EditorGUI.BeginDisabledGroup( CurrentWindow == 0 );
        {
          buttonPrevPressed = GUILayout.Button( GUI.MakeLabel( "Previous" ), GUI.Skin.button );
        }
        EditorGUI.EndDisabledGroup();

        buttonNextPressed = GUILayout.Button( GUI.MakeLabel( "Next" ), GUI.Skin.button );
      }
      GUILayout.EndHorizontal();

      if ( buttonPrevPressed )
        PrevWindow();
      else if ( buttonNextPressed || keyEnterIsReleased )
        NextWindow();
    }

    /// <summary>
    /// Go back to previous window or stays at first.
    /// </summary>
    public void PrevWindow()
    {
      if ( CurrentWindow == 0 )
        return;

      // We can't change window during Layout event because
      // the upcoming Repaint event is dependent on the window
      // currently show.
      if ( Event.current.type == EventType.Layout )
        return;

      --CurrentWindow;
    }

    /// <summary>
    /// Proceed to next window.
    /// </summary>
    public void NextWindow()
    {
      if ( CurrentWindow == GetNumWindows() )
        return;

      // We can't change window during Layout event because
      // the upcoming Repaint event is dependent on the window
      // currently show.
      if ( Event.current.type == EventType.Layout )
        return;

      ++CurrentWindow;
    }
  }
}
