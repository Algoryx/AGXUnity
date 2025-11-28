using AGXUnity.Model;
using UnityEditor;
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
      if ( Steering.Parameters != null && Steering.Parameters.Mechanism != Steering.Mechanism )
        EditorGUILayout.HelpBox( "The steering mechanism set in the steering parameters does " +
                                 "not match the steering mechanism of the steering component. " +
                                 "This might cause unintended effects.",
                                 MessageType.Warning );
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
