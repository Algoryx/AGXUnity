﻿using AGXUnity.Collide;
using AGXUnity.Rendering;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Menus
{
  public static class GameObjectMenu
  {
    [MenuItem( "GameObject/AGXUnity/Create Shape Visual in children", validate = true )]
    public static bool CreateVisualValidation( MenuCommand command )
    {
      // Issue reported to Unity that command.context == null, making validation
      // impossible so we'll do some additional validations in CreateVisual instead.
      // Update 2024: Apparently this is by design so we'll leave this in https://issuetracker.unity3d.com/issues/menuitem-validate-function-is-called-twice-and-always-returns-a-null-menucommand-dot-context-first-when-right-clicking-a-gameobject

      return true;
    }

    [MenuItem( "GameObject/AGXUnity/Create Shape Visual in children", priority = 21 )]
    public static void CreateVisual( MenuCommand command )
    {
      if ( command.context == null )
        return;

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
      if ( command.context == null )
        return;
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
