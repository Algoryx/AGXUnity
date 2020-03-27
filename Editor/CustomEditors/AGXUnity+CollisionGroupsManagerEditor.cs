
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.CollisionGroupsManager ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollisionGroupsManagerEditor : InspectorEditor
  { }
}