using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Steering ) )]
  public class SteeringTool : CustomTargetTool
  {
    public Steering Steering => Targets[ 0 ] as Steering;

    public SteeringTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( GUILayout.Button( new GUIContent( "Revert to default parameters", "Reverts to the default steering parameters for the current steering model" ) ) )
        Steering.AssignDefaults();
      if ( Steering.LeftWheel == null ||Steering.RightWheel == null ) {
        EditorGUILayout.HelpBox( "Right and Left WheelJoints must both be set", MessageType.Error );
        return;
      }
      if ( !Steering.ValidateWheelRotations() )
        EditorGUILayout.HelpBox( "Right and Left WheelJoints must have parallel steering axes", MessageType.Error );
      if ( !Steering.ValidateWheelConnectedParents() )
        EditorGUILayout.HelpBox( "Right and Left WheelJoints must have the same connected parent object", MessageType.Error );
    }
  }
}
