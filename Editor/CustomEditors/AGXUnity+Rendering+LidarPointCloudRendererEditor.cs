using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Rendering.LidarPointCloudRenderer ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRenderingLidarPointCloudRendererEditor : InspectorEditor
  { }
}
