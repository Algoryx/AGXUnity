
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.DeformableTerrainParticleRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingDeformableTerrainParticleRendererEditor : InspectorEditor
  { }
}