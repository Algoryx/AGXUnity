
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Model.MovableTerrain ) )]
  [CanEditMultipleObjects]
  public class AGXUnityModelMovableTerrainEditor : InspectorEditor
  { }
}