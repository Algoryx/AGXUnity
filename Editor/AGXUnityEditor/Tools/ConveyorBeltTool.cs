using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Model;

using Object = UnityEngine.Object;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomEditor( typeof( ConveyorBelt ) )]
  public class BeltEditor : InspectorEditor
  {
    protected override void OnTargetsDeleted()
    {
      // Completely deleted so it will be hard to remove wheels etc.
      if ( Selection.activeGameObject == null )
        return;

      var undoGroupId = Undo.GetCurrentGroup();
      var tracks = Selection.activeGameObject.GetComponents<Track>();
      foreach ( var track in tracks ) {
        Undo.RecordObject( track, "Removing track wheels" );
        foreach ( var trackWheel in track.Wheels ) {
          track.Remove( trackWheel );
          Undo.DestroyObjectImmediate( trackWheel );
        }
        Undo.DestroyObjectImmediate( track );
      }
      if ( Selection.activeGameObject.GetComponent<AGXUnity.Rendering.TrackRenderer>() != null )
        Undo.DestroyObjectImmediate( Selection.activeGameObject.GetComponent<AGXUnity.Rendering.TrackRenderer>() );
      Undo.CollapseUndoOperations( undoGroupId );
    }
  }

  [CustomTool( typeof( ConveyorBelt ) )]
  public class BeltTool : CustomTargetTool
  {
    public ConveyorBelt Belt { get { return Targets[ 0 ] as ConveyorBelt; } }

    public BeltTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Belt.ResourceHandler = ResourceHandler;

      Belt.RemoveInvalidRollers();

      foreach ( var track in Belt.Tracks )
        if ( ( track.hideFlags & HideFlags.HideInInspector ) == 0 )
          track.hideFlags |= HideFlags.HideInInspector;

      if ( Belt.Tracks.Length == 0 )
        Belt.AddDefaultComponents();
    }

    public override void OnRemove()
    {
      Belt.ResourceHandler = null;
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      InspectorGUI.ToolListGUI( this,
                                Belt.Rollers,
                                "Rollers",
                                wheel => Belt.Add( wheel ),
                                wheel => Belt.Remove( wheel ),
                                OnRollerGUI );

      using ( new GUI.EnabledBlock( false ) )
        InspectorGUI.ToolArrayGUI( this,
                                   Belt.Tracks,
                                   "Tracks" );
    }

    private void OnRollerGUI( GameObject roller, int index )
    {
      var wheel = roller.GetComponent<TrackWheel>();
      if ( wheel == null )
        return;
      var newModel = (TrackWheelModel)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Model" ),
                                                                 wheel.Model,
                                                                 InspectorEditor.Skin.Popup );
      var newProperties = (TrackWheelProperty)EditorGUILayout.EnumFlagsField( GUI.MakeLabel( "Properties" ),
                                                                              wheel.Properties,
                                                                              InspectorEditor.Skin.Popup );
      if ( newModel != wheel.Model || newProperties != wheel.Properties ) {
        var trackWheels = roller.GetComponents<TrackWheel>();
        Undo.RecordObjects( trackWheels, "Track Wheel Model" );
        foreach ( var trackWheel in trackWheels ) {
          trackWheel.Model = newModel;
          trackWheel.Properties = newProperties;
        }
      }
    }

    private Object ResourceHandler( ConveyorBelt.ResourceHandlerRequest request, Object context, Type type )
    {
      if ( request == ConveyorBelt.ResourceHandlerRequest.Begin ) {
        if ( m_undoGroupId < 0 ) {
          Undo.SetCurrentGroupName( "Belt" );
          m_undoGroupId = Undo.GetCurrentGroup();
          if ( m_undoGroupId < 0 )
            Debug.Log( "Undo group id < 0: " + m_undoGroupId.ToString() );
        }
        else
          Debug.Log( "Undo id is already set." );
        return null;
      }
      else if ( request == ConveyorBelt.ResourceHandlerRequest.End ) {
        if ( m_undoGroupId >= 0 )
          Undo.CollapseUndoOperations( m_undoGroupId );
        m_undoGroupId = -1;
        return null;
      }
      else if ( request == ConveyorBelt.ResourceHandlerRequest.AboutToChange ) {
        Undo.RecordObject( context, $"{context.name} changes." );
        return null;
      }
      else if ( request == ConveyorBelt.ResourceHandlerRequest.AddComponent )
        return Undo.AddComponent( context as GameObject, type );
      else if ( request == ConveyorBelt.ResourceHandlerRequest.DestroyObject ) {
        Undo.DestroyObjectImmediate( context );
        return null;
      }

      Debug.LogError( $"Unknown Belt resource request: {request}" );

      return null;
    }

    private int m_undoGroupId = -1;
  }
}
