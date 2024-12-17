
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.LidarPointCloudRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingLidarPointCloudRendererEditor : InspectorEditor
  { }
}