
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.HeightField ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideHeightFieldEditor : InspectorEditor
  { }
}