
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Model.DeformableTerrainMaterial ) )]
  [CanEditMultipleObjects]
  public class AGXUnityModelDeformableTerrainMaterialEditor : InspectorEditor
  { }
}