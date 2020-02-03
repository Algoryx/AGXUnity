using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

namespace AGXUnityEditor.Utils
{
  public class ShapeCreateButton
  {
    public class StateData
    {
      public Tools.ShapeCreateTool.ShapeType ShapeType { get; set; }
      public ShapeInitializationData.Axes Axis { get; set; }
      public bool ExpandRadius { get; set; }
      public bool DropdownEnabled { get; set; }
      public bool ShapeAsParent
      {
        get
        {
          return EditorData.Instance.GetStaticData( "ShapeCreateButton.ShapeAsParent" ).Bool;
        }
        set
        {
          EditorData.Instance.GetStaticData( "ShapeCreateButton.ShapeAsParent" ).Bool = value;
        }
      }
      public bool CreatePressed { get; set; }
    }

    private Rect m_buttonRect;

    public StateData State { get; private set; }

    public ShapeCreateButton( Tools.ShapeCreateTool.ShapeType shapeType )
    {
      State = new StateData() { ShapeType = shapeType, Axis = ShapeInitializationData.Axes.None, ExpandRadius = false, DropdownEnabled = false, CreatePressed = false };
    }

    public void Reset()
    {
      State.Axis            = ShapeInitializationData.Axes.None;
      State.ExpandRadius    = false;
      State.DropdownEnabled = false;
      State.CreatePressed   = false;
    }

    public bool Update( Event current )
    {
      bool toggleDropdown = GUILayout.Button( GUI.MakeLabel( State.ShapeType.ToString().Substring( 0, 3 ),
                                                             true,
                                                             "Create new " + State.ShapeType.ToString().ToLower() + "as parent of the selected object(s)." ),
                                              InspectorEditor.Skin.GetButton( State.DropdownEnabled, State.ShapeType == Tools.ShapeCreateTool.ShapeType.Box ?
                                                                                                       InspectorGUISkin.ButtonType.Left :
                                                                                                     State.ShapeType == Tools.ShapeCreateTool.ShapeType.Mesh ?
                                                                                                       InspectorGUISkin.ButtonType.Right :
                                                                                                       InspectorGUISkin.ButtonType.Middle ),
                                              GUILayout.Width( 36 ),
                                              GUILayout.Height( 25 ) );
      if ( current.type == EventType.Repaint )
        m_buttonRect = GUILayoutUtility.GetLastRect();

      if ( toggleDropdown )
        State.DropdownEnabled = !State.DropdownEnabled;

      return toggleDropdown;
    }

    public StateData UpdateDropdown( Event current )
    {
      if ( current.type == EventType.Repaint )
        State.Axis = ShapeInitializationData.Axes.None;

      if ( !State.DropdownEnabled )
        return State;

      var skin                        = InspectorEditor.Skin;
      bool hasRadius                  = State.ShapeType == Tools.ShapeCreateTool.ShapeType.Cylinder ||
                                        State.ShapeType == Tools.ShapeCreateTool.ShapeType.Capsule ||
                                        State.ShapeType == Tools.ShapeCreateTool.ShapeType.Sphere;
      float guiStartOffset            = m_buttonRect.position.x - 14;
      GUIStyle mouseOverStyle         = new GUIStyle( skin.Button );
      mouseOverStyle.hover.background = mouseOverStyle.onActive.background;

      OnShapeConfigGUI( guiStartOffset, hasRadius );

      if ( hasRadius ) {
        GUILayout.BeginHorizontal();
        {
          GUILayout.Space( guiStartOffset );

          State.CreatePressed = OnButtonGUI( "Axis 1", mouseOverStyle, current, () => { State.Axis = ShapeInitializationData.Axes.Axis_1; } );
          State.CreatePressed = OnButtonGUI( "Axis 2", mouseOverStyle, current, () => { State.Axis = ShapeInitializationData.Axes.Axis_2; } ) || State.CreatePressed;
          State.CreatePressed = OnButtonGUI( "Axis 3", mouseOverStyle, current, () => { State.Axis = ShapeInitializationData.Axes.Axis_3; } ) || State.CreatePressed;
        }
        GUILayout.EndHorizontal();
      }
      else {
        using ( GUI.IndentScope.Create() )
          State.CreatePressed = OnButtonGUI( "Create", mouseOverStyle, current, () => { State.Axis = ShapeInitializationData.Axes.Default; } );
      }

      return State;
    }

    private void OnShapeConfigGUI( float guiStartOffset, bool hasRadius )
    {
      var skin             = InspectorEditor.Skin;
      var smallLabel       = new GUIStyle( skin.Label );
      smallLabel.alignment = TextAnchor.MiddleLeft;
      smallLabel.fontSize  = 11;

      // TODO: Choice to add have the graphics or shape as parent.
      GUILayout.BeginHorizontal();
      {
        GUILayout.Space( guiStartOffset );
        State.ShapeAsParent = GUI.Toggle( GUI.MakeLabel( "Shape as parent to graphical object" ),
                                          State.ShapeAsParent );
      }
      GUILayout.EndHorizontal();

      if ( hasRadius ) {
        GUILayout.BeginHorizontal();
        {
          GUILayout.Space( guiStartOffset );
          State.ExpandRadius = GUI.Toggle( GUI.MakeLabel( "Expand radius" ),
                                           State.ExpandRadius );
        }
        GUILayout.EndHorizontal();
      }
    }

    /// <returns>True when button is pressed.</returns>
    private bool OnButtonGUI( string name, GUIStyle buttonStyle, Event current, Action onMouseOver )
    {
      bool down = GUILayout.Button( GUI.MakeLabel( name ), buttonStyle, GUILayout.Width( 52 ), GUILayout.Width( 25 ) );
      if ( current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains( current.mousePosition ) )
        onMouseOver();

      return down;
    }
  }

  public class ShapeCreateButtons
  {
    private ShapeCreateButton[] m_buttons = new ShapeCreateButton[]
    {
      new ShapeCreateButton( Tools.ShapeCreateTool.ShapeType.Box ),
      new ShapeCreateButton( Tools.ShapeCreateTool.ShapeType.Cylinder ),
      new ShapeCreateButton( Tools.ShapeCreateTool.ShapeType.Capsule ),
      new ShapeCreateButton( Tools.ShapeCreateTool.ShapeType.Sphere ),
      new ShapeCreateButton( Tools.ShapeCreateTool.ShapeType.Mesh )
    };

    private ShapeCreateButton m_selected = null;
    public ShapeCreateButton Selected
    {
      get { return m_selected; }
      set
      {
        if ( m_selected == value )
          return;

        if ( m_selected != null )
          m_selected.Reset();

        if ( value != null )
          value.State.DropdownEnabled = true;

        m_selected = value;
      }
    }

    public void Reset()
    {
      foreach ( var button in m_buttons )
        button.Reset();
    }

    public void OnGUI( Event current, int indentPixels )
    {
      GUILayout.BeginHorizontal();
      {
        GUILayout.Space( indentPixels );
        using ( new GUI.ColorBlock( Color.Lerp( UnityEngine.GUI.color, Color.red, 0.1f ) ) )
          foreach ( var button in m_buttons ) {
            bool pressed = button.Update( Event.current );
            if ( pressed )
              Selected = button.State.DropdownEnabled ? button : null;
          }
      }
      GUILayout.EndHorizontal();

      foreach ( var button in m_buttons )
        button.UpdateDropdown( current );
    }
  }
}
