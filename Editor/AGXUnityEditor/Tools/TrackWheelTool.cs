using UnityEngine;
using UnityEditor;
using AGXUnity.Models;

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

    public override void OnPreTargetMembersGUI()
    {
    }
  }
}
