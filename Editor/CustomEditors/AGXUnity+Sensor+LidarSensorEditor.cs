
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Sensor.LidarSensor ) )]
  [CanEditMultipleObjects]
  public class AGXUnitySensorLidarSensorEditor : InspectorEditor
  { }
}