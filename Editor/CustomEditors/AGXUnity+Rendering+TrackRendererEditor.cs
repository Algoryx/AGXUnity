
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.TrackRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingTrackRendererEditor : InspectorEditor
  { }
}