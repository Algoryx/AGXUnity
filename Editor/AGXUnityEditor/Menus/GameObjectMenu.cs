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

      var go = FindSelectedGivenCommand( command );
      if ( go == null ) {
        Debug.LogWarning( $"Ignoring visual for {command.context.name} - object isn't editable." );
        return;
      }

      var validShapes = from shape in go.GetComponentsInChildren<Shape>()
                        where !ShapeVisual.HasShapeVisual( shape ) &&
                               ShapeVisual.SupportsShapeVisual( shape )
                        select shape;
      var numSensors = validShapes.Count( shape => shape.IsSensor );
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

    [MenuItem( "GameObject/AGXUnity/Save/URDF Model(s) as prefab(s)...", validate = false, priority = 11 )]
    public static void ExportUrdfAsPrefab( MenuCommand command )
    {
      SaveUrdfs( command, true );
    }

    [MenuItem( "GameObject/AGXUnity/Save/URDF Model(s) assets...", validate = false, priority = 11 )]
    public static void ExportUrdfAssets( MenuCommand command )
    {
      SaveUrdfs( command, false );
    }

    private static void SaveUrdfs( MenuCommand command, bool asPrefab )
    {
#if UNITY_2019_4_OR_NEWER
      if ( command.context == null )
        return;
#endif
      var strContext = asPrefab ? "prefab(s)" : "assets";
      var rootGameObject = FindSelectedGivenCommand( command );
      if ( rootGameObject == null ) {
        Debug.LogWarning( $"Saving URDF Model as {strContext} failed for {command.context.name} - object isn't editable.",
                          command.context );
        return;
      }

      var urdfModels = AGXUnity.IO.URDF.Utils.GetElementsInChildren<AGXUnity.IO.URDF.Model>( rootGameObject );
      if ( urdfModels.Length == 0 ) {
        Debug.LogWarning( $"Saving URDF Model as {strContext} failed for {command.context.name} - the game object doesn't contain any URDF model(s).",
                          command.context );
        return;
      }

      var directory = IO.URDF.Prefab.OpenFolderPanel( asPrefab ? "URDF Prefab Directory" : "URDF Assets Directory" );
      if ( string.IsNullOrEmpty( directory ) )
        return;

      foreach ( var urdfModel in urdfModels ) {
        var modelRootGameObject = AGXUnity.IO.URDF.Utils.FindGameObjectWithElement( rootGameObject, urdfModel );
        if ( modelRootGameObject == null )
          continue;
        if ( asPrefab )
          IO.URDF.Prefab.Create( urdfModel, modelRootGameObject, directory );
        else
          IO.URDF.Prefab.CreateAssets( urdfModel, modelRootGameObject, directory, modelRootGameObject.name );
      }
    }

    private static GameObject FindSelectedGivenCommand( MenuCommand command )
    {
      return Selection.GetFiltered<GameObject>( SelectionMode.TopLevel |
                                                SelectionMode.Editable )
                      .FirstOrDefault( selected => selected == command.context );
    }
  }
}
