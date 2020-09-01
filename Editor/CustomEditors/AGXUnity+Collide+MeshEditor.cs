
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Mesh ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideMeshEditor : InspectorEditor
  { }
}