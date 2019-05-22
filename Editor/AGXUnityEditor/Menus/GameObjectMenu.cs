using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity.Rendering;
using AGXUnity.Collide;

namespace AGXUnityEditor.Menus
{
  public static class GameObjectMenu
  {
    [MenuItem( "GameObject/AGX Unity/Create Shape Visual in children", validate = false, priority = 0 )]
    public static void CreateVisual()
    {
      // Multi-selection and we're receiving one call per selected object
      // but it's not possible to process one object each call when
      // Selection.active* aren't changed - so we're blocking calls for
      // x seconds.
      if ( EditorApplication.timeSinceStartup - m_lastCreateVisualCall < 0.5 )
        return;

      var objects = Selection.GetFiltered<GameObject>( SelectionMode.Editable | SelectionMode.TopLevel );
      if ( objects.Length == 0 ) {
        Debug.Log( "Unable to create visual: Selected objects not supported (not editable - such as prefabs)." );
        return;
      }

      var validShapes            = from go in objects
                                   from shape in go.GetComponentsInChildren<Shape>()
                                   where !ShapeVisual.HasShapeVisual( shape ) &&
                                          ShapeVisual.SupportsShapeVisual( shape )
                                   select shape;
      var numSensors             = validShapes.Count( shape => shape.IsSensor );
      var createVisualForSensors = validShapes.Count() > 0 &&
                                   numSensors > 0 &&
                                   // Negated when "No" is first (left button)
                                   !EditorUtility.DisplayDialog( "Sensors",
                                                                 "There are " +
                                                                   numSensors +
                                                                   " sensors in this object. Would you like to" +
                                                                   " create visuals for sensors as well?",
                                                                 "No",
                                                                 "Yes" );

      if ( validShapes.Count() > 0 && ( createVisualForSensors || numSensors < validShapes.Count() ) ) {
        Undo.SetCurrentGroupName( "Create GameObject(s) shape visual." );
        var grouId = Undo.GetCurrentGroup();

        foreach ( var shape in validShapes ) {
          if ( shape.IsSensor && !createVisualForSensors )
            continue;

          var visual = ShapeVisual.Create( shape );
          if ( visual != null )
            Undo.RegisterCreatedObjectUndo( visual, "Create visual: " + shape.name );
        }

        Undo.CollapseUndoOperations( grouId );
      }
      else {
        Debug.Log( "Create visual ignored: All shapes already have visual data or doesn't support to be visualized.",
                   Selection.activeGameObject );
      }

      m_lastCreateVisualCall = EditorApplication.timeSinceStartup;
    }

    private static double m_lastCreateVisualCall = 0.0;
  }
}
