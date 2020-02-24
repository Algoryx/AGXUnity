
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Collide.Box ) )]
  [CanEditMultipleObjects]
  public class AGXUnityCollideBoxEditor : InspectorEditor
  { }
}