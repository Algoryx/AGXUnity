
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.ContactMaterial ) )]
  [CanEditMultipleObjects]
  public class AGXUnityContactMaterialEditor : InspectorEditor
  { }
}