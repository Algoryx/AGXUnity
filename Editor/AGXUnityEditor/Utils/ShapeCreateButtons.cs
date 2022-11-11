using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

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

    public StateData State { get; private set; }

    public ShapeCreateButton( Tools.ShapeCreateTool.ShapeType shapeType )
    {
      State = new StateData()
      {
        ShapeType = shapeType,
        Axis = ShapeInitializationData.Axes.None,
        ExpandRadius = false,
        DropdownEnabled = false,
        CreatePressed = false
      };
    }

    public void Reset()
    {
      State.Axis            = ShapeInitializationData.Axes.None;
      State.ExpandRadius    = false;
      State.DropdownEnabled = false;
      State.CreatePressed   = false;
    }

    public bool Update( Rect rect, bool isFirst, bool isLast )
    {
      var buttonType = isFirst && isFirst == isLast ? InspectorGUISkin.ButtonType.Normal :
                       isFirst                      ? InspectorGUISkin.ButtonType.Left :
                       isLast                       ? InspectorGUISkin.ButtonType.Right :
                                                      InspectorGUISkin.ButtonType.Middle;
      var toggleDropdown = InspectorGUI.Toggle( rect,
                                                Icon,
                                                State.DropdownEnabled,
                                                UnityEngine.GUI.enabled,
                                                InspectorEditor.Skin.GetButton( buttonType ),
                                                "Create new " +
                                                State.ShapeType.ToString().ToLower() +
                                                " as parent of the selected object(s)." ) != State.DropdownEnabled;

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

      bool hasRadius = State.ShapeType == Tools.ShapeCreateTool.ShapeType.Cylinder ||
                       State.ShapeType == Tools.ShapeCreateTool.ShapeType.Capsule ||
                       State.ShapeType == Tools.ShapeCreateTool.ShapeType.Sphere;

      OnShapeConfigGUI( hasRadius );

      using ( InspectorGUI.IndentScope.Create( 2 ) ) {
        var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false, 25.0f ) );
        rect.width = 50.0f;

        if ( hasRadius ) {
          State.CreatePressed = OnButtonGUI( rect,
                                             "Axis 1",
                                             InspectorGUISkin.ButtonType.Left,
                                             current,
                                             () =>
                                             {
                                               State.Axis = ShapeInitializationData.Axes.Axis_1;
                                             } );
          rect.x += rect.width;
          State.CreatePressed = OnButtonGUI( rect,
                                             "Axis 2",
                                             InspectorGUISkin.ButtonType.Middle,
                                             current,
                                             () =>
                                             {
                                               State.Axis = ShapeInitializationData.Axes.Axis_2;
                                             } ) || State.CreatePressed;
          rect.x += rect.width;
          State.CreatePressed = OnButtonGUI( rect,
                                             "Axis 3",
                                             InspectorGUISkin.ButtonType.Right,
                                             current,
                                             () =>
                                             {
                                               State.Axis = ShapeInitializationData.Axes.Axis_3;
                                             } ) || State.CreatePressed;
        }
        else {
          State.CreatePressed = OnButtonGUI( rect,
                                             "Create",
                                             InspectorGUISkin.ButtonType.Normal,
                                             current,
                                             () =>
                                             {
                                               State.Axis = ShapeInitializationData.Axes.Default;
                                             } );
        }
      }

      return State;
    }

    private void OnShapeConfigGUI( bool hasRadius )
    {
      using ( InspectorGUI.IndentScope.Create( 2 ) ) {
        State.ShapeAsParent = InspectorGUI.Toggle( GUI.MakeLabel( "Shape as parent" ),
                                                   State.ShapeAsParent );
        if ( hasRadius ) {
          State.ExpandRadius = InspectorGUI.Toggle( GUI.MakeLabel( "Expand radius" ),
                                                    State.ExpandRadius );
        }
      }
    }

    /// <returns>True when button is pressed.</returns>
    private bool OnButtonGUI( Rect rect,
                              string name,
                              InspectorGUISkin.ButtonType buttonType,
                              Event current,
                              Action onMouseOver )
    {
      using ( InspectorGUI.IndentScope.Create( 2 ) ) {
        var down = UnityEngine.GUI.Button( rect,
                                           GUI.MakeLabel( name ),
                                           InspectorEditor.Skin.GetButton( buttonType ) );
        if ( current.type == EventType.Repaint &&
             rect.Contains( current.mousePosition ) )
          onMouseOver();

        return down;
      }
    }

    private static MiscIcon[] m_iconMap = null;
    private MiscIcon Icon
    {
      get
      {
        if ( m_iconMap == null )
          m_iconMap = new MiscIcon[]
          {
            MiscIcon.Box,
            MiscIcon.Cylinder,
            MiscIcon.Capsule,
            MiscIcon.Sphere,
            MiscIcon.Mesh
          };
        return m_iconMap[ (int)State.ShapeType ];
      }
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

    public void OnGUI( Event current )
    {
      var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false ) );
      rect.width = 36.0f;
      foreach ( var button in m_buttons ) {
        rect.xMin = rect.x;
        rect.xMax = rect.x + rect.width;
        bool pressed = button.Update( rect,
                                      button == m_buttons.First(),
                                      button == m_buttons.Last() );
        if ( pressed )
          Selected = button.State.DropdownEnabled ? button : null;
        rect.x += rect.width;
      }

      foreach ( var button in m_buttons )
        button.UpdateDropdown( current );
    }
  }
}
