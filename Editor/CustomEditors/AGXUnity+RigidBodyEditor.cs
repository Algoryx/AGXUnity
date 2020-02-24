
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.RigidBody ) )]
  [CanEditMultipleObjects]
  public class AGXUnityRigidBodyEditor : InspectorEditor
  { }
}