
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Sphere ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideSphereEditor : InspectorEditor
  { }
}