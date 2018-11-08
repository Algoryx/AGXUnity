using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Rendering;
using AGXUnity.Collide;

namespace AGXUnityEditor.Menus
{
  public static class GameObjectMenu
  {
    [MenuItem( "GameObject/AGX Unity/Create Shape Visual in children", false, 0 )]
    public static void CreateVisual()
    {
      if ( Selection.activeGameObject == null ) {
        Debug.Log( "Unable to create visual: No game object selected in scene." );
        return;
      }

      if ( AssetDatabase.Contains( Selection.activeGameObject ) ) {
        Debug.Log( "Unable to create visual: Selected game object is not an instance in a scene.", Selection.activeGameObject );
        return;
      }

      var shapes = Selection.activeGameObject.GetComponentsInChildren<Shape>();
      if ( shapes.Length == 0 ) {
        Debug.Log( "Unable to create visual: Selected game object doesn't have any shapes.", Selection.activeGameObject );
        return;
      }

      int numValidCreateVisual = 0;
      int numSensors = 0;
      foreach ( var shape in shapes ) {
        var validForVisual    = !ShapeVisual.HasShapeVisual( shape ) && ShapeVisual.SupportsShapeVisual( shape );
        numValidCreateVisual += Convert.ToInt32( validForVisual );
        numSensors           += Convert.ToInt32( validForVisual && shape.IsSensor );
      }

      var createVisualForSensors = false;
      if ( numSensors > 0 )
        createVisualForSensors = EditorUtility.DisplayDialog( "Sensors",
                                                              "There are " + numSensors + " sensors in this object. Would you like to create visuals for sensors as well?",
                                                              "Yes",
                                                              "No" );

      if ( numValidCreateVisual == 0 || numSensors >= numValidCreateVisual ) {
        Debug.Log( "Create visual ignored: All shapes already have visual data or doesn't support to be visualized.", Selection.activeGameObject );
        return;
      }

      Undo.SetCurrentGroupName( "Create GameObject shape visual." );
      var grouId = Undo.GetCurrentGroup();
      foreach ( var shape in shapes ) {
        var createVisual = !ShapeVisual.HasShapeVisual( shape ) &&
                           ( createVisualForSensors || !shape.IsSensor );
        if ( createVisual ) {
          var go = ShapeVisual.Create( shape );
          if ( go != null )
            Undo.RegisterCreatedObjectUndo( go, "Shape visual" );
        }
      }

      Undo.CollapseUndoOperations( grouId );
    }
  }
}
