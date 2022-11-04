
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.ObserverFrameRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingObserverFrameRendererEditor : InspectorEditor
  { }
}