using System;
using UnityEngine;
using UnityEditor;
using AGXUnity.Model;

using Object = UnityEngine.Object;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomEditor( typeof( Belt ) )]
  public class BeltEditor : InspectorEditor
  {
    protected override void OnTargetsDeleted()
    {
      // Completely deleted so it will be hard to remove wheels etc.
      if ( Selection.activeGameObject == null )
        return;

      var undoGroupId = Undo.GetCurrentGroup();
      var tracks = Selection.activeGameObject.GetComponents<AGXUnity.Model.Track>();
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

  [CustomTool( typeof( Belt ) )]
  public class BeltTool : CustomTargetTool
  {
    public Belt Belt { get { return Targets[ 0 ] as Belt; } }

    public BeltTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Belt.ResourceHandler = ResourceHandler;

      Belt.RemoveInvalidRollers();

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
                                wheel => Belt.Remove( wheel ) );

      using ( new GUI.EnabledBlock( false ) )
        InspectorGUI.ToolArrayGUI( this,
                                   Belt.Tracks,
                                   "Tracks" );
    }

    private Object ResourceHandler( Belt.ResourceHandlerRequest request, Object context, Type type )
    {
      if ( request == Belt.ResourceHandlerRequest.Begin ) {
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
      else if ( request == Belt.ResourceHandlerRequest.End ) {
        if ( m_undoGroupId >= 0 )
          Undo.CollapseUndoOperations( m_undoGroupId );
        m_undoGroupId = -1;
        return null;
      }
      else if ( request == Belt.ResourceHandlerRequest.AboutToChange ) {
        Undo.RecordObject( context, $"{context.name} changes." );
        return null;
      }
      else if ( request == Belt.ResourceHandlerRequest.AddComponent )
        return Undo.AddComponent( context as GameObject, type );
      else if ( request == Belt.ResourceHandlerRequest.DestroyObject ) {
        Undo.DestroyObjectImmediate( context );
        return null;
      }

      Debug.LogError( $"Unknown Belt resource request: {request}" );

      return null;
    }

    private int m_undoGroupId = -1;
  }
}
