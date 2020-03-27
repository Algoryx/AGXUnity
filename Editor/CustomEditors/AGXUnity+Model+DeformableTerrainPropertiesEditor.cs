
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Model.DeformableTerrainProperties ) )]
  [CanEditMultipleObjects]
  public class AGXUnityModelDeformableTerrainPropertiesEditor : InspectorEditor
  { }
}