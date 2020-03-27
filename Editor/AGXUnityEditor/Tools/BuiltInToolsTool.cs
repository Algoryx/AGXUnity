﻿using System;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.Tools
{
  public class BuiltInToolsTool : Tool
  {
    public SelectGameObjectTool SelectGameObject
    {
      get { return GetChild<SelectGameObjectTool>(); }
      private set
      {
        if ( SelectGameObject != null )
          SelectGameObject.PerformRemoveFromParent();

        if ( value != null ) {
          AddChild( value );
          value.MenuTool.RemoveOnClickMiss = true;
        }
      }
    }

    public Utils.KeyHandler SelectGameObjectKeyHandler { get { return EditorSettings.Instance.BuiltInToolsTool_SelectGameObjectKeyHandler; } }

    public Utils.KeyHandler SelectRigidBodyKeyHandler { get { return EditorSettings.Instance.BuiltInToolsTool_SelectRigidBodyKeyHandler; } }

    public PickHandlerTool PickHandler
    {
      get { return GetChild<PickHandlerTool>(); }
      set
      {
        if ( PickHandler != null )
          PickHandler.PerformRemoveFromParent();

        if ( value != null )
          AddChild( value );
      }
    }

    public Utils.KeyHandler PickHandlerKeyHandler { get { return EditorSettings.Instance.BuiltInToolsTool_PickHandlerKeyHandler; } }

    public AGXUnity.ShapeMaterial DroppedShapeMaterial
    {
      get { return EditorData.Instance.GetStaticData( "BuiltInToolsTool.DroppedShapeMaterial" ).Asset as AGXUnity.ShapeMaterial; }
      set { EditorData.Instance.GetStaticData( "BuiltInToolsTool.DroppedShapeMaterial" ).Asset = value; }
    }

    public bool SelectGameObjectTrigger( Event current, SceneView sceneView )
    {
      return SelectGameObjectKeyHandler.IsDown &&
             EditorWindow.mouseOverWindow == sceneView &&
            !current.control &&
            !current.shift &&
            !current.alt;
    }

    public bool SelectRigidBodyTrigger( Event current, SceneView sceneView )
    {
      return SelectRigidBodyKeyHandler.IsDown &&
             EditorWindow.mouseOverWindow == sceneView &&
            !current.control &&
            !current.shift &&
            !current.alt;
    }

    public bool PickHandlerTrigger( Event current, SceneView sceneView )
    {
      return EditorApplication.isPlaying &&
             PickHandler == null &&
             EditorWindow.mouseOverWindow == sceneView &&
             PickHandlerKeyHandler.IsDown &&
            !current.shift &&
            !current.alt &&
             current.type == EventType.MouseDown &&
             current.button >= 0 &&
             current.button <= 2;
    }

    public BuiltInToolsTool()
      : base( isSingleInstanceTool: false )
    {
      AddKeyHandler( "SelectObject", SelectGameObjectKeyHandler );
      AddKeyHandler( "SelectRigidBody", SelectRigidBodyKeyHandler );
      AddKeyHandler( "PickHandler", PickHandlerKeyHandler );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      var currentEvent = Event.current;

      HandleSceneViewSelectTool( currentEvent, sceneView );
      HandlePickHandler( currentEvent, sceneView );
      HandleDragDrop( currentEvent, sceneView );
    }

    private void HandleSceneViewSelectTool( Event current, SceneView sceneView )
    {
      // TODO: Add keys to select body, shape etc.

      // Routes each selected object to its correct selection.
      // Assigning 'selectedObjects' to 'Selection.objects' doesn't
      // trigger onSelectionChanged (desired behavior).
      UnityEngine.Object[] selectedObjects = Selection.objects;
      bool selectRigidBodyMode = SelectRigidBodyTrigger( current, sceneView );
      bool hasChanges = false;
      for ( int i = 0; i < selectedObjects.Length; ++i ) {
        if ( selectedObjects[ i ] == null )
          continue;

        // TODO: Key combo to select bodies etc.
        UnityEngine.Object routedObject = Manager.RouteObject( selectedObjects[ i ] );
        AGXUnity.RigidBody rigidBody = selectRigidBodyMode &&
                                       routedObject != null &&
                                       routedObject is GameObject ?
                                         ( routedObject as GameObject ).GetComponentInParent<AGXUnity.RigidBody>() :
                                         null;
        selectedObjects[ i ] = rigidBody != null ? rigidBody.gameObject : routedObject;
        hasChanges = true;
      }
      if ( hasChanges )
        Selection.objects = selectedObjects;

      if ( SelectGameObjectTrigger( current, sceneView ) ) {
        // User is holding activate select tool - SelectGameObjectTool is waiting for the mouse click.
        if ( SelectGameObject == null )
          SelectGameObject = new SelectGameObjectTool() { OnSelect = go => { Selection.activeGameObject = go; } };
      }
      // The user has released select game object trigger and the window isn't showing.
      else if ( SelectGameObject != null && !SelectGameObject.SelectionWindowActive )
        SelectGameObject = null;
    }

    private void HandlePickHandler( Event current, SceneView sceneView )
    {
      if ( !PickHandlerTrigger( current, sceneView ) )
        return;

      Predicate<Event> removePredicate = null;
      AGXUnity.PickHandler.DofTypes dofTypes = AGXUnity.PickHandler.DofTypes.Translation;

      // Left mouse button = ball joint.
      if ( current.button == 0 ) {
        // If left mouse - make sure the manager is taking over this mouse event.
        Manager.HijackLeftMouseClick();

        removePredicate = ( e ) => { return Manager.HijackLeftMouseClick(); };
        // Ball joint.
        dofTypes = AGXUnity.PickHandler.DofTypes.Translation;
      }
      // Middle/scroll mouse button = lock joint.
      else if ( current.button == 2 ) {
        current.Use();

        removePredicate = ( e ) => { return e.type == EventType.MouseUp && e.button == 2; };
        // Lock joint.
        dofTypes = AGXUnity.PickHandler.DofTypes.Translation | AGXUnity.PickHandler.DofTypes.Rotation;
      }
      // Right mouse button = angular lock?
      else if ( current.button == 1 ) {
        current.Use();

        removePredicate = ( e ) => { return e.type == EventType.MouseUp && e.button == 1; };
        // Angular lock.
        dofTypes = AGXUnity.PickHandler.DofTypes.Rotation;
      }

      PickHandler = new PickHandlerTool( dofTypes, removePredicate );
    }

    private void HandleDragDrop( Event current, SceneView sceneView )
    {
      var mouseOverSceneView = Manager.IsMouseOverWindow( sceneView );
      var mouseOverHierarchy = !mouseOverSceneView &&
                               Manager.IsMouseOverWindow( Manager.EditorWindowType.SceneHierarchyWindow );

      var dragDropSceneViewActive = ( mouseOverSceneView || mouseOverHierarchy ) &&
                                    ( current.type == EventType.DragPerform || current.type == EventType.DragUpdated );
      if ( !dragDropSceneViewActive )
        return;

      Manager.UpdateMouseOverPrimitives( current, true );

      var mouseOverShapes         = HasShapeMaterialProperty( Manager.MouseOverObject );
      var isDraggingShapeMaterial = DragAndDrop.objectReferences.Length == 1 &&
                                    DragAndDrop.objectReferences[ 0 ] is AGXUnity.ShapeMaterial;
      DragAndDrop.visualMode      = isDraggingShapeMaterial && mouseOverShapes ?
                                      DragAndDropVisualMode.Copy :
                                      DragAndDropVisualMode.Rejected;
      if ( mouseOverShapes &&
           isDraggingShapeMaterial &&
           Event.current.type == EventType.DragPerform ) {
        DragAndDrop.AcceptDrag();

        DroppedShapeMaterial = DragAndDrop.objectReferences[ 0 ] as AGXUnity.ShapeMaterial;

        var menuTool = new SelectGameObjectDropdownMenuTool() { Target = Manager.MouseOverObject };
        menuTool.OnSelect = go =>
        {
          var shapes   = go.GetComponentsInChildren<AGXUnity.Collide.Shape>();
          var wires    = go.GetComponentsInChildren<AGXUnity.Wire>();
          var cables   = go.GetComponentsInChildren<AGXUnity.Cable>();
          var tracks   = go.GetComponentsInChildren<AGXUnity.Model.Track>();
          var terrains = go.GetComponentsInChildren<AGXUnity.Model.DeformableTerrain>();
          Action assignAll                = () =>
          {
            Undo.SetCurrentGroupName( "Assigning shape materials." );
            var undoGroup = Undo.GetCurrentGroup();
            foreach ( var shape in shapes ) {
              Undo.RecordObject( shape, "New shape material" );
              shape.Material = DroppedShapeMaterial;
            }
            foreach ( var wire in wires ) {
              Undo.RecordObject( wire, "New shape material" );
              wire.Material = DroppedShapeMaterial;
            }
            foreach ( var cable in cables ) {
              Undo.RecordObject( cable, "New shape material" );
              cable.Material = DroppedShapeMaterial;
            }
            foreach ( var track in tracks ) {
              Undo.RecordObject( track, "New shape material" );
              track.Material = DroppedShapeMaterial;
            }
            foreach ( var terrain in terrains ) {
              Undo.RecordObject( terrain, "New shape material" );
              terrain.Material = DroppedShapeMaterial;
            }

            // TODO GUI: Call RigidBody.UpdateMassProperties for affected bodies.

            Undo.CollapseUndoOperations( undoGroup );
          };

          var sumSupported = shapes.Length + wires.Length + cables.Length + tracks.Length + terrains.Length;
          if ( sumSupported == 0 )
            Debug.LogWarning( "Object selected doesn't have shapes, wires, cables or tracks.", go );
          else if ( sumSupported == 1 || EditorUtility.DisplayDialog( "Assign shape materials",
                                                                      string.Format( "Assign materials to:\n  - #shapes: {0}\n  - #wires: {1}\n  - #cables: {2}\n  - #tracks: {3}",
                                                                                     shapes.Length, wires.Length, cables.Length, tracks.Length ), "Assign", "Ignore all" ) )
            assignAll();

          DroppedShapeMaterial = null;
        };
        menuTool.Show();
        AddChild( menuTool );
      }
    }

    private bool HasShapeMaterialProperty( GameObject gameObject )
    {
      if ( gameObject == null )
        return false;

      return gameObject.GetComponentsInChildren<AGXUnity.Collide.Shape>().Length > 0 ||
             gameObject.GetComponentsInParent<AGXUnity.Collide.Shape>().Length > 0 ||
             gameObject.GetComponentsInChildren<AGXUnity.Wire>().Length > 0 ||
             gameObject.GetComponentsInChildren<AGXUnity.Cable>().Length > 0 ||
             gameObject.GetComponentsInChildren<AGXUnity.Model.Track>().Length > 0 ||
             gameObject.GetComponentsInChildren<AGXUnity.Model.DeformableTerrain>().Length > 0;
    }
  }
}
