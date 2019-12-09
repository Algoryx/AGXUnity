using UnityEngine;
using UnityEditor;
using AGXUnity.Models;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( TrackWheel ) )]
  public class TrackWheelTool : CustomTargetTool
  {
    public TrackWheel TrackWheel { get { return Targets[ 0 ] as TrackWheel; } }

    public TrackWheelTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      TrackWheelFrameToolEnable = true;
    }

    public override void OnRemove()
    {
    }

    public override void OnPreTargetMembersGUI()
    {
    }

    private FrameTool TrackWheelFrameTool
    {
      get { return FindActive<FrameTool>( tool => tool.Frame == TrackWheel.Frame ); }
    }

    private bool TrackWheelFrameToolEnable
    {
      get { return TrackWheelFrameTool != null; }
      set
      {
        if ( value && !TrackWheelFrameToolEnable )
          AddChild( new FrameTool( TrackWheel.Frame )
          {
            UndoRedoRecordObject = TrackWheel,
            IsSingleInstanceTool = false
          } );
        else if ( !value )
          RemoveChild( TrackWheelFrameTool );
      }
    }
  }
}
