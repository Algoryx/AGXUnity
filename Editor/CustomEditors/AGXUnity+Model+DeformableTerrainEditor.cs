
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Model.DeformableTerrain ) )]
  [CanEditMultipleObjects]
  public class AGXUnityModelDeformableTerrainEditor : InspectorEditor
  { }
}