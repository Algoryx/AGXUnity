
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.IO.URDF.ElementComponent ) )]
  [CanEditMultipleObjects]
  public class AGXUnityIOURDFElementComponentEditor : InspectorEditor
  { }
}