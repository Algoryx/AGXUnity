
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Sensor.LidarSurfaceMaterial ) )]
  [CanEditMultipleObjects]
  public class AGXUnitySensorLidarSurfaceMaterialEditor : InspectorEditor
  { }
}