
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Capsule ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideCapsuleEditor : InspectorEditor
  { }
}