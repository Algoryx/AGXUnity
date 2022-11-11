using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Utils
{
  public class OrientedShapeCreateButton
  {
    public Tools.CreateOrientedShapeTool.ShapeType ShapeType { get; set; }

    public OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType shapeType )
    {
      ShapeType = shapeType;
    }

    public bool Update( Rect rect, bool isFirst, bool isLast )
    {
      var buttonType = isFirst && isFirst == isLast ? InspectorGUISkin.ButtonType.Normal :
                       isFirst                      ? InspectorGUISkin.ButtonType.Left :
                       isLast                       ? InspectorGUISkin.ButtonType.Right :
                                                      InspectorGUISkin.ButtonType.Middle;
      return InspectorGUI.Button( rect,
                                  Icon,
                                  UnityEngine.GUI.enabled,
                                  InspectorEditor.Skin.GetButton( buttonType ),
                                  $"Create new {ShapeType.ToString().ToLower()} as parent of the selected object(s)." );
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
        return m_iconMap[ (int)ShapeType ];
      }
    }
  }

  public class OrientedShapeCreateButtons
  {
    private OrientedShapeCreateButton[] m_buttons = new OrientedShapeCreateButton[]
    {
      new OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType.Box ),
      new OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType.Cylinder ),
      new OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType.Capsule ),
      new OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType.Sphere ),
      new OrientedShapeCreateButton( Tools.CreateOrientedShapeTool.ShapeType.Mesh )
    };

    public void Update( Event current, Action<Tools.CreateOrientedShapeTool.ShapeType> onClick, Action<Tools.CreateOrientedShapeTool.ShapeType> onHover)
    {
      var rect = EditorGUI.IndentedRect( EditorGUILayout.GetControlRect( false ) );
      rect.width = 36.0f;
      foreach ( var button in m_buttons ) {
        rect.xMin = rect.x;
        rect.xMax = rect.x + rect.width;

        if ( button.Update( rect,
                            button == m_buttons.First(),
                            button == m_buttons.Last() ) )
          onClick( button.ShapeType );
        if ( UnityEngine.GUI.enabled && 
             current.type == EventType.Repaint &&
             rect.Contains( current.mousePosition ) )
          onHover(button.ShapeType);
        rect.x += rect.width;
      }
    }
  }
}
