using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using AGXUnity.Models;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Track ) )]
  public class TrackTool : CustomTargetTool
  {
    public Track Track { get { return Targets[ 0 ] as Track; } }

    public TrackTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      Track.RemoveInvalidWheels();
    }

    public override void OnRemove()
    {
      Manager.SceneViewGUIWindowHandler.CloseAllWindows( this );
    }

    public override void OnPreTargetMembersGUI()
    {
      Track.RemoveInvalidWheels();

      bool toggleSelectWheel = false;
      if ( !EditorApplication.isPlaying && NumTargets == 1 ) {
        using ( new GUILayout.HorizontalScope() ) {
          GUI.ToolsLabel( InspectorEditor.Skin );
          using ( GUI.ToolButtonData.ColorBlock ) {
            toggleSelectWheel = GUI.ToolButton( '\u274D',
                                                SelectWheelToolEnable,
                                                "Select track wheel to add in scene view.",
                                                InspectorEditor.Skin );
          }
        }
        GUI.Separator();
      }

      if ( toggleSelectWheel )
        SelectWheelToolEnable = !SelectWheelToolEnable;
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      Undo.RecordObject( Track, "Track wheel add/remove." );

      InspectorGUI.ToolArrayGUI( this,
                                 Track.Wheels,
                                 "Wheels",
                                 Color.Lerp( Color.yellow, Color.white, 0.25f ),
                                 wheel => Track.Add( wheel ),
                                 wheel => Track.Remove( wheel ) );
    }

    private bool SelectWheelToolEnable
    {
      get
      {
        return GetChild<SelectGameObjectTool>() != null &&
               GetChild<SelectGameObjectTool>().OnSelect == OnWheelSelect;
      }
      set
      {
        if ( value && !SelectWheelToolEnable )
          AddChild( new SelectGameObjectTool()
          {
            OnSelect = OnWheelSelect
          } );
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
    }

    private void OnWheelSelect( GameObject selection )
    {
      if ( selection == null ) {
        Debug.LogError( "Invalid TrackWheel selection - selected object is null." );
        return;
      }

      var rb = selection.GetComponentInParent<RigidBody>();
      if ( rb == null ) {
        Debug.LogError( "Invalid TrackWheel selection - unable to find RigidBody component.", selection );
        return;
      }

      var createNewComponent = rb.GetComponent<TrackWheel>() == null;
      if ( createNewComponent )
        Undo.RegisterCreatedObjectUndo( rb.gameObject.AddComponent<TrackWheel>(), "Create TrackWheel" );
      else if ( Track.Contains( rb.GetComponent<TrackWheel>() ) ) {
        Debug.Log( "TrackWheel already part of Track - ignoring selection." );
        return;
      }

      if ( !Track.Add( rb.GetComponent<TrackWheel>() ) ) {
        Debug.LogError( "Track failed to add TrackWheel instance.", Track );
        if ( createNewComponent )
          Object.DestroyImmediate( rb.GetComponent<TrackWheel>() );
        return;
      }

      EditorUtility.SetDirty( Track );
    }
  }
}
