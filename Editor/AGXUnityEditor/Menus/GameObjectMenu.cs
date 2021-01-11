using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Rendering;
using AGXUnity.Collide;

namespace AGXUnityEditor.Menus
{
  public static class GameObjectMenu
  {
    [MenuItem( "GameObject/AGXUnity/Create Shape Visual in children", validate = true )]
    public static bool CreateVisualValidation( MenuCommand command )
    {
#if UNITY_2019_4_OR_NEWER
      // Issue reported to Unity that command.context == null, making validation
      // impossible so we'll do some additional validations in CreateVisual instead.
      return true;
#else
      return command != null &&
             command.context != null;
#endif
    }

    [MenuItem( "GameObject/AGXUnity/Create Shape Visual in children", priority = 21 )]
    public static void CreateVisual( MenuCommand command )
    {
#if UNITY_2019_4_OR_NEWER
      if ( command.context == null )
        return;
#endif

      var go = Selection.GetFiltered<GameObject>( SelectionMode.TopLevel |
                                                  SelectionMode.Editable ).FirstOrDefault( selected => selected == command.context );
      if ( go == null ) {
        Debug.LogWarning( $"Ignoring visual for {command.context.name} - object isn't editable." );
        return;
      }

      var validShapes            = from shape in go.GetComponentsInChildren<Shape>()
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
    }
  }
}
