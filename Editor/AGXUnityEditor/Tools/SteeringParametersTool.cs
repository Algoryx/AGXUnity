using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( SteeringParameters ) )]
  public class SteeringParametersTool : CustomTargetTool
  {
    public SteeringParameters SteeringParameters => Targets[ 0 ] as SteeringParameters;

    public SteeringParametersTool( Object[] targets )
      : base( targets )
    { }

    public override void OnPreTargetMembersGUI()
    {
      using ( new EditorGUILayout.HorizontalScope() ) {
        SteeringParameters.Mechanism = (Steering.SteeringMechanism)EditorGUILayout.EnumPopup(
          new GUIContent( "Steering Mechanism", "The steering mechanism that this set of steering parameters is inteded for" ),
          SteeringParameters.Mechanism );
        if ( InspectorGUI.Button( MiscIcon.Update, true, "Assign default steering parameters given the selected steering mechanism", GUILayout.Height( EditorGUIUtility.singleLineHeight ), GUILayout.Width( EditorGUIUtility.singleLineHeight ) ) )
          SteeringParameters.AssignDefaults();
      }
    }
  }
}
