using AGXUnity.Sensor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( ImuSensor ) )]
  [CanEditMultipleObjects]
  public class ImuSensorEditor : Editor
  {
    public override VisualElement CreateInspectorGUI()
    {
      var container = new VisualElement();
      container.Add( new PropertyField( serializedObject.FindProperty( "m_measuredRigidBody" ), "Measured Rigid Body" ) );
      container.Add( new PropertyField( serializedObject.FindProperty( "m_subcomponents" ) ) );
      return container;
    }
  }
}
