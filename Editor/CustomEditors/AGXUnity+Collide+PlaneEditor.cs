
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Plane ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollidePlaneEditor : InspectorEditor
  { }
}