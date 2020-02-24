
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.CollisionGroups ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollisionGroupsEditor : InspectorEditor
  { }
}