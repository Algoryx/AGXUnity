
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Cylinder ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideCylinderEditor : InspectorEditor
  { }
}