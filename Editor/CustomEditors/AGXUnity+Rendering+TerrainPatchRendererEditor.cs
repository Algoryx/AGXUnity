
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.TerrainPatchRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingTerrainPatchRendererEditor : InspectorEditor
  { }
}