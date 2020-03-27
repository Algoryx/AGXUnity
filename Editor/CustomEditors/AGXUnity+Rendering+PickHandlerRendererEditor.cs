
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.PickHandlerRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingPickHandlerRendererEditor : InspectorEditor
  { }
}