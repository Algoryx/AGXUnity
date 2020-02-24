
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.WireRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingWireRendererEditor : InspectorEditor
  { }
}